using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Crm.Data;
using Sillar.Modules.Crm.Domain;

namespace Sillar.Modules.Crm.Authentication;

/// <summary>Secretos entregados al abrir una sesión de cliente.</summary>
internal sealed record CustomerSessionGrant(
    int SessionId,
    string SessionToken,
    string CsrfToken);

/// <summary>
/// Abre, rota el CSRF y revoca sesiones de cliente.
/// </summary>
/// <remarks>
/// No decide cuánto dura una sesión: esa política pertenece al acceso público
/// y se fijará junto con el login. Aquí se exige una fecha de expiración
/// explícita para no copiar silenciosamente SessionPolicy de CORE.
/// </remarks>
internal sealed class CustomerSessionService(
    CrmDbContext database,
    TimeProvider clock)
{
    /// <summary>
    /// Abre una sesión para una cuenta cuya ficha siga activa.
    /// </summary>
    public async Task<CustomerSessionGrant?> OpenAsync(
        int customerAccountId,
        DateTimeOffset expiresAt,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        if (expiresAt <= now)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAt),
                "La expiración de una sesión de cliente debe estar en el futuro.");
        }

        var canOpen = await (
            from account in database.CustomerAccounts.AsNoTracking()
            join customer in database.Customers.AsNoTracking()
                on account.CustomerId equals customer.CustomerId
            where account.CustomerAccountId == customerAccountId
                && customer.IsActive
            select account.CustomerAccountId)
            .AnyAsync(cancellationToken);

        if (!canOpen)
        {
            return null;
        }

        var sessionToken = SessionTokens.CreateSessionToken();
        var csrfToken = CustomerSessionSecrets.CreateCsrfToken();

        var session = new CustomerSession
        {
            CustomerAccountId = customerAccountId,
            TokenHash = SessionTokens.Hash(sessionToken),
            CsrfTokenHash = SessionTokens.Hash(csrfToken),
            IssuedAt = now,
            LastSeenAt = now,
            ExpiresAt = expiresAt,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        database.CustomerSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);

        return new CustomerSessionGrant(
            session.CustomerSessionId,
            sessionToken,
            csrfToken);
    }

    /// <summary>
    /// Rota el CSRF de una sesión todavía válida.
    /// </summary>
    /// <remarks>
    /// Solo se guarda su SHA-256. El token anterior deja de servir en cuanto
    /// termina esta operación.
    /// </remarks>
    public async Task<string?> RotateCsrfAsync(
        int sessionId,
        int customerAccountId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var token = CustomerSessionSecrets.CreateCsrfToken();
        var hash = SessionTokens.Hash(token);

        var affected = await database.CustomerSessions
            .Where(session =>
                session.CustomerSessionId == sessionId
                && session.CustomerAccountId == customerAccountId
                && session.RevokedAt == null
                && session.ExpiresAt > now)
            .ExecuteUpdateAsync(
                update => update.SetProperty(
                    session => session.CsrfTokenHash,
                    hash),
                cancellationToken);

        return affected == 1 ? token : null;
    }

    /// <summary>
    /// Revoca la sesión en base de datos.
    /// </summary>
    /// <remarks>
    /// Borrar la cookie del navegador no basta: un testigo copiado seguiría
    /// funcionando. La revocación de esta fila es el cierre real.
    /// </remarks>
    public async Task<bool> LogoutAsync(
        int sessionId,
        int customerAccountId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        var affected = await database.CustomerSessions
            .Where(session =>
                session.CustomerSessionId == sessionId
                && session.CustomerAccountId == customerAccountId
                && session.RevokedAt == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(
                    session => session.RevokedAt,
                    now),
                cancellationToken);

        return affected == 1;
    }
}
