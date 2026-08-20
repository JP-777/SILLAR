using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Cms.Data;
using Sillar.Modules.Cms.Endpoints;
using Sillar.Modules.Cms.Services;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.Cms;

/// <summary>M02 — Contenido Web.</summary>
public sealed class CmsModule : IModule
{
    public const string ModuleCode = "cms";
    public const string ConnectionStringName = "Default";
    public string Code => ModuleCode;
    public string DisplayName => "Contenido Web";
    public string Description =>
        "Banners, promociones, productos destacados, trabajos y redes sociales para una web administrable.";
    public string Version => "1.0.0";
    public int DisplayOrder => 20;
    public string[] HardDependencies => ["core"];
    public string[] SoftDependencies => ["catalog"];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringName}'.");

        services.AddDbContext<CmsDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(
                CmsDbContext.MigrationsHistoryTable,
                CmsDbContext.Schema)));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CmsOrderService>();
        services.AddScoped<BannerService>();
        services.AddScoped<PromotionService>();
        services.AddScoped<FeaturedProductService>();
        services.AddScoped<FeaturedProjectService>();
        services.AddScoped<SocialLinkService>();
    }

    /// <summary>Monta las rutas públicas y administrativas de CMS.</summary>
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapBannerEndpoints();
        endpoints.MapPromotionEndpoints();
        endpoints.MapFeaturedProductEndpoints();
        endpoints.MapFeaturedProjectEndpoints();
        endpoints.MapSocialLinkEndpoints();
    }
}
