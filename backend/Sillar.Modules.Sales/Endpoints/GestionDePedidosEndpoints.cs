using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Sales.Dtos;
using Sillar.Modules.Sales.Pedidos;

namespace Sillar.Modules.Sales.Endpoints;

internal static class GestionDePedidosEndpoints
{
    public static void MapGestionDePedidosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/sales/orders").WithTags("Gestión de pedidos")
            .RequireAuthorization(AdminRole.Editor).AddEndpointFilter<CsrfEndpointFilter>();
        group.MapGet("", async (string? status, bool? attention, GestionDePedidos service, CancellationToken ct)
            => Results.Ok(await service.ListarAsync(status, attention ?? false, ct)));
        group.MapGet("/{code}", async (string code, GestionDePedidos service, CancellationToken ct)
            => await service.ObtenerAsync(code, ct) is { } detail ? Results.Ok(detail) : Results.NotFound());
        group.MapPost("/{code}/payments", async (string code, RegistrarPagoManual input, GestionDePedidos service, CancellationToken ct)
            => Responder(await service.RegistrarPagoAsync(code, input, ct)));
        group.MapPost("/{code}/transitions", async (string code, CambiarPedido input, GestionDePedidos service, CancellationToken ct)
            => Responder(await service.CambiarAsync(code, input, ct)));
    }
    private static IResult Responder(ResultadoDeGestion result) => result.Success ? Results.Ok(result)
        : Results.Problem(statusCode: 409, title: "No se pudo completar la operación", detail: result.Error);
}
