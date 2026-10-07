using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Tracking.Domain;
using Sillar.Shared.Data.Replication;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Tracking.Data;

public sealed class TrackingDbContext(
    DbContextOptions<TrackingDbContext> options,
    NodeIdentity node,
    TimeProvider clock) : DbContext(options)
{
    public const string Schema = "tracking";
    public const string MigrationsHistoryTable = "__migrations";

    public DbSet<OrderTracking> OrderTracking => Set<OrderTracking>();
    public DbSet<TrackingNote> TrackingNotes => Set<TrackingNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackingDbContext).Assembly);
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
