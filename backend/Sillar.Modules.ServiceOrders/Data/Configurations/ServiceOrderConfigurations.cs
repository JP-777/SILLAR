using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Domain;
using Sillar.Shared.Data.Replication;

namespace Sillar.Modules.ServiceOrders.Data.Configurations;

internal static class ServiceOrderChecks
{
    public static string Status(string column)
        => $"{column} IN ({string.Join(", ", ServiceOrderStatuses.All.Select(value => $"'{value.Code}'"))})";

    public static string Triple(string name, string id, string node)
        => $"(({name} IS NULL AND {id} IS NULL AND {node} IS NULL) OR " +
           $"({name} IS NOT NULL AND {id} IS NOT NULL AND {node} IS NOT NULL))";
}

internal sealed class ServiceOrderConfiguration : IEntityTypeConfiguration<ServiceOrder>
{
    public void Configure(EntityTypeBuilder<ServiceOrder> builder)
    {
        builder.ToTable("service_orders", table =>
        {
            table.HasCheckConstraint("ck_service_orders_visible_code", "btrim(visible_code) <> ''");
            table.HasCheckConstraint("ck_service_orders_status", ServiceOrderChecks.Status("status"));
            table.HasCheckConstraint("ck_service_orders_customer_name", "btrim(customer_name_snapshot) <> ''");
            table.HasCheckConstraint("ck_service_orders_contact", "nullif(btrim(customer_phone_snapshot), '') IS NOT NULL OR nullif(btrim(customer_email_snapshot), '') IS NOT NULL");
            table.HasCheckConstraint("ck_service_orders_create_idempotency_key", "create_idempotency_key <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_service_orders_phone", "customer_phone_snapshot IS NULL OR btrim(customer_phone_snapshot) <> ''");
            table.HasCheckConstraint("ck_service_orders_email", "customer_email_snapshot IS NULL OR btrim(customer_email_snapshot) <> ''");
            table.HasCheckConstraint("ck_service_orders_promised_at", "promised_at IS NULL OR promised_at >= received_at");
            table.HasCheckConstraint("ck_service_orders_last_status_at", "last_status_changed_at >= received_at");
            table.HasCheckConstraint("ck_service_orders_created_by", "btrim(created_by_admin_name) <> '' AND created_by_admin_user_id > 0 AND btrim(created_by_admin_user_home_node) <> ''");
            table.HasCheckConstraint("ck_service_orders_assignee_triple", ServiceOrderChecks.Triple("current_assignee_name", "current_assignee_admin_user_id", "current_assignee_admin_user_home_node"));
            table.HasCheckConstraint("ck_service_orders_assignee_values", "current_assignee_name IS NULL OR (btrim(current_assignee_name) <> '' AND current_assignee_admin_user_id > 0 AND btrim(current_assignee_admin_user_home_node) <> '')");
            table.HasCheckConstraint("ck_service_orders_last_actor_triple", ServiceOrderChecks.Triple("last_status_changed_by_name", "last_status_changed_by_admin_user_id", "last_status_changed_by_admin_user_home_node"));
            table.HasCheckConstraint("ck_service_orders_last_actor_values", "last_status_changed_by_name IS NULL OR (btrim(last_status_changed_by_name) <> '' AND last_status_changed_by_admin_user_id > 0 AND btrim(last_status_changed_by_admin_user_home_node) <> '')");
            table.HasCheckConstraint("ck_service_orders_last_actor_not_fictitious", "last_status_changed_by_name IS NULL OR btrim(last_status_changed_by_name) <> 'Sistema'");
            table.HasCheckConstraint("ck_service_orders_replication", "btrim(origin_node) <> '' AND row_version > 0");
        });

        builder.HasKey(x => x.ServiceOrderId).HasName("pk_service_orders");
        builder.Property(x => x.ServiceOrderId).HasColumnName("service_order_id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.VisibleCode).HasColumnName("visible_code").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasDefaultValue(ServiceOrderStatuses.Received).ValueGeneratedNever();
        builder.Property(x => x.CreateIdempotencyKey).HasColumnName("create_idempotency_key").HasColumnType("uuid");
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").HasColumnType("uuid");
        builder.Property(x => x.CustomerNameSnapshot).HasColumnName("customer_name_snapshot").IsRequired();
        builder.Property(x => x.CustomerPhoneSnapshot).HasColumnName("customer_phone_snapshot");
        builder.Property(x => x.CustomerEmailSnapshot).HasColumnName("customer_email_snapshot");
        builder.Property(x => x.ReceivedNotes).HasColumnName("received_notes");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamptz");
        builder.Property(x => x.PromisedAt).HasColumnName("promised_at").HasColumnType("timestamptz");
        builder.Property(x => x.CreatedByAdminName).HasColumnName("created_by_admin_name").IsRequired();
        builder.Property(x => x.CreatedByAdminUserId).HasColumnName("created_by_admin_user_id");
        builder.Property(x => x.CreatedByAdminUserHomeNode).HasColumnName("created_by_admin_user_home_node").IsRequired();
        builder.Property(x => x.CurrentAssigneeName).HasColumnName("current_assignee_name");
        builder.Property(x => x.CurrentAssigneeAdminUserId).HasColumnName("current_assignee_admin_user_id");
        builder.Property(x => x.CurrentAssigneeAdminUserHomeNode).HasColumnName("current_assignee_admin_user_home_node");
        builder.Property(x => x.LastStatusChangedAt).HasColumnName("last_status_changed_at").HasColumnType("timestamptz");
        builder.Property(x => x.LastStatusChangedByName).HasColumnName("last_status_changed_by_name");
        builder.Property(x => x.LastStatusChangedByAdminUserId).HasColumnName("last_status_changed_by_admin_user_id");
        builder.Property(x => x.LastStatusChangedByAdminUserHomeNode).HasColumnName("last_status_changed_by_admin_user_home_node");
        builder.MapReplication();

        builder.HasIndex(x => x.VisibleCode).IsUnique().HasDatabaseName("uq_service_orders_visible_code");
        builder.HasIndex(x => x.CreateIdempotencyKey).IsUnique()
            .HasDatabaseName("uq_service_orders_create_idempotency_key");
        builder.HasIndex(x => x.Status).HasDatabaseName("idx_service_orders_status");
        builder.HasIndex(x => x.ReceivedAt).HasDatabaseName("idx_service_orders_received_at");
    }
}

internal sealed class ServiceOrderItemConfiguration : IEntityTypeConfiguration<ServiceOrderItem>
{
    public void Configure(EntityTypeBuilder<ServiceOrderItem> builder)
    {
        builder.ToTable("service_order_items", table =>
        {
            table.HasCheckConstraint("ck_service_order_items_source_id", "service_source_id > 0");
            table.HasCheckConstraint("ck_service_order_items_source_node", "btrim(service_source_node) <> ''");
            table.HasCheckConstraint("ck_service_order_items_name", "btrim(service_name_snapshot) <> ''");
            table.HasCheckConstraint("ck_service_order_items_slug", "service_slug_snapshot COLLATE \"C\" ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'");
            table.HasCheckConstraint("ck_service_order_items_description", "service_short_description_snapshot IS NOT NULL OR service_description_snapshot IS NOT NULL");
            table.HasCheckConstraint("ck_service_order_items_showcase_price", "showcase_price_snapshot IS NULL OR showcase_price_snapshot >= 0");
            table.HasCheckConstraint("ck_service_order_items_image_alt", "media_asset_id_snapshot IS NULL OR nullif(btrim(image_alt_text_snapshot), '') IS NOT NULL");
            table.HasCheckConstraint("ck_service_order_items_requested_details", "btrim(requested_details) <> ''");
            table.HasCheckConstraint("ck_service_order_items_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_service_order_items_agreed_price", "agreed_unit_price IS NULL OR agreed_unit_price >= 0");
            table.HasCheckConstraint("ck_service_order_items_sort_order", "sort_order >= 0");
            table.HasCheckConstraint("ck_service_order_items_replication", "btrim(origin_node) <> '' AND row_version > 0");
        });

        builder.HasKey(x => x.ServiceOrderItemId).HasName("pk_service_order_items");
        builder.Property(x => x.ServiceOrderItemId).HasColumnName("service_order_item_id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.ServiceOrderId).HasColumnName("service_order_id").HasColumnType("uuid");
        builder.Property(x => x.ServiceSourceId).HasColumnName("service_source_id");
        builder.Property(x => x.ServiceSourceNode).HasColumnName("service_source_node").IsRequired();
        builder.Property(x => x.ServiceNameSnapshot).HasColumnName("service_name_snapshot").IsRequired();
        builder.Property(x => x.ServiceSlugSnapshot).HasColumnName("service_slug_snapshot").IsRequired();
        builder.Property(x => x.ServiceShortDescriptionSnapshot).HasColumnName("service_short_description_snapshot");
        builder.Property(x => x.ServiceDescriptionSnapshot).HasColumnName("service_description_snapshot");
        builder.Property(x => x.ShowcasePriceSnapshot).HasColumnName("showcase_price_snapshot").HasColumnType("numeric(12,2)");
        builder.Property(x => x.SaleUnitSnapshot).HasColumnName("sale_unit_snapshot");
        builder.Property(x => x.MediaAssetIdSnapshot).HasColumnName("media_asset_id_snapshot").HasColumnType("uuid");
        builder.Property(x => x.ImageUrlSnapshot).HasColumnName("image_url_snapshot");
        builder.Property(x => x.ImageAltTextSnapshot).HasColumnName("image_alt_text_snapshot");
        builder.Property(x => x.RequestedDetails).HasColumnName("requested_details").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("numeric(12,3)");
        builder.Property(x => x.AgreedUnitPrice).HasColumnName("agreed_unit_price").HasColumnType("numeric(12,2)");
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.MapReplication();

        builder.HasOne(x => x.ServiceOrder).WithMany(x => x.Items)
            .HasForeignKey(x => x.ServiceOrderId).HasConstraintName("fk_service_order_items_service_order_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ServiceOrderId, x.SortOrder }).IsUnique()
            .HasDatabaseName("uq_service_order_items_order_sort");
    }
}

internal sealed class ServiceOrderAssignmentEventConfiguration : IEntityTypeConfiguration<ServiceOrderAssignmentEvent>
{
    public void Configure(EntityTypeBuilder<ServiceOrderAssignmentEvent> builder)
    {
        builder.ToTable("service_order_assignment_events", table =>
        {
            table.HasCheckConstraint("ck_service_order_assignment_events_action", "action IN ('assigned','unassigned')");
            table.HasCheckConstraint("ck_service_order_assignment_events_assignee", "btrim(assignee_name) <> '' AND assignee_admin_user_id > 0 AND btrim(assignee_admin_user_home_node) <> ''");
            table.HasCheckConstraint("ck_service_order_assignment_events_actor", "btrim(performed_by_name) <> '' AND performed_by_admin_user_id > 0 AND btrim(performed_by_admin_user_home_node) <> ''");
            table.HasCheckConstraint("ck_service_order_assignment_events_replication", "btrim(origin_node) <> '' AND row_version > 0");
        });

        builder.HasKey(x => x.AssignmentEventId).HasName("pk_service_order_assignment_events");
        builder.Property(x => x.AssignmentEventId).HasColumnName("assignment_event_id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.ServiceOrderId).HasColumnName("service_order_id").HasColumnType("uuid");
        builder.Property(x => x.Action).HasColumnName("action").IsRequired();
        builder.Property(x => x.AssigneeName).HasColumnName("assignee_name").IsRequired();
        builder.Property(x => x.AssigneeAdminUserId).HasColumnName("assignee_admin_user_id");
        builder.Property(x => x.AssigneeAdminUserHomeNode).HasColumnName("assignee_admin_user_home_node").IsRequired();
        builder.Property(x => x.PerformedByName).HasColumnName("performed_by_name").IsRequired();
        builder.Property(x => x.PerformedByAdminUserId).HasColumnName("performed_by_admin_user_id");
        builder.Property(x => x.PerformedByAdminUserHomeNode).HasColumnName("performed_by_admin_user_home_node").IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamptz");
        builder.MapReplication();
        builder.HasOne(x => x.ServiceOrder).WithMany(x => x.AssignmentEvents)
            .HasForeignKey(x => x.ServiceOrderId).HasConstraintName("fk_service_order_assignment_events_service_order_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ServiceOrderId, x.OccurredAt })
            .HasDatabaseName("idx_service_order_assignment_events_order_occurred");
    }
}

internal sealed class ServiceOrderStatusHistoryConfiguration : IEntityTypeConfiguration<ServiceOrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<ServiceOrderStatusHistory> builder)
    {
        builder.ToTable("service_order_status_history", table =>
        {
            table.HasCheckConstraint("ck_service_order_status_history_from", $"from_status IS NULL OR {ServiceOrderChecks.Status("from_status")}");
            table.HasCheckConstraint("ck_service_order_status_history_to", ServiceOrderChecks.Status("to_status"));
            table.HasCheckConstraint("ck_service_order_status_history_initial", $"from_status IS NOT NULL OR to_status = '{ServiceOrderStatuses.Received}'");
            table.HasCheckConstraint("ck_service_order_status_history_change", "from_status IS NULL OR from_status <> to_status");
            table.HasCheckConstraint("ck_service_order_status_history_actor_triple", ServiceOrderChecks.Triple("performed_by_name", "performed_by_admin_user_id", "performed_by_admin_user_home_node"));
            table.HasCheckConstraint("ck_service_order_status_history_actor_values", "performed_by_name IS NULL OR (btrim(performed_by_name) <> '' AND performed_by_admin_user_id > 0 AND btrim(performed_by_admin_user_home_node) <> '')");
            table.HasCheckConstraint("ck_service_order_status_history_actor_not_fictitious", "performed_by_name IS NULL OR btrim(performed_by_name) <> 'Sistema'");
            table.HasCheckConstraint("ck_service_order_status_history_replication", "btrim(origin_node) <> '' AND row_version > 0");
        });

        builder.HasKey(x => x.StatusHistoryId).HasName("pk_service_order_status_history");
        builder.Property(x => x.StatusHistoryId).HasColumnName("status_history_id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.ServiceOrderId).HasColumnName("service_order_id").HasColumnType("uuid");
        builder.Property(x => x.FromStatus).HasColumnName("from_status");
        builder.Property(x => x.ToStatus).HasColumnName("to_status").IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamptz");
        builder.Property(x => x.PerformedByName).HasColumnName("performed_by_name");
        builder.Property(x => x.PerformedByAdminUserId).HasColumnName("performed_by_admin_user_id");
        builder.Property(x => x.PerformedByAdminUserHomeNode).HasColumnName("performed_by_admin_user_home_node");
        builder.MapReplication();
        builder.HasOne(x => x.ServiceOrder).WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.ServiceOrderId).HasConstraintName("fk_service_order_status_history_service_order_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ServiceOrderId, x.OccurredAt })
            .HasDatabaseName("idx_service_order_status_history_order_occurred");
    }
}

internal sealed class ServiceOrderSeriesConfiguration : IEntityTypeConfiguration<ServiceOrderSeries>
{
    public void Configure(EntityTypeBuilder<ServiceOrderSeries> builder)
    {
        builder.ToTable("service_order_series", table =>
        {
            table.HasCheckConstraint("ck_service_order_series_node_code", "btrim(node_code) <> ''");
            table.HasCheckConstraint("ck_service_order_series_year", "year BETWEEN 2000 AND 9999");
            table.HasCheckConstraint("ck_service_order_series_last_number", "last_number >= 0");
        });
        builder.HasKey(x => x.ServiceOrderSeriesId).HasName("pk_service_order_series");
        builder.Property(x => x.ServiceOrderSeriesId).HasColumnName("service_order_series_id").UseIdentityAlwaysColumn();
        builder.Property(x => x.NodeCode).HasColumnName("node_code").IsRequired();
        builder.Property(x => x.Year).HasColumnName("year");
        builder.Property(x => x.LastNumber).HasColumnName("last_number").HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        builder.HasIndex(x => new { x.NodeCode, x.Year }).IsUnique()
            .HasDatabaseName("uq_service_order_series_node_code_year");
    }
}
