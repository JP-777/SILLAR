using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Crm.Data;
using Sillar.Modules.Crm.Domain;
using Sillar.Modules.Crm.Dtos;

namespace Sillar.Modules.Crm.Contact;

internal enum ContactMessageOutcome
{
    Ok,
    Invalid,
    RateLimited,
    NotFound
}

internal sealed record ContactMessageOperation(
    ContactMessageOutcome Outcome,
    string? Error = null,
    TimeSpan? RetryAfter = null,
    AdminContactMessageDetailResponse? Contact = null);

/// <summary>Captación pública y lectura administrativa de contacto.</summary>
internal sealed class ContactMessageService(
    CrmDbContext database,
    ContactSubmissionThrottle throttle,
    IAuditWriter audit)
{
    public async Task<ContactMessageOperation> SubmitAsync(
        PublicContactRequest request,
        Guid? customerId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (!throttle.TryAcquire(
            ipAddress,
            out var retryAfter))
        {
            return new ContactMessageOperation(
                ContactMessageOutcome.RateLimited,
                "Se alcanzó temporalmente el límite de mensajes.",
                retryAfter);
        }

        var validation = Validate(request);
        if (validation is not null)
        {
            return new ContactMessageOperation(
                ContactMessageOutcome.Invalid,
                validation);
        }

        // El customerId solo puede venir de una sesión de cliente que el
        // endpoint ya autenticó. El mensaje conserva igualmente su snapshot
        // de nombre/correo/teléfono: no depende de cambios futuros de la ficha.
        var contact = new ContactMessage
        {
            CustomerId = customerId,
            FullName = request.FullName!.Trim(),
            Email = Optional(request.Email),
            Phone = Optional(request.Phone),
            Subject = Optional(request.Subject),
            Message = request.Message!.Trim(),
            IsActive = true
        };

        database.ContactMessages.Add(contact);
        await database.SaveChangesAsync(cancellationToken);

        return new ContactMessageOperation(
            ContactMessageOutcome.Ok);
    }

    public async Task<IReadOnlyList<AdminContactMessageListItemResponse>>
        ListAdminAsync(
            bool includeInactive,
            CancellationToken cancellationToken)
    {
        var query = database.ContactMessages
            .AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(message => message.IsActive);
        }

        return await query
            .OrderByDescending(message => message.CreatedAt)
            .Take(100)
            .Select(message =>
                new AdminContactMessageListItemResponse(
                    message.ContactMessageId,
                    message.CustomerId,
                    message.FullName,
                    message.Email,
                    message.Phone,
                    message.Subject,
                    message.IsActive,
                    message.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminContactMessageDetailResponse?> GetAdminAsync(
        int contactMessageId,
        CancellationToken cancellationToken)
        => await database.ContactMessages
            .AsNoTracking()
            .Where(message =>
                message.ContactMessageId == contactMessageId)
            .Select(message =>
                new AdminContactMessageDetailResponse(
                    message.ContactMessageId,
                    message.CustomerId,
                    message.FullName,
                    message.Email,
                    message.Phone,
                    message.Subject,
                    message.Message,
                    message.IsActive,
                    message.CreatedAt,
                    message.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ContactMessageOperation> DeactivateAsync(
        int contactMessageId,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var contact = await database.ContactMessages
            .SingleOrDefaultAsync(
                message =>
                    message.ContactMessageId
                    == contactMessageId,
                cancellationToken);

        if (contact is null)
        {
            return new ContactMessageOperation(
                ContactMessageOutcome.NotFound);
        }

        if (!contact.IsActive)
        {
            return new ContactMessageOperation(
                ContactMessageOutcome.Ok,
                Contact: Project(contact));
        }

        contact.IsActive = false;
        await database.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            new AuditEntry(AuditAction.Delete)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CrmModule.ModuleCode,
                EntityType = "contact_message",
                EntityId =
                    contact.ContactMessageId.ToString(),
                // **Sin el `#{id}`.** La regla de producto no habla de `uuid`, habla
                // de identificadores internos: que éste sea un entero lo hace
                // igual de interno y solo lo hace más difícil de detectar — la
                // prueba transversal busca `uuid` y un `#42` le pasa por
                // delante sin que salte nada.
                //
                // La fila sigue siendo identificable por `EntityType` y
                // `EntityId`, que es donde vive el dato, y se consulta
                // desplegando el detalle. El resumen dice qué pasó.
                Summary = "Baja del mensaje de contacto."
            },
            cancellationToken);

        return new ContactMessageOperation(
            ContactMessageOutcome.Ok,
            Contact: Project(contact));
    }

    private static AdminContactMessageDetailResponse Project(
        ContactMessage message)
        => new(
            message.ContactMessageId,
            message.CustomerId,
            message.FullName,
            message.Email,
            message.Phone,
            message.Subject,
            message.Message,
            message.IsActive,
            message.CreatedAt,
            message.UpdatedAt);

    private static string? Validate(
        PublicContactRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.FullName))
        {
            return "El nombre es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(
            request.Message))
        {
            return "El mensaje es obligatorio.";
        }

        var email = Optional(request.Email);
        var phone = Optional(request.Phone);

        if (email is null && phone is null)
        {
            return "Indica al menos un correo o un teléfono.";
        }

        if (email is not null
            && (email.Length > 150
                || !System.Net.Mail.MailAddress.TryCreate(
                    email,
                    out _)))
        {
            return "Ingresa un correo válido.";
        }

        return null;
    }

    private static string? Optional(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
