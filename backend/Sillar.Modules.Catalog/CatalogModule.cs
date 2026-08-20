using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Modules.Catalog.Data;
using Sillar.Modules.Catalog.Endpoints;
using Sillar.Modules.Catalog.Services;
using Sillar.Shared.Modularity;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Catalog;

/// <summary>
/// M01 — Catálogo de Productos.
/// </summary>
/// <remarks>
/// El primer módulo del árbol comercial: sin él no hay tienda, ni punto de
/// venta, ni inventario. No se vende ni se cuenta lo que no está catalogado.
///
/// Vendible solo, como catálogo de exhibición sin venta.
/// </remarks>
public sealed class CatalogModule : IModule
{
    /// <summary>Código del módulo, y nombre de su schema.</summary>
    public const string ModuleCode = "catalog";

    /// <summary>Nombre de la cadena de conexión en la configuración.</summary>
    public const string ConnectionStringName = "Default";

    /// <inheritdoc />
    public string Code => ModuleCode;

    /// <inheritdoc />
    public string DisplayName => "Catálogo de Productos";

    /// <inheritdoc />
    public string Description =>
        "Categorías, productos, variantes, marcas e imágenes. Es la base de todo lo comercial: " +
        "no se vende ni se cuenta lo que no está catalogado.";

    /// <inheritdoc />
    public string Version => "1.1.0";

    /// <inheritdoc />
    public int DisplayOrder => 10;

    /// <inheritdoc />
    /// <remarks>
    /// Solo CORE: autenticación, auditoría, <c>core.media_assets</c> para las
    /// imágenes y las colaciones compartidas. M01 no depende de nada más, y es
    /// deliberado: todo lo demás cuelga de él.
    /// </remarks>
    public string[] HardDependencies => ["core"];

    /// <inheritdoc />
    public string[] SoftDependencies => [];

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringName}'.");

        // El nodo de origen de las filas replicadas. CORE también lo registra
        // (ADR-018: media_assets); TryAddNodeIdentity evita que se registre dos
        // veces, sea cual sea el orden en que arranquen los módulos.
        services.TryAddNodeIdentity(configuration);

        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(
                CatalogDbContext.MigrationsHistoryTable,
                CatalogDbContext.Schema)));

        services.AddScoped<CategoryService>();
        services.AddScoped<BrandService>();
        services.AddScoped<ProductService>();
        services.AddScoped<ProductItemService>();
        services.AddScoped<ProductImageService>();

        // El contrato público (SPEC §7): lo que M03, M09, M13 y M15 ven de M01,
        // sin conocer su schema. Mismo patrón que IMediaStorage en CORE.
        services.AddScoped<CatalogService>();
        services.AddScoped<ICatalogService>(provider => provider.GetRequiredService<CatalogService>());
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCategoryEndpoints();
        endpoints.MapBrandEndpoints();
        endpoints.MapProductEndpoints();
        endpoints.MapProductItemEndpoints();
    }
}
