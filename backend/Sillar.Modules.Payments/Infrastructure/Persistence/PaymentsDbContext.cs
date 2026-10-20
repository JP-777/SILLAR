using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

namespace Sillar.Modules.Payments.Infrastructure.Persistence;

/// <summary>Persistencia técnica local de M11; nunca escribe en <c>sales.*</c>.</summary>
public sealed class PaymentsDbContext(
    DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public const string Schema = "payments";
    public const string MigrationsHistoryTable = "__migrations";

    internal DbSet<ProviderEvidenceReceipt> EvidenceReceipts
        => Set<ProviderEvidenceReceipt>();

    internal DbSet<ProviderInboxEvent> InboxEvents
        => Set<ProviderInboxEvent>();

    internal DbSet<ProviderProcessingAttempt> ProcessingAttempts
        => Set<ProviderProcessingAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PaymentsDbContext).Assembly);
    }
}
