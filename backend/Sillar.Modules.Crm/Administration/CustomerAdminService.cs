using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Core.Contracts.Email;
using Sillar.Modules.Crm.Authentication;
using Sillar.Modules.Crm.Data;
using Sillar.Modules.Crm.Domain;
using Sillar.Modules.Crm.Dtos;

namespace Sillar.Modules.Crm.Administration;

internal enum CustomerAdminOutcome
{
    Ok,
    NotFound,
    Invalid,
    Conflict,
    HasAccount,
    Inactive
}

internal sealed record CustomerAdminOperation(
    CustomerAdminOutcome Outcome,
    string? Error = null,
    AdminCustomerDetailResponse? Customer = null,
    AdminCustomerInvitationResponse? Invitation = null);

/// <summary>Gestión de clientes desde el panel administrativo.</summary>
internal sealed class CustomerAdminService(
    CrmDbContext database,
    CustomerAccountTokenService tokens,
    IEmailSender email,
    IAuditWriter audit,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<AdminCustomerListItemResponse>> ListAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var query = database.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var pattern = $"%{term}%";

            query = query.Where(customer =>
                EF.Functions
                    .ToTsVector(
                        "crm.spanish_unaccent",
                        customer.FullName)
                    .Matches(
                        EF.Functions.PlainToTsQuery(
                            "crm.spanish_unaccent",
                            term))
                || EF.Functions.ILike(
                    EF.Functions.Collate(
                        customer.Email,
                        "C"),
                    pattern)
                || (customer.DocumentNumber != null
                    && EF.Functions.ILike(
                        customer.DocumentNumber,
                        pattern)));
        }

        var customers = await query
            .OrderByDescending(customer => customer.IsActive)
            .ThenBy(customer => customer.FullName)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (customers.Count == 0)
        {
            return [];
        }

        var ids = customers
            .Select(customer => customer.CustomerId)
            .ToArray();

        var accounts = await database.CustomerAccounts
            .AsNoTracking()
            .Where(account => ids.Contains(account.CustomerId))
            .ToDictionaryAsync(
                account => account.CustomerId,
                cancellationToken);

        var invitations = await ActiveInvitationsAsync(
            ids,
            cancellationToken);

        return customers
            .Select(customer =>
            {
                accounts.TryGetValue(
                    customer.CustomerId,
                    out var account);

                invitations.TryGetValue(
                    customer.CustomerId,
                    out var invitation);

                return new AdminCustomerListItemResponse(
                    customer.CustomerId,
                    customer.FullName,
                    customer.Email,
                    customer.Phone,
                    customer.DocumentType,
                    customer.DocumentNumber,
                    customer.IsActive,
                    Access(customer, account, invitation));
            })
            .ToList();
    }

    public async Task<AdminCustomerDetailResponse?> GetAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var customer = await database.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CustomerId == customerId,
                cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var account = await database.CustomerAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CustomerId == customerId,
                cancellationToken);

        var invitation = await database.CustomerTokens
            .AsNoTracking()
            .Where(token =>
                token.CustomerId == customerId
                && token.Purpose == CustomerTokenPurpose.Invitation
                && token.UsedAt == null
                && token.ExpiresAt > clock.GetUtcNow())
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var addresses = await database.CustomerAddresses
            .AsNoTracking()
            .Where(address => address.CustomerId == customerId)
            .OrderByDescending(address => address.IsActive)
            .ThenByDescending(address => address.IsPreferred)
            .ThenBy(address => address.CreatedAt)
            .Select(address => new AdminCustomerAddressResponse(
                address.CustomerAddressId,
                address.Label,
                address.AddressLine,
                address.District,
                address.Province,
                address.Department,
                address.Reference,
                address.IsPreferred,
                address.IsActive))
            .ToListAsync(cancellationToken);

        return Project(customer, account, invitation, addresses);
    }

    public async Task<CustomerAdminOperation> CreateAsync(
        CreateAdminCustomerRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var error = Validate(
            request.FullName,
            request.Email,
            request.DocumentType,
            request.DocumentNumber);

        if (error is not null)
        {
            return Invalid(error);
        }

        var customer = new Customer
        {
            FullName = request.FullName!.Trim(),
            Email = request.Email!.Trim(),
            Phone = Optional(request.Phone),
            DocumentType = Optional(request.DocumentType)?.ToLowerInvariant(),
            DocumentNumber = Optional(request.DocumentNumber),
            InternalNotes = Optional(request.InternalNotes),
            IsActive = true
        };

        database.Customers.Add(customer);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception))
        {
            database.ChangeTracker.Clear();

            return new CustomerAdminOperation(
                CustomerAdminOutcome.Conflict,
                ConflictMessage(exception));
        }

        await AuditAsync(
            AuditAction.Create,
            actingUserId,
            actingEmail,
            customer.CustomerId,
            $"Alta administrativa de la ficha «{customer.FullName}».",
            cancellationToken);

        return new CustomerAdminOperation(
            CustomerAdminOutcome.Ok,
            Customer: await GetAsync(
                customer.CustomerId,
                cancellationToken));
    }

    public async Task<CustomerAdminOperation> UpdateAsync(
        Guid customerId,
        UpdateAdminCustomerRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var error = Validate(
            request.FullName,
            request.Email,
            request.DocumentType,
            request.DocumentNumber);

        if (error is not null)
        {
            return Invalid(error);
        }

        var customer = await database.Customers
            .SingleOrDefaultAsync(
                candidate => candidate.CustomerId == customerId,
                cancellationToken);

        if (customer is null)
        {
            return new CustomerAdminOperation(
                CustomerAdminOutcome.NotFound);
        }

        customer.FullName = request.FullName!.Trim();
        customer.Email = request.Email!.Trim();
        customer.Phone = Optional(request.Phone);
        customer.DocumentType =
            Optional(request.DocumentType)?.ToLowerInvariant();
        customer.DocumentNumber = Optional(request.DocumentNumber);
        customer.InternalNotes = Optional(request.InternalNotes);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception))
        {
            database.ChangeTracker.Clear();

            return new CustomerAdminOperation(
                CustomerAdminOutcome.Conflict,
                ConflictMessage(exception));
        }

        await AuditAsync(
            AuditAction.Update,
            actingUserId,
            actingEmail,
            customerId,
            $"Actualización administrativa de la ficha «{customer.FullName}».",
            cancellationToken);

        return new CustomerAdminOperation(
            CustomerAdminOutcome.Ok,
            Customer: await GetAsync(
                customerId,
                cancellationToken));
    }

    public async Task<CustomerAdminOperation> DeactivateAsync(
        Guid customerId,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var customer = await database.Customers
            .SingleOrDefaultAsync(
                candidate => candidate.CustomerId == customerId,
                cancellationToken);

        if (customer is null)
        {
            return new CustomerAdminOperation(
                CustomerAdminOutcome.NotFound);
        }

        if (!customer.IsActive
            && customer.DeactivatedAt is not null)
        {
            return new CustomerAdminOperation(
                CustomerAdminOutcome.Ok,
                Customer: await GetAsync(
                    customerId,
                    cancellationToken));
        }

        var now = clock.GetUtcNow();

        customer.IsActive = false;
        customer.DeactivatedAt = now;
        customer.BlockedAt = null;

        await database.SaveChangesAsync(cancellationToken);

        var accountId = await database.CustomerAccounts
            .AsNoTracking()
            .Where(account => account.CustomerId == customerId)
            .Select(account => (int?)account.CustomerAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (accountId is { } id)
        {
            await database.CustomerSessions
                .Where(session =>
                    session.CustomerAccountId == id
                    && session.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        session => session.RevokedAt,
                        now),
                    cancellationToken);
        }

        await AuditAsync(
            AuditAction.Deactivate,
            actingUserId,
            actingEmail,
            customerId,
            $"Baja de la ficha «{customer.FullName}» y revocación de sus sesiones.",
            cancellationToken);

        return new CustomerAdminOperation(
            CustomerAdminOutcome.Ok,
            Customer: await GetAsync(
                customerId,
                cancellationToken));
    }

    public async Task<CustomerAdminOperation> ReactivateAsync(
        Guid customerId,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var customer = await database.Customers
            .SingleOrDefaultAsync(
                candidate => candidate.CustomerId == customerId,
                cancellationToken);

        if (customer is null)
        {
            return new CustomerAdminOperation(
                CustomerAdminOutcome.NotFound);
        }

        if (customer.IsActive)
        {
            return new CustomerAdminOperation(
                CustomerAdminOutcome.Ok,
                Customer: await GetAsync(
                    customerId,
                    cancellationToken));
        }

        var now = clock.GetUtcNow();
        var wasBlocked = customer.BlockedAt is not null;

        customer.IsActive = true;
        customer.DeactivatedAt = null;
        customer.BlockedAt = null;

        if (wasBlocked)
        {
            customer.ReactivationRequestedAt ??= now;
            customer.ReactivationResolvedAt = now;
        }

        await database.SaveChangesAsync(cancellationToken);

        await AuditAsync(
            AuditAction.Activate,
            actingUserId,
            actingEmail,
            customerId,
            $"Reactivación de la ficha «{customer.FullName}».",
            cancellationToken);

        return new CustomerAdminOperation(
            CustomerAdminOutcome.Ok,
            Customer: await GetAsync(
                customerId,
                cancellationToken));
    }

    public async Task<CustomerAdminOperation> InviteAsync(
        Guid customerId,
        string baseUrl,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var customer = await database.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CustomerId == customerId,
                cancellationToken);

        if (customer is null)
        {
            return new CustomerAdminOperation(
                CustomerAdminOutcome.NotFound);
        }

        if (!customer.IsActive)
        {
            return new CustomerAdminOperation(
                CustomerAdminOutcome.Inactive,
                "La ficha debe estar activa antes de invitar.");
        }

        var hasAccount = await database.CustomerAccounts
            .AsNoTracking()
            .AnyAsync(
                account => account.CustomerId == customerId,
                cancellationToken);

        if (hasAccount)
        {
            return new CustomerAdminOperation(
                CustomerAdminOutcome.HasAccount,
                "La ficha ya tiene una cuenta de cliente.");
        }

        var issued = await tokens.IssueInvitationAsync(
            customerId,
            cancellationToken);

        if (issued is null)
        {
            return new CustomerAdminOperation(
                CustomerAdminOutcome.Conflict,
                "No se pudo emitir la invitación.");
        }

        var send = await email.SendAsync(
            CustomerEmailComposer.Invitation(
                issued,
                baseUrl),
            cancellationToken);

        await AuditAsync(
            AuditAction.Create,
            actingUserId,
            actingEmail,
            customerId,
            $"Invitación de cuenta emitida para «{customer.FullName}».",
            cancellationToken,
            entityType: "customer_invitation");

        var invitation = new AdminCustomerInvitationResponse(
            send.Success,
            send.Success
                ? "Invitación emitida y correo enviado."
                : "Invitación emitida, pero el correo no pudo enviarse.",
            clock.GetUtcNow() + CustomerTokenPolicy.InvitationLifetime);

        return new CustomerAdminOperation(
            CustomerAdminOutcome.Ok,
            Customer: await GetAsync(
                customerId,
                cancellationToken),
            Invitation: invitation);
    }

    private async Task<Dictionary<Guid, CustomerToken>> ActiveInvitationsAsync(
        Guid[] customerIds,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        var tokens = await database.CustomerTokens
            .AsNoTracking()
            .Where(token =>
                customerIds.Contains(token.CustomerId)
                && token.Purpose == CustomerTokenPurpose.Invitation
                && token.UsedAt == null
                && token.ExpiresAt > now)
            .OrderByDescending(token => token.CreatedAt)
            .ToListAsync(cancellationToken);

        return tokens
            .GroupBy(token => token.CustomerId)
            .ToDictionary(
                group => group.Key,
                group => group.First());
    }

    private static AdminCustomerDetailResponse Project(
        Customer customer,
        CustomerAccount? account,
        CustomerToken? invitation,
        IReadOnlyList<AdminCustomerAddressResponse> addresses)
        => new(
            customer.CustomerId,
            customer.FullName,
            customer.Email,
            customer.Phone,
            customer.DocumentType,
            customer.DocumentNumber,
            customer.InternalNotes,
            customer.IsActive,
            customer.DeactivatedAt,
            customer.BlockedAt,
            customer.ReactivationRequestedAt,
            customer.ReactivationResolvedAt,
            Access(customer, account, invitation),
            addresses,
            customer.CreatedAt,
            customer.UpdatedAt);

    private static AdminCustomerAccessResponse Access(
        Customer customer,
        CustomerAccount? account,
        CustomerToken? invitation)
    {
        if (!customer.IsActive
            && customer.BlockedAt is { } blockedAt)
        {
            return new AdminCustomerAccessResponse(
                "blocked",
                blockedAt,
                account?.EmailVerifiedAt is not null,
                invitation?.ExpiresAt);
        }

        if (!customer.IsActive
            && customer.DeactivatedAt is { } deactivatedAt)
        {
            return new AdminCustomerAccessResponse(
                "deactivated",
                deactivatedAt,
                account?.EmailVerifiedAt is not null,
                invitation?.ExpiresAt);
        }

        if (account is not null)
        {
            return new AdminCustomerAccessResponse(
                "active",
                account.CreatedAt,
                account.EmailVerifiedAt is not null,
                null);
        }

        if (invitation is not null)
        {
            return new AdminCustomerAccessResponse(
                "invited",
                invitation.CreatedAt,
                false,
                invitation.ExpiresAt);
        }

        return new AdminCustomerAccessResponse(
            "no_account",
            customer.CreatedAt,
            false,
            null);
    }

    private async Task AuditAsync(
        string action,
        int actingUserId,
        string actingEmail,
        Guid customerId,
        string summary,
        CancellationToken cancellationToken,
        string entityType = "customer")
        => await audit.WriteAsync(
            new AuditEntry(action)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CrmModule.ModuleCode,
                EntityType = entityType,
                EntityId = customerId.ToString(),
                Summary = summary
            },
            cancellationToken);

    private static string? Validate(
        string? fullName,
        string? email,
        string? documentType,
        string? documentNumber)
    {
        var name = fullName?.Trim() ?? string.Empty;
        var mail = email?.Trim() ?? string.Empty;
        var type = Optional(documentType)?.ToLowerInvariant();
        var number = Optional(documentNumber);

        if (name.Length == 0)
        {
            return "El nombre es obligatorio.";
        }

        if (mail.Length == 0
            || mail.Length > 150
            || !System.Net.Mail.MailAddress.TryCreate(mail, out _))
        {
            return "Ingresa un correo válido.";
        }

        if ((type is null) != (number is null))
        {
            return "Tipo y número de documento deben enviarse juntos.";
        }

        if (type is not null
            && type is not "dni" and not "ruc")
        {
            return "El tipo de documento debe ser dni o ruc.";
        }

        return null;
    }

    private static CustomerAdminOperation Invalid(string error)
        => new(CustomerAdminOutcome.Invalid, error);

    private static string? Optional(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static bool IsUniqueViolation(
        DbUpdateException exception)
        => exception.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            };

    private static string ConflictMessage(
        DbUpdateException exception)
        => (exception.InnerException as PostgresException)?.ConstraintName switch
        {
            "uq_customers_email" =>
                "Ya existe una ficha con ese correo.",
            "uq_customers_document" =>
                "Ya existe una ficha con ese documento.",
            _ =>
                "Los datos chocan con otra ficha existente."
        };
}
