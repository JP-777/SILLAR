using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
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

        Domain.AdminSession? session;

        try
        {
            session = await database.AdminSessions
                .Include(candidate => candidate.AdminUser)
                .FirstOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, Context.RequestAborted);
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // **Si todavía no existe el almacenamiento que permite validar una
            // cookie, esa cookie no concede identidad: la autenticación se
            // comporta como ausente.** Es la misma respuesta que sin cookie, y
            // deja que /api/setup/status y /api/setup decidan el estado real.
            //
            // Antes, una cookie vieja contra una base sin `core` convertía una
            // instalación incompleta en un 500 aquí, antes de cualquier ruta:
            // UseAuthentication corre para todas, también las anónimas.
            //
            // Solo si la relación que falta es LA DE SESIONES. Cualquier otro
            // 42P01 —el JOIN con admin_users, sin ir más lejos— es una avería y
            // sube como tal. Esto no es un detector de «modo instalación».
            if (!await SessionStorageExistsAsync())
            {
                Logger.LogWarning(
                    "Cookie de sesión ignorada: {Tabla} no existe en esta base, así que no hay nada contra lo "
                    + "que validarla. La petición sigue como anónima.",
                    SessionTable());

                return AuthenticateResult.NoResult();
            }

            throw;
        }

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

    /// <summary>
    /// ¿Existe la tabla de sesiones en la base a la que apunta el contexto?
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué se pregunta en vez de leerlo del error.</b> Un <c>42P01</c> del
    /// analizador de PostgreSQL <b>no trae <c>SchemaName</c> ni <c>TableName</c></b>:
    /// medido con <c>psql</c> en modo verboso, el servidor solo envía el código, el
    /// texto y la posición. Y el texto sale en el idioma de <c>lc_messages</c>, así
    /// que leerlo sería frágil. Lo que sí es exacto es preguntarle a PostgreSQL
    /// por la relación concreta con <c>to_regclass</c>.
    /// </para>
    /// <para>
    /// Se pregunta <b>solo después</b> de un <c>42P01</c>: en el camino normal no
    /// cuesta nada. Y el nombre sale del modelo de EF, no escrito a mano.
    /// </para>
    /// <para>
    /// <b>Sin <c>catch</c>.</b> Si la pregunta falla —conexión caída, base
    /// inexistente—, es otra avería y sale como tal.
    /// </para>
    /// </remarks>
    internal async Task<bool> SessionStorageExistsAsync()
    {
        var (schema, table) = SessionTableParts();

        return await database.Database
            .SqlQuery<bool>($"SELECT to_regclass(format('%I.%I', {schema}, {table})) IS NOT NULL AS \"Value\"")
            .SingleAsync(Context.RequestAborted);
    }

    private (string Schema, string Table) SessionTableParts()
    {
        var entity = database.Model.FindEntityType(typeof(Domain.AdminSession))
            ?? throw new InvalidOperationException("El modelo de CORE no conoce AdminSession.");

        return (entity.GetSchema() ?? CoreDbContext.Schema, entity.GetTableName()!);
    }

    private string SessionTable()
    {
        var (schema, table) = SessionTableParts();
        return $"{schema}.{table}";
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
