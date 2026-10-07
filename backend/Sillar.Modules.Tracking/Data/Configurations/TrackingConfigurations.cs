using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Tracking.Domain;
using Sillar.Shared.Data.Replication;

namespace Sillar.Modules.Tracking.Data.Configurations;

internal static class TrackingChecks
{
    public static string Triple(string name, string id, string node)
        => $"(({name} IS NULL AND {id} IS NULL AND {node} IS NULL) OR " +
           $"({name} IS NOT NULL AND {id} IS NOT NULL AND {node} IS NOT NULL))";
}

internal sealed class OrderTrackingConfiguration : IEntityTypeConfiguration<OrderTracking>
{
    public void Configure(EntityTypeBuilder<OrderTracking> builder)
    {
        builder.ToTable("order_tracking", table =>
        {
            table.HasCheckConstraint(
                "ck_order_tracking_priority",
                "board_priority IS NULL OR board_priority >= 0");

            table.HasCheckConstraint(
                "ck_order_tracking_last_touched_triple",
                TrackingChecks.Triple(
                    "last_touched_by_name",
                    "last_touched_by_admin_user_id",
                    "last_touched_by_admin_user_home_node"));

            table.HasCheckConstraint(
                "ck_order_tracking_last_touched_values",
                "last_touched_by_name IS NULL OR " +
                "(btrim(last_touched_by_name) <> '' " +
                "AND last_touched_by_admin_user_id > 0 " +
                "AND btrim(last_touched_by_admin_user_home_node) <> '')");

            table.HasCheckConstraint(
                "ck_order_tracking_last_touched_not_fictitious",
                "last_touched_by_name IS NULL OR btrim(last_touched_by_name) <> 'Sistema'");

            table.HasCheckConstraint(
                "ck_order_tracking_replication",
                "btrim(origin_node) <> '' AND row_version > 0");
        });

        builder.HasKey(x => x.OrderTrackingId)
            .HasName("pk_order_tracking");

        builder.Property(x => x.OrderTrackingId)
            .HasColumnName("order_tracking_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(x => x.ServiceOrderId)
            .HasColumnName("service_order_id")
            .HasColumnType("uuid");

        builder.Property(x => x.BoardPriority)
            .HasColumnName("board_priority");

        builder.Property(x => x.InternalDueAt)
            .HasColumnName("internal_due_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.Pinned)
            .HasColumnName("pinned")
            .HasDefaultValue(false);

        builder.Property(x => x.LastTouchedByName)
            .HasColumnName("last_touched_by_name");

        builder.Property(x => x.LastTouchedByAdminUserId)
            .HasColumnName("last_touched_by_admin_user_id");

        builder.Property(x => x.LastTouchedByAdminUserHomeNode)
            .HasColumnName("last_touched_by_admin_user_home_node");

        builder.Property(x => x.LastTouchedAt)
            .HasColumnName("last_touched_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.MapReplication();

        builder.HasIndex(x => x.ServiceOrderId)
            .IsUnique()
            .HasDatabaseName("uq_order_tracking_service_order_id");
    }
}

internal sealed class TrackingNoteConfiguration : IEntityTypeConfiguration<TrackingNote>
{
    public void Configure(EntityTypeBuilder<TrackingNote> builder)
    {
        builder.ToTable("tracking_notes", table =>
        {
            table.HasCheckConstraint(
                "ck_tracking_notes_body",
                "btrim(body) <> ''");

            table.HasCheckConstraint(
                "ck_tracking_notes_author_triple",
                TrackingChecks.Triple(
                    "author_name",
                    "author_admin_user_id",
                    "author_admin_user_home_node"));

            table.HasCheckConstraint(
                "ck_tracking_notes_author_values",
                "author_name IS NULL OR " +
                "(btrim(author_name) <> '' " +
                "AND author_admin_user_id > 0 " +
                "AND btrim(author_admin_user_home_node) <> '')");

            table.HasCheckConstraint(
                "ck_tracking_notes_author_not_fictitious",
                "author_name IS NULL OR btrim(author_name) <> 'Sistema'");

            table.HasCheckConstraint(
                "ck_tracking_notes_replication",
                "btrim(origin_node) <> '' AND row_version > 0");
        });

        builder.HasKey(x => x.TrackingNoteId)
            .HasName("pk_tracking_notes");

        builder.Property(x => x.TrackingNoteId)
            .HasColumnName("tracking_note_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(x => x.OrderTrackingId)
            .HasColumnName("order_tracking_id")
            .HasColumnType("uuid");

        builder.Property(x => x.Body)
            .HasColumnName("body")
            .IsRequired();

        builder.Property(x => x.AuthorName)
            .HasColumnName("author_name");

        builder.Property(x => x.AuthorAdminUserId)
            .HasColumnName("author_admin_user_id");

        builder.Property(x => x.AuthorAdminUserHomeNode)
            .HasColumnName("author_admin_user_home_node");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.MapReplication();

        builder.HasOne(x => x.OrderTracking)
            .WithMany(x => x.Notes)
            .HasForeignKey(x => x.OrderTrackingId)
            .HasConstraintName("fk_tracking_notes_order_tracking_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.OrderTrackingId, x.CreatedAt })
            .HasDatabaseName("idx_tracking_notes_order_created");
    }
}
