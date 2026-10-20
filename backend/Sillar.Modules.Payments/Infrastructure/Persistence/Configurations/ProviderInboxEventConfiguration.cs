using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

namespace Sillar.Modules.Payments.Infrastructure.Persistence.Configurations;

internal sealed class ProviderInboxEventConfiguration
    : IEntityTypeConfiguration<ProviderInboxEvent>
{
    public void Configure(EntityTypeBuilder<ProviderInboxEvent> builder)
    {
        builder.ToTable("provider_inbox_events", table =>
        {
            table.HasCheckConstraint(
                "ck_payment_inbox_provider",
                "btrim(provider) <> ''");
            table.HasCheckConstraint(
                "ck_payment_inbox_event",
                "btrim(provider_event_id) <> ''");
            table.HasCheckConstraint(
                "ck_payment_inbox_settlement",
                "btrim(external_settlement_id) <> ''");
            table.HasCheckConstraint(
                "ck_payment_inbox_sha256",
                "body_sha256 COLLATE \"C\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "ck_payment_inbox_processing_state",
                "processing_state IN ('pending_review','held','rejected','ready'," +
                "'processing','indeterminate','completed')");
            table.HasCheckConstraint(
                "ck_payment_inbox_commercial_effect_state",
                "commercial_effect_state IN ('not_requested','indeterminate','accredited')");
            table.HasCheckConstraint(
                "ck_payment_inbox_attempt_count",
                "attempt_count >= 0");
            table.HasCheckConstraint(
                "ck_payment_inbox_lease",
                "(processing_state = 'processing' AND lease_token IS NOT NULL " +
                "AND lease_until IS NOT NULL) OR " +
                "(processing_state <> 'processing' AND lease_token IS NULL " +
                "AND lease_until IS NULL)");
            table.HasCheckConstraint(
                "ck_payment_inbox_hold_reason",
                "(processing_state = 'held' AND hold_reason IS NOT NULL " +
                "AND btrim(hold_reason) <> '') OR " +
                "(processing_state <> 'held' AND hold_reason IS NULL)");
            table.HasCheckConstraint(
                "ck_payment_inbox_commercial_reference",
                "(commercial_effect_state = 'accredited' " +
                "AND commercial_effect_reference IS NOT NULL " +
                "AND btrim(commercial_effect_reference) <> '') OR " +
                "(commercial_effect_state <> 'accredited' " +
                "AND commercial_effect_reference IS NULL)");
        });

        builder.HasKey(item => item.InboxEventId)
            .HasName("pk_provider_inbox_events");
        builder.Property(item => item.InboxEventId).HasColumnName("inbox_event_id");
        builder.Property(item => item.Provider)
            .HasColumnName("provider").HasMaxLength(128).IsRequired();
        builder.Property(item => item.ProviderEventId)
            .HasColumnName("provider_event_id").HasMaxLength(512).IsRequired();
        builder.Property(item => item.ExternalSettlementId)
            .HasColumnName("external_settlement_id").HasMaxLength(512).IsRequired();
        builder.Property(item => item.BodySha256)
            .HasColumnName("body_sha256").HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(item => item.VerifiedAt).HasColumnName("verified_at").IsRequired();
        builder.Property(item => item.ProcessingState)
            .HasColumnName("processing_state").HasMaxLength(32).IsRequired();
        builder.Property(item => item.HoldReason)
            .HasColumnName("hold_reason").HasMaxLength(512);
        builder.Property(item => item.AttemptCount)
            .HasColumnName("attempt_count").HasDefaultValue(0).IsRequired();
        builder.Property(item => item.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(item => item.LeaseToken).HasColumnName("lease_token");
        builder.Property(item => item.LeaseUntil).HasColumnName("lease_until");
        builder.Property(item => item.CommercialEffectState)
            .HasColumnName("commercial_effect_state").HasMaxLength(32).IsRequired();
        builder.Property(item => item.CommercialEffectReference)
            .HasColumnName("commercial_effect_reference").HasMaxLength(512);
        builder.Property(item => item.LastFailureCode)
            .HasColumnName("last_failure_code").HasMaxLength(256);
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(item => new { item.Provider, item.ProviderEventId })
            .IsUnique()
            .HasDatabaseName("uq_payment_inbox_provider_event");
        builder.HasIndex(item => item.ExternalSettlementId)
            .HasDatabaseName("idx_payment_inbox_settlement");
        builder.HasIndex(item => new
            {
                item.ProcessingState,
                item.NextAttemptAt,
                item.LeaseUntil
            })
            .HasDatabaseName("idx_payment_inbox_work");
    }
}
