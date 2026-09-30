using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Modules.B2B.Solicitudes;
using Sillar.Modules.Crm.Contracts;

namespace Sillar.Modules.B2B.Endpoints;

/// <summary>
/// Las rutas públicas de M07. <b>Todas con sesión de cliente de M04, ninguna
/// anónima</b> (SPEC §6, regla 1; confirmado por JP el 27/09). No hay
/// autenticación propia: la política y el CSRF son los del contrato de M04.
/// </summary>
public static class SolicitudesClienteEndpoints
{
    private const string Tag = "Solicitudes B2B — Cliente";

    /// <summary>Monta las cuatro rutas de cliente.</summary>
    public static IEndpointRouteBuilder MapSolicitudesClienteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var cliente = endpoints.MapGroup("/api/b2b")
            .WithTags(Tag)
            .RequireAuthorization(CustomerAuthorization.PolicyName)
            .AddEndpointFilter<CustomerCsrfEndpointFilter>();

        cliente.MapPost("/special-orders", CrearPersonalizacion)
            .WithName("B2bCreateSpecialOrder")
            .WithSummary("Pide un cambio sobre un producto de la tienda.")
            .WithDescription("El producto tiene que estar activo y publicado. El cupo por cuenta es común a los dos tipos de solicitud.")
            .Produces<SolicitudPropiaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        cliente.MapPost("/institution-requests", CrearVolumen)
            .WithName("B2bCreateInstitutionRequest")
            .WithSummary("Pide una cantidad que cambia el precio, esté o no en la tienda.")
            .Produces<SolicitudPropiaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        cliente.MapGet("/my-requests", ListarPropias)
            .WithName("B2bListMyRequests")
            .WithSummary("Las solicitudes propias, de los dos tipos, más recientes primero.")
            .Produces<IReadOnlyList<SolicitudPropiaResponse>>();

        cliente.MapGet("/quotes/{quoteNumber}", ObtenerCotizacion)
            .WithName("B2bGetMyQuote")
            .WithSummary("El detalle de una cotización propia, por su número.")
            .WithDescription("404 tanto si no existe como si es de otro cliente: la ruta no revela qué números existen.")
            .Produces<CotizacionPropiaResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Crea una solicitud de personalización para la cuenta de la sesión.</summary>
    private static async Task<IResult> CrearPersonalizacion(
        CrearPersonalizacionRequest request, SolicitudesService servicio, ICurrentCustomer cliente,
        HttpContext http, CancellationToken ct)
        => Responder(await servicio.CrearPersonalizacionAsync(cliente.CustomerId!.Value, request, ct), http);

    /// <summary>Crea una solicitud de volumen para la cuenta de la sesión.</summary>
    private static async Task<IResult> CrearVolumen(
        CrearVolumenRequest request, SolicitudesService servicio, ICurrentCustomer cliente,
        HttpContext http, CancellationToken ct)
        => Responder(await servicio.CrearVolumenAsync(cliente.CustomerId!.Value, request, ct), http);

    /// <summary>Lista las solicitudes de la cuenta de la sesión.</summary>
    private static async Task<IResult> ListarPropias(SolicitudesService servicio, ICurrentCustomer cliente, CancellationToken ct)
        => Results.Ok(await servicio.ListarPropiasAsync(cliente.CustomerId!.Value, ct));

    /// <summary>Lee una cotización de la cuenta de la sesión.</summary>
    private static async Task<IResult> ObtenerCotizacion(
        string quoteNumber, SolicitudesService servicio, ICurrentCustomer cliente, CancellationToken ct)
        => await servicio.ObtenerCotizacionPropiaAsync(cliente.CustomerId!.Value, quoteNumber, ct) is { } cotizacion
            ? Results.Ok(cotizacion)
            : Results.NotFound();

    // El identificador del cliente sale SIEMPRE de la sesión: ningún pedido
    // trae un customerId que se lea (plan de pruebas 1.10).
    private static IResult Responder(OperacionDeSolicitud operacion, HttpContext http)
    {
        switch (operacion.Resultado)
        {
            case ResultadoSolicitud.Creada:
                return Results.Created("/api/b2b/my-requests", operacion.Solicitud);
            case ResultadoSolicitud.Limitada:
                if (operacion.Esperar is { } esperar)
                {
                    http.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(esperar.TotalSeconds)).ToString();
                }
                return Results.Problem(title: operacion.Motivo, statusCode: StatusCodes.Status429TooManyRequests);
            case ResultadoSolicitud.ProductoNoDisponible:
                return Results.Problem(title: operacion.Motivo, statusCode: StatusCodes.Status409Conflict);
            default:
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["solicitud"] = [operacion.Motivo!] });
        }
    }
}
