using Microsoft.EntityFrameworkCore;
using Sillar.Modules.ServiceOrders.Domain;
using Sillar.Shared.Data.Replication;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Data;

/// <summary>Persistencia exclusiva de M05b.</summary>
public sealed class ServiceOrdersDbContext(
    DbContextOptions<ServiceOrdersDbContext> options,
    NodeIdentity node,
    TimeProvider clock) : DbContext(options)
{
    public const string Schema = "service_orders";
    public const string MigrationsHistoryTable = "__migrations";

    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<ServiceOrderItem> ServiceOrderItems => Set<ServiceOrderItem>();
    public DbSet<ServiceOrderAssignmentEvent> AssignmentEvents => Set<ServiceOrderAssignmentEvent>();
    public DbSet<ServiceOrderStatusHistory> StatusHistory => Set<ServiceOrderStatusHistory>();
    public DbSet<ServiceOrderSeries> Series => Set<ServiceOrderSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceOrdersDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        ChangeTracker.StampReplicationColumns(node, clock);
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ChangeTracker.StampReplicationColumns(node, clock);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
