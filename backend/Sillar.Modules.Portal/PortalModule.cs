using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Portal.Application;
using Sillar.Modules.Portal.Endpoints;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.Portal;

/// <summary>Portal privado de solo lectura para clientes de M04.</summary>
public sealed class PortalModule : IModule
{
    public string Code => "portal";
    public string? Schema => null;
    public string DisplayName => "Portal del Cliente";
    public string Description => "Cuenta, pedidos y seguimiento privado de trabajos propios.";
    public string Version => "1.0.0";
    public int DisplayOrder => 80;
    public string[] HardDependencies => ["core", "crm"];
    public string[] SoftDependencies => ["sales", "tracking"];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<PortalReader>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapPortalEndpoints();
}
