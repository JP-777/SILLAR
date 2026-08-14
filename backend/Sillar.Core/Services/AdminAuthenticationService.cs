using Microsoft.EntityFrameworkCore;
using Sillar.Core.Auditing;
using Sillar.Core.Authentication;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Core.Domain.Values;

namespace Sillar.Core.Services;

/// <summary>Resultado de un intento de acceso, ya con sus efectos aplicados.</summary>
internal sealed record LoginAttempt(
    LoginOutcome Outcome,
    DateTimeOffset? LockedUntil = null,
    AdminUser? User = null,
    string? SessionToken = null,
    string? CsrfToken = null);

/// <summary>Inicio y cierre de sesión, y cambio de contraseña.</summary>
internal sealed class AdminAuthenticationService(
    CoreDbContext database,
    IPasswordHasher hasher,
    IAuditWriter audit,
    TimeProvider clock)
{
    /// <summary>Ejecuta la secuencia de inicio de sesión de la entrega 2 §4.</summary>
    /// <remarks>
    /// La decisión vive en <see cref="LoginEvaluator"/>, que no toca la base de
    /// datos y está probado entero. Aquí solo se aplican las consecuencias.
    /// </remarks>
    public async Task<LoginAttempt> LoginAsync(
        string email,
        string password,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        // La columna usa la colación core.es_ci, así que esta igualdad no
        // distingue mayúsculas: PERSONA@EJEMPLO.PE encuentra la misma cuenta.
        var user = await database.AdminUsers
            .FirstOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);

        var credentials = user is null
            ? null
            : new AdminUserCredentials(
                user.AdminUserId,
                user.Email,
                user.FullName,
                user.Role,
                user.PasswordHash,
                user.IsActive,
                user.LockedUntil,
                user.FailedLoginCount);

        var result = LoginEvaluator.Evaluate(credentials, password, hasher, now);

        switch (result.Outcome)
        {
            case LoginOutcome.UnknownEmail:
                // Se audita igual que un fallo real, con el correo intentado:
                // una ráfaga de intentos contra correos inexistentes es
                // justamente lo que interesa poder ver después.
                await AuditFailureAsync(null, email, "Intento de acceso con un correo no registrado.", cancellationToken);
                return new LoginAttempt(result.Outcome);

            case LoginOutcome.WrongPassword:
                return await RegisterFailedAttemptAsync(user!, now, cancellationToken);

            case LoginOutcome.Locked:
                await AuditFailureAsync(user!.AdminUserId, email, "Acceso con la cuenta bloqueada.", cancellationToken);
                return new LoginAttempt(result.Outcome, result.LockedUntil);

            case LoginOutcome.Inactive:
                await AuditFailureAsync(user!.AdminUserId, email, "Acceso con la cuenta desactivada.", cancellationToken);
                return new LoginAttempt(result.Outcome);

            default:
                return await GrantAsync(user!, now, userAgent, cancellationToken);
        }
    }

    /// <summary>Cierra la sesión revocando su fila.</summary>
    /// <remarks>
    /// Borrar la cookie solo limpia el navegador. Lo que cierra la sesión de
    /// verdad es esta marca: a partir de aquí, ese token no vale aunque alguien
    /// lo hubiera copiado.
    /// </remarks>
    public async Task LogoutAsync(Guid sessionId, int adminUserId, string email, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        await database.AdminSessions
            .Where(session => session.AdminSessionId == sessionId && session.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now), cancellationToken);

        await audit.WriteAsync(
            new AuditEntry(AuditAction.Logout)
            {
                AdminUserId = adminUserId,
                AdminUserEmail = email,
                ModuleCode = CoreModule.ModuleCode,
                EntityType = "admin_session",
                EntityId = sessionId.ToString(),
                Summary = "Cierre de sesión."
            },
            cancellationToken);
    }

    /// <summary>Devuelve el token CSRF de una sesión activa.</summary>
    /// <remarks>
    /// El token en claro no se guarda en ninguna parte, así que no se puede
    /// recuperar: se emite uno nuevo y se sustituye el hash de la sesión. El
    /// anterior deja de valer, que es lo correcto si el frontend lo perdió.
    /// </remarks>
    public async Task<string?> RefreshCsrfTokenAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await database.AdminSessions
            .FirstOrDefaultAsync(candidate => candidate.AdminSessionId == sessionId, cancellationToken);

        if (session is null || session.RevokedAt is not null)
        {
            return null;
        }

        var csrfToken = SessionTokens.CreateCsrfToken();
        session.CsrfTokenHash = SessionTokens.Hash(csrfToken);
        await database.SaveChangesAsync(cancellationToken);

        return csrfToken;
    }

    /// <summary>Cambia la contraseña del usuario en sesión.</summary>
    /// <returns>El motivo del rechazo, o <c>null</c> si se cambió.</returns>
    public async Task<string?> ChangePasswordAsync(
        int adminUserId,
        Guid currentSessionId,
        string currentPassword,
        string? newPassword,
        CancellationToken cancellationToken)
    {
        var user = await database.AdminUsers
            .FirstOrDefaultAsync(candidate => candidate.AdminUserId == adminUserId, cancellationToken);

        if (user is null)
        {
            return "No se encontró la cuenta.";
        }

        // Se exige la contraseña actual aunque haya sesión abierta: si alguien
        // se dejó el panel abierto en el mostrador, no debe poder cambiarla.
        if (!hasher.Verify(currentPassword, user.PasswordHash))
        {
            return "La contraseña actual no es correcta.";
        }

        var check = PasswordPolicy.Check(newPassword, user.Email, user.FullName);
        if (!check.IsValid)
        {
            return check.Error;
        }

        var now = clock.GetUtcNow();
        user.PasswordHash = hasher.Hash(newPassword!);

        // Quien cambia su contraseña suele hacerlo porque sospecha. Dejar vivas
        // las demás sesiones anularía el gesto; la actual se conserva para no
        // echar de la aplicación a quien acaba de protegerla.
        await database.AdminSessions
            .Where(session => session.AdminUserId == adminUserId
                && session.AdminSessionId != currentSessionId
                && session.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now), cancellationToken);

        await database.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            new AuditEntry(AuditAction.Update)
            {
                AdminUserId = adminUserId,
                AdminUserEmail = user.Email,
                ModuleCode = CoreModule.ModuleCode,
                EntityType = "admin_user",
                EntityId = adminUserId.ToString(),
                Summary = "Cambio de contraseña propio. Se revocaron las demás sesiones."
            },
            cancellationToken);

        return null;
    }

    /// <summary>Suma el intento fallido y bloquea si toca.</summary>
    private async Task<LoginAttempt> RegisterFailedAttemptAsync(
        AdminUser user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        user.FailedLoginCount++;

        var lockedUntil = LockoutPolicy.LockedUntil(user.FailedLoginCount, now);
        if (lockedUntil is not null)
        {
            user.LockedUntil = lockedUntil;
        }

        await database.SaveChangesAsync(cancellationToken);

        await AuditFailureAsync(
            user.AdminUserId,
            user.Email,
            lockedUntil is null
                ? $"Contraseña incorrecta. Intentos fallidos: {user.FailedLoginCount}."
                : $"Contraseña incorrecta. Cuenta bloqueada hasta {lockedUntil:O}.",
            cancellationToken);

        // La respuesta es la misma con cuenta bloqueada o sin bloquear: quien no
        // sabe la contraseña no se entera de nada.
        return new LoginAttempt(LoginOutcome.WrongPassword);
    }

    /// <summary>Abre la sesión.</summary>
    private async Task<LoginAttempt> GrantAsync(
        AdminUser user,
        DateTimeOffset now,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        // Un acceso correcto limpia el historial de fallos y cualquier bloqueo
        // que quedara vencido.
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;

        await database.AdminSessions
            .Where(session => session.AdminUserId == user.AdminUserId
                && session.ExpiresAt < SessionPolicy.PurgeBefore(now))
            .ExecuteDeleteAsync(cancellationToken);

        var sessionToken = SessionTokens.CreateSessionToken();
        var csrfToken = SessionTokens.CreateCsrfToken();

        var session = new AdminSession
        {
            AdminSessionId = Guid.CreateVersion7(),
            AdminUserId = user.AdminUserId,
            TokenHash = SessionTokens.Hash(sessionToken),
            CsrfTokenHash = SessionTokens.Hash(csrfToken),
            IssuedAt = now,
            LastSeenAt = now,
            ExpiresAt = SessionPolicy.ExpiresAt(now, now),
            UserAgent = Truncate(userAgent, 300)
        };

        database.AdminSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            new AuditEntry(AuditAction.Login)
            {
                AdminUserId = user.AdminUserId,
                AdminUserEmail = user.Email,
                ModuleCode = CoreModule.ModuleCode,
                EntityType = "admin_session",
                EntityId = session.AdminSessionId.ToString(),
                Summary = "Inicio de sesión."
            },
            cancellationToken);

        return new LoginAttempt(LoginOutcome.Granted, User: user, SessionToken: sessionToken, CsrfToken: csrfToken);
    }

    private Task AuditFailureAsync(int? adminUserId, string email, string summary, CancellationToken cancellationToken)
        => audit.WriteAsync(
            new AuditEntry(AuditAction.LoginFailed)
            {
                AdminUserId = adminUserId,
                AdminUserEmail = email,
                ModuleCode = CoreModule.ModuleCode,
                EntityType = "admin_user",
                Summary = summary
            },
            cancellationToken);

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];
}
