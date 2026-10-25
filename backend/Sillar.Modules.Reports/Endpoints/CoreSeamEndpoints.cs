using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;

namespace Sillar.Modules.Reports.Endpoints;

/// <summary>
/// Costura de M10 con CORE: la zona de la instalación y la auditoría de la
/// exportación, leídas de los contratos candidatos de CORE.
/// </summary>
/// <remarks>
/// Los dos productores son opcionales y el host no los registra todavía: sin
/// ellos, M10 dice «sin conexión» o «auditoría no disponible», nunca inventa una
/// zona ni un registro. La exportación queda cerrada mientras la zona no esté
/// verificada, y hoy CORE nunca la verifica.
/// </remarks>
public static class CoreSeamEndpoints
{
    /// <summary>Tipo estable de la exportación de indicadores de pedidos.</summary>
    public const string OrderIndicatorsExportKind = "order_indicators";

    /// <summary>Formato del archivo exportado.</summary>
    public const string CsvFormat = "csv";

    /// <summary>Registra la lectura de la zona y el registro previo a la exportación.</summary>
    public static IEndpointRouteBuilder MapCoreSeamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/reporting")
            .WithTags("Reportes — Instalación")
            .RequireAuthorization(AdminRole.Admin);

        admin.MapGet("/installation-zone", ReadZone)
            .WithName("ReportingAdminInstallationZone")
            .WithSummary("Zona horaria de la instalación tal como la resuelve CORE, traducida a la observación que consume Reportes.")
            .WithDescription("kind: unconnected (CORE no publica la zona), approved_initial (zona inicial aprobada, sin verificar), runtime_resolved (configurada, sin verificar), verified o invalid.")
            .Produces<InstallationZoneObservationView>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        admin.MapPost("/orders/export-audit", RecordExport)
            .WithName("ReportingAdminOrderIndicatorsExportAudit")
            .WithSummary("Registra en la auditoría de CORE una exportación de indicadores de pedidos, antes de entregar el archivo.")
            .WithDescription("Solo se registra con la zona de la instalación verificada, y la zona del archivo tiene que ser esa o UTC. No se guarda el contenido, ni los filtros, ni el nombre del archivo.")
            .AddEndpointFilter<CsrfEndpointFilter>()
            .Produces<ExportAuditDecisionView>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    /// <summary>Lee la zona de CORE. Sin productor registrado, «sin conexión».</summary>
    private static IResult ReadZone(IServiceProvider services)
        => Results.Ok(Observe(services.GetService(typeof(IInstallationTimeZoneResolver)) as IInstallationTimeZoneResolver));

    /// <summary>
    /// Traduce la resolución de CORE. Solo <see cref="InstallationTimeZoneVerification.Verified"/>
    /// produce <c>verified</c>; la zona inicial y la configurada salen sin verificar.
    /// </summary>
    internal static InstallationZoneObservationView Observe(IInstallationTimeZoneResolver? resolver)
    {
        if (resolver is null) return new("unconnected", null, null, null, null);

        var resolution = resolver.Resolve();
        var original = resolution.Setting.OriginalValue;
        if (!resolution.IsResolved || resolution.ZoneId is not { Length: > 0 } zoneId)
        {
            return new("invalid", original, null, null, null);
        }
        if (resolution.Verification == InstallationTimeZoneVerification.Verified)
        {
            return new("verified", original ?? zoneId, zoneId, null, null);
        }
        return resolution.Origin switch
        {
            InstallationTimeZoneOrigin.ApprovedInitial => new("approved_initial", null, null, null, zoneId),
            InstallationTimeZoneOrigin.ConfiguredSetting when original is { Length: > 0 }
                => new("runtime_resolved", original, null, zoneId, null),
            _ => new("invalid", original, null, null, null),
        };
    }

    /// <summary>
    /// Zona verificada de la instalación, o null. Es la misma regla que la
    /// pantalla, del lado del servidor: el navegador no es la garantía.
    /// </summary>
    internal static string? VerifiedZone(IInstallationTimeZoneResolver? resolver)
    {
        if (resolver is null) return null;
        var resolution = resolver.Resolve();
        return resolution is { IsResolved: true, Verification: InstallationTimeZoneVerification.Verified, ZoneId: { Length: > 0 } zoneId }
            ? zoneId
            : null;
    }

    /// <summary>Exige zona verificada y registra la exportación con la acción <c>export</c> de CORE.</summary>
    private static async Task<IResult> RecordExport(
        [FromBody] ExportAuditBody? body,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (body is not { DataRows: >= 0 } || string.IsNullOrEmpty(body.Zone))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["exportacion"] = ["Indica la zona del archivo y cuántas filas de datos lleva."]
            });
        }

        // Primero la zona: sin ella no se audita nada, para no dejar registros
        // de exportaciones que no podían salir.
        var zone = VerifiedZone(services.GetService(typeof(IInstallationTimeZoneResolver)) as IInstallationTimeZoneResolver);
        if (zone is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Para exportar, la zona horaria de la instalación tiene que estar verificada, y todavía no lo está.");
        }
        if (body.Zone != zone && body.Zone != "UTC")
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "La zona horaria pedida no es la de la instalación ni UTC, así que no se generó el archivo.");
        }

        if (services.GetService(typeof(IExportAudit)) is not IExportAudit audit)
        {
            return Unavailable();
        }

        var result = await audit.RecordAsync(
            new ExportAuditRequest(ReportsModule.ModuleCode, OrderIndicatorsExportKind, CsvFormat, body.DataRows),
            cancellationToken);

        // Recorded solo cuenta con el identificador que promete CORE: uuid v7. Sin
        // él no hay registro verificable, y un registro dudoso no es un registro.
        if (result is { IsRecorded: true, ExportId: { Version: 7 } exportId })
        {
            return Results.Ok(new ExportAuditDecisionView("recorded", exportId));
        }
        if (result.IsRecorded) return Unavailable();
        // Sin autor o con metadatos que CORE no admite: CORE se niega a registrarla.
        // Base caída o acción todavía no admitida por el CHECK: no está disponible.
        return result.Failure is ExportAuditFailure.ActorMissing or ExportAuditFailure.MetadataInvalid
            ? Results.Ok(new ExportAuditDecisionView("denied", null))
            : Unavailable();
    }

    private static IResult Unavailable() => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "No se pudo registrar la exportación en la auditoría, así que no se generó el archivo.",
        detail: "Vuelve a intentarlo; si se repite, avisa a quien administre el sistema.");
}

/// <summary>
/// Observación de la zona, con la forma que consume la pantalla. Cada estado
/// usa solo sus campos; los demás van nulos.
/// </summary>
public sealed record InstallationZoneObservationView(
    string Kind,
    string? Original,
    string? Canonical,
    string? RuntimeId,
    string? Zone);

/// <summary>Lo que la pantalla pide registrar: la zona del archivo y cuántas filas de datos lleva.</summary>
public sealed record ExportAuditBody(string? Zone, int? DataRows);

/// <summary>Decisión de la auditoría: <c>recorded</c>, con el identificador de CORE, o <c>denied</c>.</summary>
public sealed record ExportAuditDecisionView(string Decision, Guid? ExportId);
