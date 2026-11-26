using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Reports.Endpoints;
using Sillar.Modules.Reports.Orders;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.Reports;

/// <summary>M10 — Reportes; solo lectura, sin schema ni migraciones.</summary>
public sealed class ReportsModule : IModule
{
    /// <summary>Código público del módulo.</summary>
    public const string ModuleCode = "reporting";

    /// <inheritdoc />
    public string Code => ModuleCode;

    /// <inheritdoc />
    public string? Schema => null;

    /// <inheritdoc />
    public string DisplayName => "Reportes";

    /// <inheritdoc />
    public string Description => "Indicadores agregados del negocio, sin exponer datos personales.";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public int DisplayOrder => 100;

    /// <inheritdoc />
    public string[] HardDependencies => ["core"];

    /// <inheritdoc />
    public string[] SoftDependencies => ["sales"];

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
        => services.AddOrderIndicatorsReport();

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOrderIndicatorsEndpoints();
        endpoints.MapMonetaryIndicatorsEndpoints();
        endpoints.MapCoreSeamEndpoints();
    }
}
