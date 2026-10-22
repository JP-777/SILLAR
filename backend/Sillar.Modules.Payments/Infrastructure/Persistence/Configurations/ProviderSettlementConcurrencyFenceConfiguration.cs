using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

namespace Sillar.Modules.Payments.Infrastructure.Persistence.Configurations;

internal sealed class ProviderSettlementConcurrencyFenceConfiguration
    : IEntityTypeConfiguration<ProviderSettlementConcurrencyFence>
{
    public void Configure(
        EntityTypeBuilder<ProviderSettlementConcurrencyFence> builder)
    {
        builder.ToTable("provider_settlement_concurrency_fences", table =>
        {
            table.HasCheckConstraint(
                "ck_payment_settlement_fence_sha256",
                "fence_sha256 COLLATE \"C\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "ck_payment_settlement_fence_identity",
                "btrim(external_settlement_id) <> ''");
            table.HasCheckConstraint(
                "ck_payment_settlement_fence_revision",
                "revision > 0");
            table.HasCheckConstraint(
                "ck_payment_settlement_fence_timestamps",
                "updated_at >= created_at");
        });

        builder.HasKey(item => item.FenceSha256)
            .HasName("pk_provider_settlement_concurrency_fences");
        builder.Property(item => item.FenceSha256)
            .HasColumnName("fence_sha256")
            .HasMaxLength(64)
            .IsFixedLength()
            .UseCollation(PaymentsDbContext.OpaqueIdentifierCollation)
            .ValueGeneratedNever();
        builder.Property(item => item.ExternalSettlementId)
            .HasColumnName("external_settlement_id")
            .HasMaxLength(512)
            .UseCollation(PaymentsDbContext.OpaqueIdentifierCollation)
            .IsRequired();
        builder.Property(item => item.Revision)
            .HasColumnName("revision")
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(item => item.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(item => item.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Deliberadamente no hay UNIQUE(external_settlement_id): la fila es
        // una valla conservadora de concurrencia, no identidad comercial.
        builder.HasIndex(item => item.ExternalSettlementId)
            .HasDatabaseName("idx_payment_settlement_fence_identity");
    }
}
