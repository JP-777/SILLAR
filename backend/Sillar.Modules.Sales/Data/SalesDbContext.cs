using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Sales.Domain;
using Sillar.Shared.Data.Replication;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Data;

/// <summary>
/// Contexto de datos de M03 Ventas Online. Solo escribe en el schema <c>sales</c>.
/// </summary>
/// <remarks>
/// <para>
/// Dos claves foráneas salen del schema, y las dos están permitidas porque sus
/// módulos son <b>dependencias duras</b>: <c>order_lines.item_id</c> hacia
/// <c>catalog.product_items</c> y <c>orders.customer_id</c> hacia
/// <c>crm.customers</c>. Van en la migración de M03 y no en un script de
/// integración — <c>sales_crm.sql</c> dejó de hacer falta el 21 de agosto de 2026,
/// cuando la cuenta obligatoria para comprar convirtió M04 en dura.
/// </para>
/// <para>
/// <b>Ninguna colación no determinista.</b> No se introduce una «por costumbre»:
/// el código visible se busca exacto y los nombres congelados son evidencia
/// histórica, no claves de búsqueda. Si algún día la lista del panel busca por
/// nombre de cliente, esa columna pasará a <c>core.es_search</c> y entonces
/// <c>LIKE</c> quedará prohibido sobre ella — la salida es <c>COLLATE "C"</c> en la
/// expresión, con el índice usando la misma expresión.
/// </para>
/// </remarks>
public class SalesDbContext(
    DbContextOptions<SalesDbContext> options,
    NodeIdentity node,
    TimeProvider clock) : DbContext(options)
{
    /// <summary>Schema propio del módulo.</summary>
    public const string Schema = "sales";

    /// <summary>Historial de migraciones, dentro del schema del módulo.</summary>
    public const string MigrationsHistoryTable = "__migrations";

    /// <summary>Pedidos.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Líneas de pedido: una por variante comprada.</summary>
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    /// <summary>Pagos registrados. Hechos consumados, no estados.</summary>
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();

    /// <summary>Historial de cambios de estado.</summary>
    public DbSet<OrderStatusChange> OrderStatusChanges => Set<OrderStatusChange>();

    /// <summary>Contadores de serie, uno por nodo y año. No se replican.</summary>
    public DbSet<OrderSeries> OrderSeries => Set<OrderSeries>();

    /// <summary>Carritos de la tienda. No se replican.</summary>
    public DbSet<Cart> Carts => Set<Cart>();

    /// <summary>Contenido de los carritos. No se replica.</summary>
    public DbSet<CartItem> CartItems => Set<CartItem>();

    /// <summary>El nodo de esta instalación, para componer el código visible.</summary>
    internal NodeIdentity Node => node;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
    }

    /// <inheritdoc />
    public override int SaveChanges()
    {
        ChangeTracker.StampReplicationColumns(node, clock);
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ChangeTracker.StampReplicationColumns(node, clock);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
