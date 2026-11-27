using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;

namespace Sillar.Modules.Reports.Endpoints;

/// <summary>
/// Costura de M10 con CORE: la zona de la instalación y la auditoría de la
/// exportación, leídas de los contratos candidatos de CORE.
/// </summary>
/// <remarks>
/// Los contratos son opcionales para una composición sin CORE; la composición
/// productiva registra sus productores persistentes. No inventa una zona cuando
/// no hay productor ni convierte un fallo de lectura en falta de datos.
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
            .WithDescription("kind: unconnected (CORE no publica la zona), approved_initial (zona inicial aprobada, sin verificar), runtime_resolved (configurada, sin verificar, caducada o revocada), verified o invalid. 503 si CORE no pudo resolverla.")
            .Produces<InstallationZoneObservationView>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // El servidor calcula las filas y audita en /orders/export. No se publica
        // un recibo de auditoría basado en cantidades aportadas por el cliente.

        return endpoints;
    }

    /// <summary>Lee la zona de CORE. Sin productor registrado, «sin conexión»; si CORE falla, 503.</summary>
    private static async Task<IResult> ReadZone(
        IServiceProvider services, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var read = await InstallationZoneGate.ReadAsync(services, loggerFactory, cancellationToken);
        return read.Failed ? InstallationZoneGate.Unreadable() : Results.Ok(Observe(read));
    }

    /// <summary>
    /// Traduce la resolución de CORE. Solo una zona que <see cref="InstallationZoneGate.Verified"/>
    /// acepta produce <c>verified</c>; la inicial, la configurada sin verificar, la
    /// caducada, la revocada y la que no se puede comprobar salen sin verificar.
    /// </summary>
    internal static InstallationZoneObservationView Observe(ZoneRead read)
    {
        if (read.Resolution is not { } resolution) return new("unconnected", null, null, null, null);

        var original = resolution.Setting?.OriginalValue;
        if (!resolution.IsResolved || resolution.ZoneId is not { Length: > 0 } zoneId)
        {
            return new("invalid", original, null, null, null);
        }
        if (InstallationZoneGate.Verified(resolution) is { } verified)
        {
            return new("verified", original ?? verified.Id, verified.Id, null, null);
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
    /// El identificador de un registro legítimo, o null. Legítimo es lo que promete
    /// CORE: registrado, sin causa de rechazo, uuid v7 y un actor con nombre, id, correo y
    /// nodo. Un registro dudoso no es un registro.
    /// </summary>
    internal static Guid? LegitimateRecord(ExportAuditResult? result)
        => result is
        {
            IsRecorded: true,
            Failure: null,
            ExportId: { Version: 7 } exportId,
            Actor: { AdminUserId: > 0, Email.Length: > 0 } actor,
        } && !string.IsNullOrWhiteSpace(actor.HomeNode) && !string.IsNullOrWhiteSpace(actor.DisplayName)
            ? exportId
            : null;

    private static IResult Unavailable() => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "No se pudo registrar la exportación en la auditoría, así que no se generó el archivo.",
        detail: "Vuelve a intentarlo; si se repite, avisa a quien administre el sistema.");
}

/// <summary>Lo que se leyó de CORE: la resolución, o que no hay productor, o que falló.</summary>
/// <param name="Resolution">La resolución; nula sin productor o si falló.</param>
/// <param name="Failed">CORE no pudo resolver la zona: no es «sin conexión» ni «sin verificar».</param>
internal sealed record ZoneRead(InstallationTimeZoneResolution? Resolution, bool Failed);

/// <summary>Una zona que M10 acepta como verificada, con la resolución de la que sale.</summary>
internal sealed record VerifiedInstallationZone(string Id, TimeZoneInfo Zone, InstallationTimeZoneResolution Resolution);

/// <summary>
/// Cuándo M10 da por verificada la zona de la instalación y cuándo deja de
/// valer. Es la misma regla para el registro y para el archivo.
/// </summary>
internal static class InstallationZoneGate
{
    /// <summary>
    /// Lee la zona. La cancelación del llamador se propaga; cualquier otro fallo del
    /// resolvedor es <see cref="ZoneRead.Failed"/>, nunca una zona.
    /// </summary>
    public static async Task<ZoneRead> ReadAsync(
        IServiceProvider services, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (services.GetService(typeof(IInstallationTimeZoneResolver)) is not IInstallationTimeZoneResolver resolver)
        {
            return new(null, false);
        }
        try
        {
            return new(await resolver.ResolveAsync(cancellationToken), false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            loggerFactory.CreateLogger(typeof(InstallationZoneGate)).LogError(exception, "CORE no pudo resolver la zona de la instalación.");
            return new(null, true);
        }
    }

    /// <summary>
    /// Verificada para M10: estado <see cref="TimeZoneVerificationStatus.Verified"/>,
    /// zona configurada (la inicial nunca se atesta) y con atestación. Que el
    /// runtime reconozca el identificador no basta; Unverified, Stale, Revoked y
    /// SourceNotVerifiable nunca pasan.
    /// </summary>
    public static VerifiedInstallationZone? Verified(InstallationTimeZoneResolution? resolution)
        => resolution is
        {
            Status: TimeZoneVerificationStatus.Verified,
            Origin: InstallationTimeZoneOrigin.ConfiguredSetting,
            Zone: { } zone,
            ZoneId: { Length: > 0 } zoneId,
            AttestationId: { } attestation,
        } && attestation != Guid.Empty
            ? new(zoneId, zone, resolution)
            : null;

    /// <summary>
    /// Sigue valiendo la zona aceptada antes. No basta el mismo <c>ZoneId</c>: tienen
    /// que coincidir el estado, el motivo, el origen, la atestación y lo leído de la
    /// configuración, y la observación no puede ser anterior a la primera.
    /// </summary>
    public static bool StillSame(VerifiedInstallationZone earlier, InstallationTimeZoneResolution? later)
    {
        if (Verified(later) is not { } now) return false;
        var before = earlier.Resolution;
        var after = now.Resolution;
        return now.Id == earlier.Id
            && after.Status == before.Status
            && after.Reason == before.Reason
            && after.Origin == before.Origin
            && after.AttestationId == before.AttestationId
            && Equals(after.Setting, before.Setting)
            && after.ObservedAt >= before.ObservedAt;
    }

    public static IResult NotVerified() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Para exportar, la zona horaria de la instalación tiene que estar verificada, y todavía no lo está.");

    public static IResult Changed() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "La zona horaria de la instalación cambió mientras se preparaba la exportación, así que no se entregó el archivo. Vuelve a consultar y exporta de nuevo.");

    public static IResult Unreadable() => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "No se pudo leer la zona horaria de la instalación, así que no se generó el archivo.",
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
