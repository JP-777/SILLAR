using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Catalog.Domain;
using Sillar.Shared.Data.Replication;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Catalog.Data;

/// <summary>
/// Contexto de datos de M01. Solo escribe en el schema <c>catalog</c>.
/// </summary>
/// <remarks>
/// Las claves foráneas hacia <c>core.media_assets</c> son la única salida de
/// este schema, y están permitidas porque CORE es dependencia dura. Ninguna
/// entidad de aquí mapea una tabla de otro módulo: para leer datos ajenos se
/// pide su contrato al contenedor.
/// </remarks>
public class CatalogDbContext : DbContext
{
    private readonly NodeIdentity _node;
    private readonly TimeProvider _clock;

    /// <summary>Schema propio del módulo.</summary>
    public const string Schema = "catalog";

    /// <summary>Historial de migraciones, dentro del schema del módulo.</summary>
    public const string MigrationsHistoryTable = "__migrations";

    /// <summary>Colación de identidad: ignora mayúsculas, respeta tildes.</summary>
    public const string IdentityCollation = "core.es_ci";

    /// <summary>Colación de búsqueda: ignora mayúsculas y tildes.</summary>
    public const string SearchCollation = "core.es_search";

    /// <summary>Crea el contexto.</summary>
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options, NodeIdentity node, TimeProvider clock)
        : base(options)
    {
        _node = node;
        _clock = clock;
    }

    /// <summary>Categorías del catálogo.</summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>Marcas.</summary>
    public DbSet<Brand> Brands => Set<Brand>();

    /// <summary>Productos.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>Variantes: lo que se cuenta y se cobra.</summary>
    public DbSet<ProductItem> ProductItems => Set<ProductItem>();

    /// <summary>Asociación entre productos y categorías.</summary>
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    /// <summary>Galería de los productos.</summary>
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }

    /// <inheritdoc />
    public override int SaveChanges()
    {
        ChangeTracker.StampReplicationColumns(_node, _clock);
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.StampReplicationColumns(_node, _clock);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
