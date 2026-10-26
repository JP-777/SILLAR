using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;
using Sillar.Modules.Reports.Orders;

namespace Sillar.Modules.Reports.Endpoints;

/// <summary>
/// Indicadores con importes: I02, valor de pedidos. Cada uno en su propia ruta,
/// para que el fallo de un lector no apague a los demás.
/// </summary>
public static class MonetaryIndicatorsEndpoints
{
    /// <summary>Registra las lecturas monetarias, solo cuando M10 está activo.</summary>
    public static IEndpointRouteBuilder MapMonetaryIndicatorsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/reporting")
            .WithTags("Reportes — Importes")
            .RequireAuthorization(AdminRole.Admin);

        admin.MapGet("/orders/value", ReadOrderValue)
            .WithName("ReportingAdminOrderValue")
            .WithSummary("I02: valor congelado de los pedidos creados entre inicio incluido y fin excluido, una suma por moneda.")
            .WithDescription("No es un cobro ni un ingreso. Nunca suma monedas distintas; los pedidos sin moneda se cuentan aparte y no se suman. status: source_unavailable, not_measurable, empty, currency_not_accredited o with_data.")
            .Produces<OrderValueView>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    /// <summary>Lee I02 de la cohorte [start, end).</summary>
    private static async Task<IResult> ReadOrderValue(
        HttpRequest request,
        IServiceProvider services,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!ReportPeriod.TryRead(request.Query["start"].ToString(), request.Query["end"].ToString(),
                out var start, out var end, out var problem))
        {
            return problem!;
        }

        var report = new OrderValueReport(services);
        return await Guarded(() => report.ReadAsync(start, end, cancellationToken),
            loggerFactory, "valor de pedidos", start, end, cancellationToken);
    }

    /// <summary>
    /// Un fallo de ventas es un 503 con su propia frase; una respuesta que no
    /// cuadra, otro. Ninguno se convierte en 0 ni en «no disponible». Una
    /// cancelación del llamador se propaga.
    /// </summary>
    internal static async Task<IResult> Guarded<T>(Func<Task<T>> read, ILoggerFactory loggerFactory,
        string what, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(MonetaryIndicatorsEndpoints));
        try
        {
            return Results.Ok(await read());
        }
        catch (SourceIncoherentException exception)
        {
            logger.LogError(exception, "Ventas respondió {What} incoherente para [{Start}, {End}).", what, start, end);
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "El módulo de ventas respondió con cifras que no cuadran, así que no se muestra ninguna.",
                detail: "Esto no significa cero. Vuelve a consultar; si se repite, avisa a quien administre el sistema.");
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "La lectura de {What} falló para [{Start}, {End}).", what, start, end);
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "El módulo de ventas no respondió, así que no se muestra ninguna cifra.",
                detail: "Esto no significa cero. Vuelve a consultar en unos minutos; si se repite, revisa el registro del servidor.");
        }
    }
}
