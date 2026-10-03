using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Dtos;
using Sillar.Modules.Sales.Pedidos;

namespace Sillar.Modules.Sales.Endpoints;

/// <summary>Los pedidos del cliente autenticado.</summary>
/// <remarks>
/// <para>
/// <b>Solo lectura, y solo de lo congelado en <c>sales</c>.</b> Ninguna de las dos
/// rutas consulta el catálogo ni CRM: el nombre del producto, su variante y su precio
/// se copiaron al comprar, así que el pedido se lee igual aunque M01 cambie o se
/// desinstale.
/// </para>
/// <para>
/// <b>Sesión de cliente obligatoria en las dos.</b> Comprar exige cuenta, y por eso la
/// dependencia sobre M04 es dura; consultar lo propio exige la misma cuenta.
/// </para>
/// </remarks>
public static class PedidosDelClienteEndpoints
{
    private const string Prefix = "/api/sales/my-orders";
    private const string Tag = "Pedidos del cliente";

    /// <summary>Cuántos pedidos devuelve la lista por defecto.</summary>
    private const int PorDefecto = 20;

    /// <summary>Mapea las rutas de pedidos propios.</summary>
    public static IEndpointRouteBuilder MapPedidosDelClienteEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var propios = endpoints.MapGroup(Prefix)
            .WithTags(Tag)
            .RequireAuthorization(CustomerAuthorization.PolicyName);

        // La creación va en su propio grupo porque es la única que escribe, y por
        // tanto la única que necesita CSRF: toda petición que modifique datos lo
        // exige.
        var creacion = endpoints.MapGroup("/api/sales/orders")
            .WithTags(Tag)
            .RequireAuthorization(CustomerAuthorization.PolicyName)
            .AddEndpointFilter<CustomerCsrfEndpointFilter>();

        creacion.MapPost("", (Delegate)CrearPedido)
            .WithName("SalesOrderCreate")
            .WithSummary("Crea un pedido de recojo en tienda con las líneas indicadas.")
            .WithDescription(
                "El cliente envía variantes y cantidades; nunca precios. El precio de " +
                "cada línea y el total los resuelve el servidor contra M01 en el " +
                "momento de confirmar. Un producto cuyo precio es «a consultar» no " +
                "entra: se cotiza.")
            .Produces<PedidoCreadoRespuesta>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        propios.MapGet("", (Delegate)ListarPropios)
            .WithName("SalesMyOrdersList")
            .WithSummary("Devuelve los pedidos propios, del más reciente al más antiguo.")
            .WithDescription(
                "Solo datos congelados en el momento de la compra. Una lista vacía " +
                "significa que este cliente no tiene pedidos, que es un caso normal.")
            .Produces<IReadOnlyList<CustomerOrderSummary>>(StatusCodes.Status200OK);

        propios.MapGet("/{orderCode}", (Delegate)ObtenerPropio)
            .WithName("SalesMyOrderDetail")
            .WithSummary("Devuelve el detalle de un pedido propio por su código visible.")
            .WithDescription(
                "Responde 404 —y no 403— cuando el código existe pero es de otro " +
                "cliente: distinguirlos convertiría la ruta en un detector de códigos " +
                "de pedido válidos.")
            .Produces<PedidoPropioDetalle>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Crea un pedido para el cliente de la sesión.</summary>
    /// <remarks>
    /// <b>409 y no 400 para los conflictos de negocio.</b> Un producto que se cotiza o
    /// una variante que dejó de venderse no son errores de forma de la petición: la
    /// petición era correcta cuando el cliente la compuso, y el estado del mundo
    /// cambió. Y la frase dice <b>qué lo impide y qué hacer</b>, sin ningún «ha
    /// ocurrido un error».
    /// </remarks>
    private static async Task<IResult> CrearPedido(
        CrearPedidoPeticion peticion,
        ICurrentCustomer current,
        CreadorDePedidos creador,
        CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var r = await creador.CrearAsync(customerId, peticion.Lines ?? [], cancellationToken);

        if (r.Creado)
        {
            return Results.Created(
                $"/api/sales/my-orders/{r.OrderCode}",
                new PedidoCreadoRespuesta(
                    r.OrderCode!, OrderStatus.PendingPayment, r.TotalAmount, r.PaymentDueAt!.Value));
        }

        return r.Motivo switch
        {
            MotivoDeNoCreacion.SinLineas => Results.Problem(
                title: "El carrito está vacío",
                detail: "Añade algún producto antes de confirmar el pedido.",
                statusCode: StatusCodes.Status400BadRequest),

            MotivoDeNoCreacion.CantidadNoPositiva => Results.Problem(
                title: "Hay una cantidad que no se puede pedir",
                detail: "Indica al menos una unidad de cada producto.",
                statusCode: StatusCodes.Status400BadRequest),

            MotivoDeNoCreacion.CorreoSinVerificar => Results.Problem(
                title: "Falta verificar tu correo",
                detail:
                    "Te enviamos un correo para confirmar tu dirección. Verifícala y " +
                    "vuelve a intentarlo: así podemos avisarte del estado de tu pedido.",
                statusCode: StatusCodes.Status409Conflict),

            MotivoDeNoCreacion.LaCuentaNoPuedeComprar => Results.Problem(
                // M04 devuelve null sin distinguir el motivo, a propósito. No se
                // nombra una causa que nadie garantiza.
                title: "Esta cuenta no puede completar pedidos ahora mismo",
                detail: "Escríbenos y lo revisamos contigo.",
                statusCode: StatusCodes.Status409Conflict),

            MotivoDeNoCreacion.ItemNoVendible => Results.Problem(
                title: "Un producto de tu carrito dejó de estar disponible",
                detail: "Quítalo del carrito y vuelve a confirmar.",
                statusCode: StatusCodes.Status409Conflict),

            MotivoDeNoCreacion.ItemAConsultar => Results.Problem(
                title: "Hay un producto que se cotiza",
                detail:
                    "Ese producto no tiene precio publicado. Pide información y te " +
                    "decimos el precio.",
                statusCode: StatusCodes.Status409Conflict),

            _ => Results.Problem(statusCode: StatusCodes.Status409Conflict)
        };
    }

    /// <summary>Los pedidos del cliente de la sesión.</summary>
    private static async Task<IResult> ListarPropios(
        ICurrentCustomer current,
        SalesDbContext database,
        CancellationToken cancellationToken,
        int? limit = null)
    {
        // La política ya exigió sesión; si no hubiera, no se inventa un cliente.
        if (current.CustomerId is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var pedidos = await HistorialDePedidos
            .Consulta(database, customerId)
            .Take(Math.Clamp(limit ?? PorDefecto, 1, PorDefecto))
            .ToListAsync(cancellationToken);

        return Results.Ok(pedidos);
    }

    /// <summary>El detalle de un pedido propio.</summary>
    /// <remarks>
    /// <b>El filtro por cliente va en la consulta, no después.</b> Traer el pedido y
    /// comprobar el dueño en memoria funcionaría igual hoy y dejaría la puerta
    /// abierta a que alguien devuelva antes de comprobar. Así el pedido de otro
    /// cliente <b>no existe</b> para esta ruta, y el 404 no es una decisión: es lo que
    /// la consulta encontró.
    /// </remarks>
    private static async Task<IResult> ObtenerPropio(
        string orderCode,
        ICurrentCustomer current,
        SalesDbContext database,
        CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var pedido = await database.Orders
            .AsNoTracking()
            .Where(o => o.OrderCode == orderCode
                        && o.CustomerId == customerId
                        && o.IsActive)
            .Select(o => new
            {
                o.OrderId,
                o.OrderCode,
                o.Status,
                o.TotalAmount,
                o.CreatedAt,
                o.PaymentDueAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (pedido is null)
        {
            // 404 y no 403, también cuando el código existe y es de otro: el motivo
            // está en el WithDescription de arriba y no se repite aquí.
            return Results.NotFound();
        }

        var lineas = await database.OrderLines
            .AsNoTracking()
            .Where(l => l.OrderId == pedido.OrderId)
            .OrderBy(l => l.ProductName)
            .ThenBy(l => l.OrderLineId)
            .Select(l => new LineaDePedidoPropio(
                l.ProductName,
                l.VariantValue,
                l.SaleUnit,
                l.Quantity,
                l.UnitPrice,
                l.UnitPrice * l.Quantity))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PedidoPropioDetalle(
            pedido.OrderCode,
            pedido.Status,
            pedido.TotalAmount,
            pedido.CreatedAt,
            pedido.PaymentDueAt,
            lineas));
    }
}
