using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

namespace Sillar.Modules.Payments.Infrastructure.Persistence.Configurations;

internal sealed class ProviderProcessingAttemptConfiguration
    : IEntityTypeConfiguration<ProviderProcessingAttempt>
{
    public void Configure(EntityTypeBuilder<ProviderProcessingAttempt> builder)
    {
        builder.ToTable("provider_processing_attempts", table =>
        {
            table.HasCheckConstraint(
                "ck_payment_attempt_number",
                "attempt_number > 0");
            table.HasCheckConstraint(
                "ck_payment_attempt_outcome",
                "outcome IN ('running','succeeded','failed','indeterminate')");
            table.HasCheckConstraint(
                "ck_payment_attempt_completion",
                "(outcome = 'running' AND completed_at IS NULL " +
                "AND failure_code IS NULL) OR " +
                "(outcome = 'succeeded' AND completed_at IS NOT NULL " +
                "AND failure_code IS NULL) OR " +
                "(outcome IN ('failed','indeterminate') AND completed_at IS NOT NULL " +
                "AND failure_code IS NOT NULL AND btrim(failure_code) <> '')");
            table.HasCheckConstraint(
                "ck_payment_attempt_times",
                "completed_at IS NULL OR completed_at >= started_at");
        });

        builder.HasKey(item => item.ProcessingAttemptId)
            .HasName("pk_provider_processing_attempts");
        builder.Property(item => item.ProcessingAttemptId)
            .HasColumnName("processing_attempt_id");
        builder.Property(item => item.InboxEventId).HasColumnName("inbox_event_id");
        builder.Property(item => item.AttemptNumber).HasColumnName("attempt_number");
        builder.Property(item => item.LeaseToken).HasColumnName("lease_token");
        builder.Property(item => item.StartedAt).HasColumnName("started_at");
        builder.Property(item => item.CompletedAt).HasColumnName("completed_at");
        builder.Property(item => item.Outcome)
            .HasColumnName("outcome").HasMaxLength(32).IsRequired();
        builder.Property(item => item.FailureCode)
            .HasColumnName("failure_code").HasMaxLength(256);

        builder.HasIndex(item => new { item.InboxEventId, item.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("uq_payment_attempt_inbox_number");
        builder.HasIndex(item => item.Outcome)
            .HasDatabaseName("idx_payment_attempt_outcome");
        builder.HasOne<ProviderInboxEvent>()
            .WithMany()
            .HasForeignKey(item => item.InboxEventId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_payment_attempts_inbox_event");
    }
}
