using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Core.Dtos;
using Sillar.Shared.Platform;

namespace Sillar.Core.Endpoints;

/// <summary>Endpoint público de capacidades.</summary>
public static class CapabilitiesEndpoints
{
    /// <summary>Monta <c>GET /api/capabilities</c>.</summary>
    public static IEndpointRouteBuilder MapCapabilitiesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/capabilities", GetCapabilities)
            .WithName("GetCapabilities")
            .WithTags("Capacidades")
            .WithSummary("Producto, versión y módulos activos de esta instalación.")
            .WithDescription(
                "Público y sin sesión: el frontend lo consulta antes de pintar nada para saber " +
                "qué rutas montar y qué secciones mostrar. No expone información de licencia.")
            .Produces<CapabilitiesResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    /// <summary>
    /// Devuelve el producto, su versión y la lista de módulos activos.
    /// </summary>
    /// <param name="registry">Registro de módulos activos de la instalación.</param>
    /// <returns>Capacidades de la instalación.</returns>
    private static CapabilitiesResponse GetCapabilities(IModuleRegistry registry) => new(
        SillarProduct.Name,
        SillarProduct.Version,
        [.. registry.GetActive().Select(module => new ModuleCapability(module.Code, module.Version))]);
}
