using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Core.Dtos;

namespace Sillar.Core.Services;

/// <summary>Cómo terminó una operación sobre usuarios.</summary>
internal enum AdminUserOutcome
{
    /// <summary>Hecho.</summary>
    Ok,

    /// <summary>No existe ese usuario.</summary>
    NotFound,

    /// <summary>Los datos enviados no sirven.</summary>
    Invalid,

    /// <summary>La operación dejaría el sistema en un estado que no se admite.</summary>
    Conflict
}

/// <summary>Resultado de una operación sobre usuarios.</summary>
internal sealed record AdminUserOperation(
    AdminUserOutcome Outcome,
    string? Error = null,
    AdminUserResponse? User = null);

/// <summary>Administración de usuarios y de sus sesiones.</summary>
internal sealed class AdminUserService(
    CoreDbContext database,
    IPasswordHasher hasher,
    IAuditWriter audit,
    TimeProvider clock)
{
    /// <summary>Lista todos los administradores.</summary>
    public async Task<IReadOnlyList<AdminUserResponse>> ListAsync(CancellationToken cancellationToken)
        => await database.AdminUsers
            .AsNoTracking()
            .OrderBy(user => user.FullName)
            .Select(user => Project(user))
            .ToListAsync(cancellationToken);

    /// <summary>Da de alta un administrador.</summary>
    public async Task<AdminUserOperation> CreateAsync(
        CreateAdminUserRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var invalid = ValidateIdentity(request.FullName, request.Email, request.Role);
        if (invalid is not null)
        {
            return new AdminUserOperation(AdminUserOutcome.Invalid, invalid);
        }

        var password = PasswordPolicy.Check(request.Password, request.Email!, request.FullName!);
        if (!password.IsValid)
        {
            return new AdminUserOperation(AdminUserOutcome.Invalid, password.Error);
        }

        var user = new AdminUser
        {
            FullName = request.FullName!.Trim(),
            Email = request.Email!.Trim(),
            PasswordHash = hasher.Hash(request.Password!),
            Role = request.Role!,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            IsActive = true
        };

        database.AdminUsers.Add(user);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // La colación core.es_ci hace que el choque ocurra también si solo
            // cambia la caja del correo.
            return new AdminUserOperation(
                AdminUserOutcome.Conflict,
                "Ya existe una cuenta con ese correo.");
        }

        await AuditAsync(AuditAction.Create, actingUserId, actingEmail, user,
            $"Alta de «{user.FullName}» con rol {user.Role}.", cancellationToken);

        return new AdminUserOperation(AdminUserOutcome.Ok, User: Project(user));
    }

    /// <summary>Modifica un administrador.</summary>
    public async Task<AdminUserOperation> UpdateAsync(
        int adminUserId,
        UpdateAdminUserRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var invalid = ValidateIdentity(request.FullName, email: null, request.Role);
        if (invalid is not null)
        {
            return new AdminUserOperation(AdminUserOutcome.Invalid, invalid);
        }

        var user = await database.AdminUsers
            .FirstOrDefaultAsync(candidate => candidate.AdminUserId == adminUserId, cancellationToken);

        if (user is null)
        {
            return new AdminUserOperation(AdminUserOutcome.NotFound);
        }

        var losesRole = user.Role != request.Role;
        var losesAccess = user.IsActive && !request.IsActive;

        if (adminUserId == actingUserId && (losesRole || losesAccess))
        {
            // Nadie se quita a sí mismo el acceso o el rol: es la forma más
            // habitual de dejar un sistema sin nadie que pueda entrar.
            return new AdminUserOperation(
                AdminUserOutcome.Conflict,
                "No puedes cambiar tu propio rol ni desactivar tu propia cuenta.");
        }

        if ((losesRole || losesAccess) && await WouldLeaveNoSuperAdminAsync(user, request, cancellationToken))
        {
            return new AdminUserOperation(
                AdminUserOutcome.Conflict,
                "Debe quedar al menos un super_admin activo.");
        }

        user.FullName = request.FullName!.Trim();
        user.Role = request.Role!;
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.IsActive = request.IsActive;

        await database.SaveChangesAsync(cancellationToken);

        // Cambiar el rol o quitar el acceso tiene efecto ahora, no cuando a esa
        // persona le caduque la sesión dentro de ocho horas.
        if (losesRole || losesAccess)
        {
            await RevokeSessionsOfAsync(adminUserId, cancellationToken);
        }

        await AuditAsync(AuditAction.Update, actingUserId, actingEmail, user,
            $"Modificación de «{user.FullName}»: rol {user.Role}, activo {user.IsActive}.", cancellationToken);

        return new AdminUserOperation(AdminUserOutcome.Ok, User: Project(user));
    }

    /// <summary>Desactiva un administrador. No hay borrado físico.</summary>
    public async Task<AdminUserOperation> DeactivateAsync(
        int adminUserId,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var user = await database.AdminUsers
            .FirstOrDefaultAsync(candidate => candidate.AdminUserId == adminUserId, cancellationToken);

        if (user is null)
        {
            return new AdminUserOperation(AdminUserOutcome.NotFound);
        }

        if (adminUserId == actingUserId)
        {
            return new AdminUserOperation(
                AdminUserOutcome.Conflict,
                "No puedes desactivar tu propia cuenta.");
        }

        if (!user.IsActive)
        {
            return new AdminUserOperation(AdminUserOutcome.Ok, User: Project(user));
        }

        if (user.Role == AdminRole.SuperAdmin && !await OtherActiveSuperAdminsExistAsync(adminUserId, cancellationToken))
        {
            return new AdminUserOperation(
                AdminUserOutcome.Conflict,
                "Debe quedar al menos un super_admin activo.");
        }

        user.IsActive = false;
        await database.SaveChangesAsync(cancellationToken);

        await RevokeSessionsOfAsync(adminUserId, cancellationToken);

        await AuditAsync(AuditAction.Delete, actingUserId, actingEmail, user,
            $"Desactivación de «{user.FullName}». Se revocaron sus sesiones.", cancellationToken);

        return new AdminUserOperation(AdminUserOutcome.Ok, User: Project(user));
    }

    /// <summary>Lista las sesiones, empezando por las que siguen vivas.</summary>
    public async Task<IReadOnlyList<AdminSessionResponse>> ListSessionsAsync(CancellationToken cancellationToken)
        => await database.AdminSessions
            .AsNoTracking()
            .OrderBy(session => session.RevokedAt != null)
            .ThenByDescending(session => session.LastSeenAt)
            .Select(session => new AdminSessionResponse(
                session.AdminSessionId,
                session.AdminUserId,
                session.AdminUser!.Email,
                session.IssuedAt,
                session.LastSeenAt,
                session.ExpiresAt,
                session.RevokedAt,
                session.IpAddress,
                session.UserAgent))
            .ToListAsync(cancellationToken);

    /// <summary>Revoca una sesión concreta.</summary>
    public async Task<bool> RevokeSessionAsync(
        Guid sessionId,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var affected = await database.AdminSessions
            .Where(session => session.AdminSessionId == sessionId && session.RevokedAt == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(session => session.RevokedAt, clock.GetUtcNow()),
                cancellationToken);

        if (affected == 0)
        {
            return false;
        }

        await audit.WriteAsync(
            new AuditEntry(AuditAction.Delete)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CoreModule.ModuleCode,
                EntityType = "admin_session",
                EntityId = sessionId.ToString(),
                Summary = "Revocación manual de una sesión."
            },
            cancellationToken);

        return true;
    }

    /// <summary>Comprueba si la modificación dejaría el sistema sin super_admin.</summary>
    private async Task<bool> WouldLeaveNoSuperAdminAsync(
        AdminUser user,
        UpdateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        var wasSuperAdmin = user is { Role: AdminRole.SuperAdmin, IsActive: true };
        var staysSuperAdmin = request is { Role: AdminRole.SuperAdmin, IsActive: true };

        if (!wasSuperAdmin || staysSuperAdmin)
        {
            return false;
        }

        return !await OtherActiveSuperAdminsExistAsync(user.AdminUserId, cancellationToken);
    }

    /// <summary>
    /// Indica si queda algún <c>super_admin</c> activo aparte del indicado.
    /// </summary>
    /// <remarks>
    /// Hoy esta comprobación nunca falla, y conviene saber por qué: quien
    /// ejecuta la operación es siempre un <c>super_admin</c> activo —lo exige la
    /// política de la ruta, y el esquema de autenticación rechaza la sesión de
    /// una cuenta desactivada—, así que al excluir solo al usuario afectado, el
    /// propio actor basta para que la respuesta sea afirmativa. El único camino
    /// hasta cero es que alguien se lo haga a sí mismo, y eso lo corta antes la
    /// regla de no tocarse uno mismo.
    ///
    /// Se conserva porque la regla del SPEC es sobre el estado del sistema, no
    /// sobre quién la provoca: el día que exista un traspaso de propiedad, una
    /// operación por lotes o un mantenimiento sin sesión, este será el único
    /// sitio que impida quedarse sin nadie que pueda entrar.
    /// </remarks>
    private Task<bool> OtherActiveSuperAdminsExistAsync(int excludedUserId, CancellationToken cancellationToken)
        => database.AdminUsers.AnyAsync(
            candidate => candidate.AdminUserId != excludedUserId
                && candidate.IsActive
                && candidate.Role == AdminRole.SuperAdmin,
            cancellationToken);

    private Task RevokeSessionsOfAsync(int adminUserId, CancellationToken cancellationToken)
        => database.AdminSessions
            .Where(session => session.AdminUserId == adminUserId && session.RevokedAt == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(session => session.RevokedAt, clock.GetUtcNow()),
                cancellationToken);

    private Task AuditAsync(
        string action,
        int actingUserId,
        string actingEmail,
        AdminUser affected,
        string summary,
        CancellationToken cancellationToken)
        => audit.WriteAsync(
            new AuditEntry(action)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CoreModule.ModuleCode,
                EntityType = "admin_user",
                EntityId = affected.AdminUserId.ToString(),
                Summary = summary
            },
            cancellationToken);

    private static string? ValidateIdentity(string? fullName, string? email, string? role)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 150)
        {
            return "El nombre es obligatorio y no puede pasar de 150 caracteres.";
        }

        if (email is not null && !EmailAddress.IsValid(email))
        {
            return "El correo no es válido.";
        }

        return role is null || !AdminRole.All.Contains(role)
            ? $"El rol debe ser uno de: {string.Join(", ", AdminRole.All)}."
            : null;
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static AdminUserResponse Project(AdminUser user) => new(
        user.AdminUserId,
        user.FullName,
        user.Email,
        user.Role,
        user.Phone,
        user.IsActive,
        user.LastLoginAt,
        user.LockedUntil);
}
