using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

namespace Sillar.Modules.Payments.Infrastructure.Persistence;

/// <summary>Persistencia técnica local de M11; nunca escribe en <c>sales.*</c>.</summary>
public sealed class PaymentsDbContext(
    DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public const string Schema = "payments";
    public const string MigrationsHistoryTable = "__migrations";

    /// <summary>
    /// Colación determinista y sensible a cada byte para identificadores
    /// externos opacos. Evita heredar una colación cultural o case-insensitive.
    /// </summary>
    public const string OpaqueIdentifierCollation = "C";

    internal DbSet<ProviderEvidenceReceipt> EvidenceReceipts
        => Set<ProviderEvidenceReceipt>();

    internal DbSet<ProviderInboxEvent> InboxEvents
        => Set<ProviderInboxEvent>();

    internal DbSet<ProviderProcessingAttempt> ProcessingAttempts
        => Set<ProviderProcessingAttempt>();

    internal DbSet<ProviderSettlementFactObservation> SettlementFactObservations
        => Set<ProviderSettlementFactObservation>();

    internal DbSet<ProviderSettlementConcurrencyFence>
        SettlementConcurrencyFences
        => Set<ProviderSettlementConcurrencyFence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PaymentsDbContext).Assembly);
    }
}
