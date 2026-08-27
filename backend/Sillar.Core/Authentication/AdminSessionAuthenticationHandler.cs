using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sillar.Core.Contracts;
using Sillar.Core.Data;

namespace Sillar.Core.Authentication;

/// <summary>
/// Autentica cada petición contra la fila de <c>core.admin_sessions</c>.
/// </summary>
/// <remarks>
/// La cookie no lleva ningún dato firmado: solo un token opaco. Todo lo que
/// decide —quién eres, si tu sesión sigue viva, si te la revocaron— se lee de la
/// base de datos en cada petición. Es una consulta más por petición, y es
/// exactamente lo que permite cerrar una sesión de verdad desde el panel.
/// </remarks>
public sealed class AdminSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    CoreDbContext database,
    TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    /// <summary>Nombre del esquema de autenticación.</summary>
    public const string SchemeName = "SillarAdminSession";

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Cookies[AdminSessionCookie.Name];

        // Sin cookie no hay nada que decir: no es un fallo, es una petición
        // anónima. Los endpoints públicos siguen su curso.
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        var tokenHash = SessionTokens.Hash(token);

        var session = await database.AdminSessions
            .Include(candidate => candidate.AdminUser)
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == tokenHash);

        if (session?.AdminUser is null)
        {
            return AuthenticateResult.Fail("Sesión desconocida.");
        }

        var now = clock.GetUtcNow();
        var state = SessionPolicy.Evaluate(session.IssuedAt, session.LastSeenAt, session.RevokedAt, now);

        if (state is not SessionState.Valid)
        {
            return AuthenticateResult.Fail($"Sesión no utilizable: {state}.");
        }

        // Desactivar a alguien tiene que echarlo del panel aunque su sesión siga
        // en plazo. Las sesiones se revocan al desactivar, pero esta comprobación
        // cierra la ventana entre ambas cosas.
        if (!session.AdminUser.IsActive)
        {
            return AuthenticateResult.Fail("La cuenta está desactivada.");
        }

        await RenewIfDueAsync(session, now);

        var user = session.AdminUser;
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.AdminUserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(AdminSessionClaims.SessionId, session.AdminSessionId.ToString()),
            new Claim(CsrfEndpointFilter.ClaimType, session.CsrfTokenHash)
        ], SchemeName);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>Renovación deslizante, con freno.</summary>
    /// <remarks>
    /// Solo se escribe si <c>last_seen_at</c> tiene más de un minuto. Sin ese
    /// umbral, cada clic del panel sería una escritura en la base de datos para
    /// anotar algo que ya se sabía.
    /// </remarks>
    private async Task RenewIfDueAsync(Domain.AdminSession session, DateTimeOffset now)
    {
        if (!SessionPolicy.ShouldRenew(session.LastSeenAt, now))
        {
            return;
        }

        session.LastSeenAt = now;
        session.ExpiresAt = SessionPolicy.ExpiresAt(session.IssuedAt, now);
        await database.SaveChangesAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sin cabecera <c>WWW-Authenticate</c>: esto es un API con cookie, no
    /// autenticación HTTP, y provocaría el cuadro de diálogo del navegador.
    /// </remarks>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
