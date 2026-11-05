using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Sillar.Core.Authentication;
using Sillar.Core.Data;
using Sillar.Core.Contracts;
using Sillar.Core.Contracts.Email;
using Sillar.Core.Dtos;
using Sillar.Core.Services;
using Sillar.Core.Settings;

namespace Sillar.Core.Endpoints;

/// <summary>Configuración del sitio, pública y administrativa.</summary>
public static class SettingsEndpoints
{
    /// <summary>Monta las rutas de configuración.</summary>
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/settings/public", GetPublic)
            .WithTags("Configuración")
            .WithName("GetPublicSettings")
            .WithSummary("Devuelve las configuraciones marcadas como públicas.")
            .WithDescription(
                "Público y sin sesión. Solo las claves con is_public = true, que vale false por " +
                "defecto: publicar un dato es siempre un acto deliberado.")
            .Produces<IReadOnlyDictionary<string, string>>(StatusCodes.Status200OK);

        var admin = endpoints.MapGroup("/api/admin/settings")
            .WithTags("Configuración")
            .RequireAuthorization(AdminRole.Admin)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("", List)
            .WithName("ListSettings")
            .WithSummary("Lista todas las configuraciones del sitio.")
            .WithDescription(
                "Incluye 'needsSetup', que marca las claves que siguen con el valor del seed y que el " +
                "negocio todavía no ha configurado.")
            .Produces<IReadOnlyList<SettingResponse>>(StatusCodes.Status200OK);

        admin.MapGet("/email/status", GetEmailTestStatus)
            .RequireAuthorization(AdminRole.SuperAdmin)
            .WithName("GetOutgoingEmailTestStatus")
            .WithSummary("Devuelve el estado persistente de la última prueba SMTP.")
            .WithDescription(
                "Solo super_admin. Devuelve neverTested=true mientras no exista una prueba smtp_test auditada.")
            .Produces<EmailTestStatusResponse>(StatusCodes.Status200OK);

        admin.MapPost("/email/test", TestEmail)
            .RequireAuthorization(AdminRole.SuperAdmin)
            .WithName("TestOutgoingEmail")
            .WithSummary("Prueba la configuración SMTP.")
            .WithDescription(
                "Solo super_admin. La contraseña se lee de SILLAR_SMTP_PASSWORD y nunca se devuelve. " +
                "El resultado queda persistido en audit_log y puede consultarse en /email/status.")
            .Produces<TestEmailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        admin.MapGet("/installation-currency", GetInstallationCurrency)
            .WithName("GetInstallationCurrency")
            .WithSummary("Devuelve la moneda de la instalación tal como la acredita CORE.")
            .WithDescription(
                "Lee la base en cada llamada. Solo 'Accredited' es una moneda utilizable: valor en la lista " +
                "cerrada de monedas soportadas y con el registro de auditoría que lo estableció. " +
                "El ajuste público 'currency_code' no interviene.")
            .Produces<InstallationCurrencyResponse>(StatusCodes.Status200OK);

        admin.MapPut("/installation-currency", SetInstallationCurrency)
            .RequireAuthorization(AdminRole.SuperAdmin)
            .WithName("SetInstallationCurrency")
            .WithSummary("Establece o cambia la moneda de la instalación.")
            .WithDescription(
                "Solo super_admin. El valor es un código ISO 4217 exacto de la lista cerrada. El cambio y su " +
                "auditoría, con los valores anterior y nuevo, se guardan en la misma transacción: si la " +
                "auditoría falla, no se guarda nada. Se rechaza mientras no se pueda comprobar que no hay pedidos.")
            .Produces<InstallationCurrencyResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        admin.MapPut("/{key}", Update)
            .WithName("UpdateSetting")
            .WithSummary("Cambia el valor de una configuración.")
            .WithDescription(
                "Las claves no se crean ni se borran desde el API: una clave desconocida responde 404. " +
                "Cambiar 'isPublic' exige rol super_admin, aunque cambiar el valor solo exija admin.")
            .Produces<SettingResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Devuelve las configuraciones públicas.</summary>
    /// <param name="settings">Lector de configuración, servido desde la caché.</param>
    /// <returns>Pares clave-valor de las configuraciones publicadas.</returns>
    private static IResult GetPublic(ISettingsReader settings) => Results.Ok(settings.GetPublic());

    /// <summary>Lista todas las configuraciones.</summary>
    /// <param name="settings">Servicio de configuración.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>Todas las claves con su valor, tipo, visibilidad y estado.</returns>
    private static async Task<IResult> List(
        SiteSettingService settings,
        CancellationToken cancellationToken)
        => Results.Ok(await settings.ListAsync(cancellationToken));

    private static async Task<IResult> GetEmailTestStatus(
        CoreDbContext database,
        CancellationToken cancellationToken)
    {
        var latest = await database.AuditLogs
            .AsNoTracking()
            .Where(entry =>
                entry.Action == AuditAction.EmailSend
                && entry.ModuleCode == CoreModule.ModuleCode
                && entry.EntityType == "email"
                && entry.EntityId == "smtp_test")
            .OrderByDescending(entry => entry.OccurredAt)
            .Select(entry => new
            {
                entry.OccurredAt,
                entry.Summary
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return Results.Ok(
                new EmailTestStatusResponse(
                    NeverTested: true,
                    LastTestedAt: null,
                    LastSuccess: null));
        }

        var success = latest.Summary?.Contains(
            ": enviado.",
            StringComparison.Ordinal) == true;

        return Results.Ok(
            new EmailTestStatusResponse(
                NeverTested: false,
                LastTestedAt: latest.OccurredAt,
                LastSuccess: success));
    }

    private static async Task<IResult> TestEmail(
        TestEmailRequest request,
        IEmailSender email,
        CancellationToken cancellationToken)
    {
        var recipient = request.Recipient?.Trim();

        if (string.IsNullOrWhiteSpace(recipient)
            || !System.Net.Mail.MailAddress.TryCreate(recipient, out _))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["correo"] = ["Ingresa un correo válido."]
                },
                title: "No se pudo probar el correo.");
        }

        var result = await email.SendAsync(
            new OutgoingEmail(
                recipient,
                "SILLAR — correo de prueba",
                "La configuración SMTP de SILLAR funciona correctamente.",
                "smtp_test",
                CoreModule.ModuleCode),
            cancellationToken);

        return Results.Ok(
            new TestEmailResponse(
                result.Success,
                result.Success
                    ? "Correo de prueba enviado."
                    : result.Error ?? "El servidor SMTP rechazó el envío."));
    }

    /// <summary>Devuelve la moneda de la instalación.</summary>
    /// <param name="authority">Lector de la autoridad monetaria.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con el estado de la moneda.</returns>
    private static async Task<IResult> GetInstallationCurrency(
        IInstallationCurrencyAuthority authority,
        CancellationToken cancellationToken)
        => Results.Ok(ToResponse(await authority.ReadAsync(cancellationToken)));

    /// <summary>Establece o cambia la moneda de la instalación.</summary>
    /// <param name="request">Código ISO 4217.</param>
    /// <param name="currency">Operación de moneda.</param>
    /// <param name="authority">Lector, para devolver el estado resultante.</param>
    /// <param name="currentUser">Quién lo pide.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>
    /// 200 con la moneda; 400 si el valor no es de la lista; 403 sin super_admin; 409 si hay pedidos,
    /// no se puede comprobar, la clave no está preparada o una venta retiene el bloqueo de moneda;
    /// 503 si no se pudo guardar con su auditoría.
    /// </returns>
    private static async Task<IResult> SetInstallationCurrency(
        SetInstallationCurrencyRequest request,
        InstallationCurrencyService currency,
        IInstallationCurrencyAuthority authority,
        CurrentAdmin currentUser,
        CancellationToken cancellationToken)
    {
        var result = await currency.SetAsync(
            request.Value,
            currentUser.IsInRole(AdminRole.SuperAdmin),
            currentUser.AdminUserId!.Value,
            currentUser.Email!,
            cancellationToken);

        return result.Outcome switch
        {
            CurrencyChangeOutcome.Changed or CurrencyChangeOutcome.Reattested or CurrencyChangeOutcome.Unchanged
                => Results.Ok(ToResponse(await authority.ReadAsync(cancellationToken))),
            CurrencyChangeOutcome.Invalid => Results.ValidationProblem(
                new Dictionary<string, string[]> { ["valor"] = [result.Message!] },
                title: "La moneda no es válida."),
            CurrencyChangeOutcome.Forbidden => Results.Problem(title: result.Message, statusCode: StatusCodes.Status403Forbidden),
            CurrencyChangeOutcome.AuditNotAtomic or CurrencyChangeOutcome.NotSaved
                => Results.Problem(title: result.Message, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.Problem(title: result.Message, statusCode: StatusCodes.Status409Conflict),
        };
    }

    private static InstallationCurrencyResponse ToResponse(InstallationCurrencyReading reading)
        => new(reading.Status.ToString(), reading.Currency?.Code, reading.Currency?.MinorUnits, reading.ListVersion, reading.ObservedAt);

    /// <summary>Cambia una configuración.</summary>
    /// <param name="key">Clave a modificar. Debe existir.</param>
    /// <param name="request">Valor nuevo y, opcionalmente, la visibilidad.</param>
    /// <param name="settings">Servicio de configuración.</param>
    /// <param name="currentUser">Quién lo pide.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la configuración, 400 si el valor no encaja, 403 si le falta rol, 404 si la clave no existe.</returns>
    private static async Task<IResult> Update(
        string key,
        UpdateSettingRequest request,
        SiteSettingService settings,
        CurrentAdmin currentUser,
        CancellationToken cancellationToken)
    {
        // La distinción de rol se resuelve aquí y no en la ruta porque depende
        // del cuerpo: un admin puede usar este endpoint, pero no para publicar.
        var result = await settings.UpdateAsync(
            key,
            request,
            canChangeVisibility: currentUser.IsInRole(AdminRole.SuperAdmin),
            currentUser.AdminUserId!.Value,
            currentUser.Email!,
            cancellationToken);

        return result.Outcome switch
        {
            SettingOutcome.Ok => Results.Ok(result.Setting),
            SettingOutcome.NotFound => Results.NotFound(),
            SettingOutcome.Forbidden => Results.Problem(
                title: result.Error,
                statusCode: StatusCodes.Status403Forbidden),
            _ => Results.ValidationProblem(
                new Dictionary<string, string[]> { ["valor"] = [result.Error!] },
                title: "El valor de la configuración no es válido.")
        };
    }
}
