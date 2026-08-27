using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sillar.Core.Contracts;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Crm.Data;

namespace Sillar.Modules.Crm.Authentication;

/// <summary>
/// Autentica una petición exclusivamente contra crm.customer_sessions.
/// </summary>
/// <remarks>
/// No reutiliza el handler administrativo ni consulta core.admin_sessions.
/// En esta primera frontera no renueva sesiones: valida únicamente el estado
/// ya persistido. La política de emisión y renovación llega con el login.
/// </remarks>
public sealed class CustomerSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    CrmDbContext database,
    TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options,
        loggerFactory,
        encoder)
{
    public const string SchemeName = "SillarCustomerSession";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Cookies[CustomerSessionCookie.Name];

        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        var tokenHash = SessionTokens.Hash(token);

        var match = await (
            from session in database.CustomerSessions.AsNoTracking()
            join account in database.CustomerAccounts.AsNoTracking()
                on session.CustomerAccountId equals account.CustomerAccountId
            join customer in database.Customers.AsNoTracking()
                on account.CustomerId equals customer.CustomerId
            where session.TokenHash == tokenHash
            select new
            {
                session.CustomerSessionId,
                session.CustomerAccountId,
                session.CsrfTokenHash,
                session.ExpiresAt,
                session.RevokedAt,
                customer.CustomerId,
                customer.Email,
                customer.IsActive,
                account.EmailVerifiedAt
            })
            .SingleOrDefaultAsync(Context.RequestAborted);

        if (match is null)
        {
            return AuthenticateResult.Fail("Sesión de cliente desconocida.");
        }

        if (match.RevokedAt is not null)
        {
            return AuthenticateResult.Fail("La sesión de cliente fue revocada.");
        }

        if (match.ExpiresAt <= clock.GetUtcNow())
        {
            return AuthenticateResult.Fail("La sesión de cliente caducó.");
        }

        if (!match.IsActive)
        {
            return AuthenticateResult.Fail("La ficha de cliente no está activa.");
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(
                CustomerSessionClaims.CustomerId,
                match.CustomerId.ToString()),
            new Claim(
                CustomerSessionClaims.AccountId,
                match.CustomerAccountId.ToString()),
            new Claim(
                CustomerSessionClaims.SessionId,
                match.CustomerSessionId.ToString()),
            new Claim(
                CustomerSessionClaims.Email,
                match.Email),
            new Claim(
                CustomerSessionClaims.EmailVerified,
                (match.EmailVerifiedAt is not null).ToString()),
            new Claim(
                CustomerCsrfEndpointFilter.ClaimType,
                match.CsrfTokenHash)
        ],
        SchemeName);

        return AuthenticateResult.Success(
            new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                SchemeName));
    }

    protected override Task HandleChallengeAsync(
        AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(
        AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
