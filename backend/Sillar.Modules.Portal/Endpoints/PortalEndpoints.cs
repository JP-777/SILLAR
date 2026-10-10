using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Portal.Application;

namespace Sillar.Modules.Portal.Endpoints;

internal static class PortalEndpoints
{
    internal static IEndpointRouteBuilder MapPortalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Se exige exclusivamente el esquema de cliente M04; jamás el administrativo.
        var group = endpoints.MapGroup("/api/portal")
            .WithTags("Portal del Cliente")
            .RequireAuthorization(CustomerAuthorization.PolicyName);

        group.MapGet("/overview", GetOverview)
            .WithName("PortalCustomerOverview")
            .WithSummary("Cuenta, pedidos y trabajos del cliente autenticado.")
            .Produces<PortalOverview>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/work/{visibleCode}", GetWork)
            .WithName("PortalCustomerWorkDetail")
            .WithSummary("Detalle público de un trabajo propio; orden ajena e inexistente devuelven 404.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> GetOverview(
        PortalReader portal, CancellationToken cancellationToken, int? limit = null)
    {
        var response = await portal.GetOverviewAsync(limit, cancellationToken);
        return response is null ? Results.Unauthorized() : Results.Ok(response);
    }

    private static async Task<IResult> GetWork(
        string visibleCode, PortalReader portal, CancellationToken cancellationToken)
    {
        var result = await portal.GetWorkAsync(visibleCode, cancellationToken);
        return result.Outcome switch
        {
            PortalWorkOutcome.Found => Results.Ok(result.Detail),
            PortalWorkOutcome.NotFound => Results.NotFound(),
            PortalWorkOutcome.Unauthorized => Results.Unauthorized(),
            PortalWorkOutcome.Unavailable => Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
            _ => Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
        };
    }
}
