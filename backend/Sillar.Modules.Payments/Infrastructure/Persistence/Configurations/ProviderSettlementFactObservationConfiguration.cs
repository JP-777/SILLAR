using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

namespace Sillar.Modules.Payments.Infrastructure.Persistence.Configurations;

internal sealed class ProviderSettlementFactObservationConfiguration
    : IEntityTypeConfiguration<ProviderSettlementFactObservation>
{
    public void Configure(
        EntityTypeBuilder<ProviderSettlementFactObservation> builder)
    {
        builder.ToTable("provider_settlement_fact_observations", table =>
        {
            table.HasCheckConstraint(
                "ck_payment_settlement_fact_provider",
                "btrim(provider) <> ''");
            table.HasCheckConstraint(
                "ck_payment_settlement_fact_event",
                "btrim(provider_event_id) <> ''");
            table.HasCheckConstraint(
                "ck_payment_settlement_fact_settlement",
                "btrim(external_settlement_id) <> ''");
            table.HasCheckConstraint(
                "ck_payment_settlement_fact_sha256",
                "body_sha256 COLLATE \"C\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "ck_payment_settlement_fact_amount",
                "gross_amount > 0");
            table.HasCheckConstraint(
                "ck_payment_settlement_fact_currency",
                "currency_code COLLATE \"C\" ~ '^[A-Z]{3}$'");
            table.HasCheckConstraint(
                "ck_payment_settlement_fact_classification",
                "classification IN " +
                "('new','duplicate','conflict','decision_required')");
            table.HasCheckConstraint(
                "ck_payment_settlement_fact_timestamps",
                "recorded_at >= verified_at");
        });

        builder.HasKey(item => item.ReceiptId)
            .HasName("pk_provider_settlement_fact_observations");
        builder.Property(item => item.ReceiptId).HasColumnName("receipt_id");
        builder.Property(item => item.Provider)
            .HasColumnName("provider")
            .HasMaxLength(128)
            .UseCollation(PaymentsDbContext.OpaqueIdentifierCollation)
            .IsRequired();
        builder.Property(item => item.ProviderEventId)
            .HasColumnName("provider_event_id")
            .HasMaxLength(512)
            .UseCollation(PaymentsDbContext.OpaqueIdentifierCollation)
            .IsRequired();
        builder.Property(item => item.ExternalSettlementId)
            .HasColumnName("external_settlement_id")
            .HasMaxLength(512)
            .UseCollation(PaymentsDbContext.OpaqueIdentifierCollation)
            .IsRequired();
        builder.Property(item => item.BodySha256)
            .HasColumnName("body_sha256")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(item => item.VerifiedAt)
            .HasColumnName("verified_at")
            .IsRequired();
        builder.Property(item => item.GrossAmount)
            .HasColumnName("gross_amount")
            .HasColumnType("numeric")
            .IsRequired();
        builder.Property(item => item.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .IsFixedLength()
            .UseCollation(PaymentsDbContext.OpaqueIdentifierCollation)
            .IsRequired();
        builder.Property(item => item.ProviderConfirmedAt)
            .HasColumnName("provider_confirmed_at")
            .IsRequired();
        builder.Property(item => item.Classification)
            .HasColumnName("classification")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(item => item.RecordedAt)
            .HasColumnName("recorded_at")
            .IsRequired();

        builder.HasIndex(item => new { item.Provider, item.ProviderEventId })
            .HasDatabaseName("idx_payment_settlement_fact_provider_event");
        builder.HasIndex(item => item.ExternalSettlementId)
            .HasDatabaseName("idx_payment_settlement_fact_settlement");
        builder.HasOne<ProviderEvidenceReceipt>()
            .WithOne()
            .HasForeignKey<ProviderSettlementFactObservation>(
                item => item.ReceiptId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_payment_settlement_fact_receipt");

        foreach (var property in builder.Metadata.GetProperties())
        {
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        }
    }
}
