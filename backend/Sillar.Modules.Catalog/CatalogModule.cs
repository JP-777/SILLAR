using Sillar.Shared.Data.Modularity;
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
public sealed class CatalogModule : IModule, IModuleMigrations
{
    /// <summary>Código del módulo, y nombre de su schema.</summary>
    public const string ModuleCode = "catalog";

    /// <summary>Nombre de la cadena de conexión en la configuración.</summary>
    public const string ConnectionStringName = "Default";

    /// <inheritdoc />
    public string Code => ModuleCode;

    /// <summary>
    /// Sus migraciones, para que el instalador las aplique sin conocer este
    /// módulo. El contexto se construye aquí, con la misma configuración que
    /// en tiempo de ejecución; el nodo y el reloj no intervienen al migrar.
    /// </summary>
    private static readonly MigracionesDeContexto<CatalogDbContext> Migraciones = new(
        connectionString => new CatalogDbContext(
            PersistenciaDeModulo.Opciones<CatalogDbContext>(
                connectionString, CatalogDbContext.Schema, CatalogDbContext.MigrationsHistoryTable),
            new NodeIdentity(NodeIdentity.DefaultCode),
            TimeProvider.System),
        CatalogDbContext.MigrationsHistoryTable);

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

        // Por el mismo sitio que el instalador: ver PersistenciaDeModulo.
        services.AddDbContext<CatalogDbContext>(options => PersistenciaDeModulo.Configurar(
            options, connectionString, CatalogDbContext.Schema, CatalogDbContext.MigrationsHistoryTable));

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
