using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;
using Sillar.Modules.Reports.Export;
using Sillar.Modules.Reports.Orders;

namespace Sillar.Modules.Reports.Endpoints;

/// <summary>
/// Exportación de indicadores de pedidos generada en el servidor: instantánea →
/// archivo preparado → registro durable en la auditoría de CORE → bytes.
/// </summary>
/// <remarks>
/// <para>
/// <b>No está montada.</b> <see cref="ReportsModule.MapEndpoints"/> no la llama y
/// la extensión es interna, así que ni el host puede activarla sin cambiar M10.
/// La descarga positiva sigue sin habilitar: CORE no produce zonas verificadas y
/// la migración <c>CoreAuditExport</c> está diferida.
/// </para>
/// <para>
/// El registro es de una exportación <b>autorizada y preparada</b>, no de una
/// descarga: que la respuesta se envíe no prueba que el archivo llegara. Si algo
/// falla después de registrar, queda un registro sin archivo; nunca un archivo
/// sin registro (<see cref="IExportAudit"/>).
/// </para>
/// </remarks>
internal static class OrderIndicatorsExportEndpoints
{
    /// <summary>Ruta de la exportación, cuando se monte.</summary>
    public const string Route = "/orders/export";

    /// <summary>Cabecera con el identificador que CORE dio al registro.</summary>
    public const string ExportIdHeader = "X-Export-Id";

    /// <summary>Monta la exportación. Solo la llaman las pruebas.</summary>
    public static IEndpointRouteBuilder MapOrderIndicatorsCsvExport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/admin/reporting")
            .WithTags("Reportes — Exportación")
            .RequireAuthorization(AdminRole.Admin)
            .MapPost(Route, Export)
            .WithName("ReportingAdminOrderIndicatorsCsvExport")
            .WithSummary("Genera en el servidor el CSV de indicadores de pedidos y lo entrega solo después de registrarlo en la auditoría de CORE.")
            .WithDescription("Exige zona de la instalación verificada; la zona del archivo es esa o UTC. El número de filas auditado sale del archivo. Sin registro confirmado no se envía ningún byte del archivo.")
            .AddEndpointFilter<CsrfEndpointFilter>()
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .Produces(StatusCodes.Status401Unauthorized);
        return endpoints;
    }

    private static async Task<IResult> Export(
        [FromBody] CsvExportBody? body,
        HttpContext http,
        IOrderIndicatorsReport report,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrEmpty(body.Zone))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["exportacion"] = ["Indica el período y la zona horaria del archivo."]
            });
        }
        if (!ReportPeriod.TryRead(body.StartInclusive, body.EndExclusive, out var start, out var end, out var problem))
        {
            return problem!;
        }

        // 1. Zona: verificada por CORE, y la del archivo es esa o UTC.
        var resolver = http.RequestServices.GetService(typeof(IInstallationTimeZoneResolver)) as IInstallationTimeZoneResolver;
        if (CoreSeamEndpoints.VerifiedZoneInfo(resolver) is not { } verified)
        {
            return Conflict("Para exportar, la zona horaria de la instalación tiene que estar verificada, y todavía no lo está.");
        }
        if (body.Zone != verified.Id && body.Zone != "UTC")
        {
            return Conflict("La zona horaria pedida no es la de la instalación ni UTC, así que no se generó el archivo.");
        }
        var fileZone = body.Zone == verified.Id ? verified.Zone : TimeZoneInfo.Utc;

        // 2. Instantánea: una sola lectura de ventas.
        var logger = loggerFactory.CreateLogger(typeof(OrderIndicatorsExportEndpoints));
        OrderIndicatorsResult data;
        try
        {
            data = await report.ReadAsync(start, end, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "La lectura para exportar falló para [{Start}, {End}).", start, end);
            return Unavailable("El módulo de ventas no respondió, así que no se generó el archivo.");
        }
        if (data.SourceStatus == OrderIndicatorsSourceStatus.SourceUnavailable)
        {
            return Conflict("El módulo de ventas no está activo, así que no hay indicadores que exportar.");
        }

        // 3. Archivo preparado, con sus filas contadas de lo escrito.
        OrderIndicatorsCsv.Prepared file;
        try
        {
            file = OrderIndicatorsCsv.Build(data, body.Zone, fileZone);
        }
        catch (SourceIncoherentException exception)
        {
            logger.LogError(exception, "Indicadores incoherentes al exportar [{Start}, {End}).", start, end);
            return Unavailable("Los indicadores de ventas no cuadran entre sí, así que no se generó el archivo.");
        }

        // Si la zona cambió mientras se leía, no se audita lo que ya no vale.
        if (!StillVerified(resolver, verified.Id))
        {
            return ZoneChanged();
        }

        // 4. Registro durable antes de cualquier byte.
        if (http.RequestServices.GetService(typeof(IExportAudit)) is not IExportAudit audit)
        {
            return Unavailable("No se pudo registrar la exportación en la auditoría, así que no se generó el archivo.");
        }
        var result = await audit.RecordAsync(
            new ExportAuditRequest(ReportsModule.ModuleCode, CoreSeamEndpoints.OrderIndicatorsExportKind, CoreSeamEndpoints.CsvFormat, file.DataRows),
            cancellationToken);
        if (result is not { IsRecorded: true, ExportId: { Version: 7 } exportId })
        {
            return result is { IsRecorded: false, Failure: ExportAuditFailure.ActorMissing or ExportAuditFailure.MetadataInvalid }
                ? Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "La auditoría no aceptó registrar esta exportación a tu nombre, así que no se generó el archivo.",
                    detail: "Vuelve a iniciar sesión e inténtalo de nuevo.")
                : Unavailable("No se pudo registrar la exportación en la auditoría, así que no se generó el archivo.");
        }

        // Cambió durante el registro: queda un registro sin archivo, que es lo que
        // admite la auditoría; un archivo en una zona que ya no vale, no.
        if (!StillVerified(resolver, verified.Id))
        {
            return ZoneChanged();
        }
        cancellationToken.ThrowIfCancellationRequested();

        // 5. Bytes.
        http.Response.Headers[ExportIdHeader] = exportId.ToString();
        return Results.File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

    private static bool StillVerified(IInstallationTimeZoneResolver? resolver, string zoneId)
        => CoreSeamEndpoints.VerifiedZoneInfo(resolver)?.Id == zoneId;

    private static IResult ZoneChanged() => Conflict(
        "La zona horaria de la instalación cambió mientras se preparaba la exportación, así que no se entregó el archivo. Vuelve a consultar y exporta de nuevo.");

    private static IResult Conflict(string title)
        => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: title);

    private static IResult Unavailable(string title) => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: title,
        detail: "Vuelve a intentarlo; si se repite, avisa a quien administre el sistema.");
}

/// <summary>Lo que se pide exportar: el período y la zona del archivo. Las filas no se piden: salen del archivo.</summary>
public sealed record CsvExportBody(string? StartInclusive, string? EndExclusive, string? Zone);
