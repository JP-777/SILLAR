using Sillar.Shared.Data.Modularity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Catalog.Contracts.Events;
using Sillar.Modules.Cms.Data;
using Sillar.Modules.Cms.Endpoints;
using Sillar.Modules.Cms.Events;
using Sillar.Modules.Cms.Services;
using Sillar.Shared.Events;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.Cms;

/// <summary>M02 — Contenido Web.</summary>
public sealed class CmsModule : IModule, IModuleMigrations
{
    public const string ModuleCode = "cms";
    public const string ConnectionStringName = "Default";
    public string Code => ModuleCode;

    /// <summary>
    /// Sus migraciones, para que el instalador las aplique sin conocer este
    /// módulo. El contexto se construye aquí, con la misma configuración que
    /// en tiempo de ejecución.
    /// </summary>
    private static readonly MigracionesDeContexto<CmsDbContext> Migraciones = new(
        connectionString => new CmsDbContext(
            PersistenciaDeModulo.Opciones<CmsDbContext>(
                connectionString, CmsDbContext.Schema, CmsDbContext.MigrationsHistoryTable)),
        CmsDbContext.MigrationsHistoryTable);

    /// <inheritdoc />
    public string MigrationsHistoryTable => Migraciones.MigrationsHistoryTable;

    /// <inheritdoc />
    public IReadOnlyList<string> KnownMigrations(string connectionString)
        => Migraciones.KnownMigrations(connectionString);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> AppliedMigrationsAsync(string connectionString, CancellationToken cancellationToken)
        => Migraciones.AppliedMigrationsAsync(connectionString, cancellationToken);

    /// <inheritdoc />
    public Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken)
        => Migraciones.ApplyMigrationsAsync(connectionString, cancellationToken);
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

        // Por el mismo sitio que el instalador: ver PersistenciaDeModulo.
        services.AddDbContext<CmsDbContext>(options => PersistenciaDeModulo.Configurar(
            options, connectionString, CmsDbContext.Schema, CmsDbContext.MigrationsHistoryTable));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CmsOrderService>();
        services.AddScoped<BannerService>();
        services.AddScoped<PromotionService>();
        services.AddScoped<FeaturedProductService>();
        services.AddSingleton<FeaturedProductSnapshotCoordinator>();
        services.AddSingleton<IEventHandler<ProductoActualizado>, ProductoActualizadoHandler>();
        services.AddSingleton<IEventHandler<ProductoDesactivado>, ProductoDesactivadoHandler>();
        services.AddSingleton<IEventHandler<CategoriaDesactivada>, CategoriaDesactivadaHandler>();
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
