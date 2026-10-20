using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

namespace Sillar.Modules.Payments.Infrastructure.Persistence.Configurations;

internal sealed class ProviderEvidenceReceiptConfiguration
    : IEntityTypeConfiguration<ProviderEvidenceReceipt>
{
    public void Configure(EntityTypeBuilder<ProviderEvidenceReceipt> builder)
    {
        builder.ToTable("provider_evidence_receipts", table =>
        {
            table.HasCheckConstraint(
                "ck_payment_receipts_provider",
                "btrim(provider) <> ''");
            table.HasCheckConstraint(
                "ck_payment_receipts_event",
                "btrim(provider_event_id) <> ''");
            table.HasCheckConstraint(
                "ck_payment_receipts_settlement",
                "btrim(external_settlement_id) <> ''");
            table.HasCheckConstraint(
                "ck_payment_receipts_sha256",
                "body_sha256 COLLATE \"C\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "ck_payment_receipts_verification_state",
                "verification_state IN ('received','authenticated','rejected','indeterminate')");
            table.HasCheckConstraint(
                "ck_payment_receipts_reception_state",
                "reception_state IN ('received','rejected','indeterminate','held'," +
                "'duplicate','pending_durable_review')");
            table.HasCheckConstraint(
                "ck_payment_receipts_authentication",
                "(verification_state = 'authenticated' AND verified_at IS NOT NULL " +
                "AND attestation_reference IS NOT NULL AND btrim(attestation_reference) <> '' " +
                "AND inbox_event_id IS NOT NULL AND analysis_sha256 IS NOT NULL " +
                "AND reception_state IN ('held','duplicate','pending_durable_review') " +
                "AND rejection_reason IS NULL) OR " +
                "(verification_state <> 'authenticated' AND verified_at IS NULL " +
                "AND attestation_reference IS NULL AND inbox_event_id IS NULL " +
                "AND analysis_sha256 IS NULL)");
            table.HasCheckConstraint(
                "ck_payment_receipts_resolution_reason",
                "(verification_state IN ('rejected','indeterminate') " +
                "AND rejection_reason IS NOT NULL AND btrim(rejection_reason) <> '') OR " +
                "(verification_state NOT IN ('rejected','indeterminate') " +
                "AND rejection_reason IS NULL)");
        });

        builder.HasKey(item => item.ReceiptId)
            .HasName("pk_provider_evidence_receipts");
        builder.Property(item => item.ReceiptId).HasColumnName("receipt_id");
        builder.Property(item => item.Provider)
            .HasColumnName("provider").HasMaxLength(128).IsRequired();
        builder.Property(item => item.ProviderEventId)
            .HasColumnName("provider_event_id").HasMaxLength(512).IsRequired();
        builder.Property(item => item.ExternalSettlementId)
            .HasColumnName("external_settlement_id").HasMaxLength(512).IsRequired();
        builder.Property(item => item.BodySha256)
            .HasColumnName("body_sha256").HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(item => item.ReceivedAt)
            .HasColumnName("received_at").IsRequired();
        builder.Property(item => item.VerificationState)
            .HasColumnName("verification_state").HasMaxLength(32).IsRequired();
        builder.Property(item => item.ReceptionState)
            .HasColumnName("reception_state").HasMaxLength(32).IsRequired();
        builder.Property(item => item.VerifiedAt).HasColumnName("verified_at");
        builder.Property(item => item.AttestationReference)
            .HasColumnName("attestation_reference").HasMaxLength(512);
        builder.Property(item => item.RejectionReason)
            .HasColumnName("rejection_reason").HasMaxLength(512);
        builder.Property(item => item.AnalysisSha256)
            .HasColumnName("analysis_sha256").HasMaxLength(64).IsFixedLength();
        builder.Property(item => item.InboxEventId).HasColumnName("inbox_event_id");

        builder.HasIndex(item => new
            {
                item.Provider,
                item.ProviderEventId,
                item.ReceivedAt
            })
            .HasDatabaseName("idx_payment_receipts_event_received");
        builder.HasIndex(item => item.InboxEventId)
            .HasDatabaseName("idx_payment_receipts_inbox_event");
        builder.HasOne<ProviderInboxEvent>()
            .WithMany()
            .HasForeignKey(item => item.InboxEventId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_payment_receipts_inbox_event");
    }
}
