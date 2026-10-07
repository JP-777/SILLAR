using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sillar.Modules.ServiceOrders.Migrations
{
    /// <inheritdoc />
    public partial class ServiceOrdersInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    faltan text[] := ARRAY[]::text[];
                BEGIN
                    IF to_regclass('core.modules') IS NULL THEN
                        faltan := array_append(faltan, 'CORE (schema core)');
                    END IF;
                    IF to_regclass('services.service_entries') IS NULL THEN
                        faltan := array_append(faltan, 'M05a Servicios — Vitrina (schema services)');
                    END IF;
                    IF cardinality(faltan) > 0 THEN
                        RAISE EXCEPTION 'M05b Servicios — Órdenes no se puede instalar porque falta: %. Instala eso antes y repite la instalación de M05b.',
                            array_to_string(faltan, ' y ')
                            USING ERRCODE = 'undefined_table';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.EnsureSchema(
                name: "service_orders");

            migrationBuilder.CreateTable(
                name: "service_order_series",
                schema: "service_orders",
                columns: table => new
                {
                    service_order_series_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    node_code = table.Column<string>(type: "text", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_order_series", x => x.service_order_series_id);
                    table.CheckConstraint("ck_service_order_series_last_number", "last_number >= 0");
                    table.CheckConstraint("ck_service_order_series_node_code", "btrim(node_code) <> ''");
                    table.CheckConstraint("ck_service_order_series_year", "year BETWEEN 2000 AND 9999");
                });

            migrationBuilder.CreateTable(
                name: "service_orders",
                schema: "service_orders",
                columns: table => new
                {
                    service_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    visible_code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "received"),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_name_snapshot = table.Column<string>(type: "text", nullable: false),
                    customer_phone_snapshot = table.Column<string>(type: "text", nullable: true),
                    customer_email_snapshot = table.Column<string>(type: "text", nullable: true),
                    received_notes = table.Column<string>(type: "text", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    promised_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_by_admin_name = table.Column<string>(type: "text", nullable: false),
                    created_by_admin_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_admin_user_home_node = table.Column<string>(type: "text", nullable: false),
                    current_assignee_name = table.Column<string>(type: "text", nullable: true),
                    current_assignee_admin_user_id = table.Column<int>(type: "integer", nullable: true),
                    current_assignee_admin_user_home_node = table.Column<string>(type: "text", nullable: true),
                    last_status_changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    last_status_changed_by_name = table.Column<string>(type: "text", nullable: true),
                    last_status_changed_by_admin_user_id = table.Column<int>(type: "integer", nullable: true),
                    last_status_changed_by_admin_user_home_node = table.Column<string>(type: "text", nullable: true),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_orders", x => x.service_order_id);
                    table.CheckConstraint("ck_service_orders_assignee_triple", "((current_assignee_name IS NULL AND current_assignee_admin_user_id IS NULL AND current_assignee_admin_user_home_node IS NULL) OR (current_assignee_name IS NOT NULL AND current_assignee_admin_user_id IS NOT NULL AND current_assignee_admin_user_home_node IS NOT NULL))");
                    table.CheckConstraint("ck_service_orders_assignee_values", "current_assignee_name IS NULL OR (btrim(current_assignee_name) <> '' AND current_assignee_admin_user_id > 0 AND btrim(current_assignee_admin_user_home_node) <> '')");
                    table.CheckConstraint("ck_service_orders_contact", "nullif(btrim(customer_phone_snapshot), '') IS NOT NULL OR nullif(btrim(customer_email_snapshot), '') IS NOT NULL");
                    table.CheckConstraint("ck_service_orders_created_by", "btrim(created_by_admin_name) <> '' AND created_by_admin_user_id > 0 AND btrim(created_by_admin_user_home_node) <> ''");
                    table.CheckConstraint("ck_service_orders_customer_name", "btrim(customer_name_snapshot) <> ''");
                    table.CheckConstraint("ck_service_orders_email", "customer_email_snapshot IS NULL OR btrim(customer_email_snapshot) <> ''");
                    table.CheckConstraint("ck_service_orders_last_actor_triple", "((last_status_changed_by_name IS NULL AND last_status_changed_by_admin_user_id IS NULL AND last_status_changed_by_admin_user_home_node IS NULL) OR (last_status_changed_by_name IS NOT NULL AND last_status_changed_by_admin_user_id IS NOT NULL AND last_status_changed_by_admin_user_home_node IS NOT NULL))");
                    table.CheckConstraint("ck_service_orders_last_actor_not_fictitious", "last_status_changed_by_name IS NULL OR btrim(last_status_changed_by_name) <> 'Sistema'");
                    table.CheckConstraint("ck_service_orders_last_actor_values", "last_status_changed_by_name IS NULL OR (btrim(last_status_changed_by_name) <> '' AND last_status_changed_by_admin_user_id > 0 AND btrim(last_status_changed_by_admin_user_home_node) <> '')");
                    table.CheckConstraint("ck_service_orders_last_status_at", "last_status_changed_at >= received_at");
                    table.CheckConstraint("ck_service_orders_phone", "customer_phone_snapshot IS NULL OR btrim(customer_phone_snapshot) <> ''");
                    table.CheckConstraint("ck_service_orders_promised_at", "promised_at IS NULL OR promised_at >= received_at");
                    table.CheckConstraint("ck_service_orders_replication", "btrim(origin_node) <> '' AND row_version > 0");
                    table.CheckConstraint("ck_service_orders_status", "status IN ('received', 'in_progress', 'ready', 'completed', 'cancelled')");
                    table.CheckConstraint("ck_service_orders_visible_code", "btrim(visible_code) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "service_order_assignment_events",
                schema: "service_orders",
                columns: table => new
                {
                    assignment_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    assignee_name = table.Column<string>(type: "text", nullable: false),
                    assignee_admin_user_id = table.Column<int>(type: "integer", nullable: false),
                    assignee_admin_user_home_node = table.Column<string>(type: "text", nullable: false),
                    performed_by_name = table.Column<string>(type: "text", nullable: false),
                    performed_by_admin_user_id = table.Column<int>(type: "integer", nullable: false),
                    performed_by_admin_user_home_node = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_order_assignment_events", x => x.assignment_event_id);
                    table.CheckConstraint("ck_service_order_assignment_events_action", "action IN ('assigned','unassigned')");
                    table.CheckConstraint("ck_service_order_assignment_events_actor", "btrim(performed_by_name) <> '' AND performed_by_admin_user_id > 0 AND btrim(performed_by_admin_user_home_node) <> ''");
                    table.CheckConstraint("ck_service_order_assignment_events_assignee", "btrim(assignee_name) <> '' AND assignee_admin_user_id > 0 AND btrim(assignee_admin_user_home_node) <> ''");
                    table.CheckConstraint("ck_service_order_assignment_events_replication", "btrim(origin_node) <> '' AND row_version > 0");
                    table.ForeignKey(
                        name: "fk_service_order_assignment_events_service_order_id",
                        column: x => x.service_order_id,
                        principalSchema: "service_orders",
                        principalTable: "service_orders",
                        principalColumn: "service_order_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_order_items",
                schema: "service_orders",
                columns: table => new
                {
                    service_order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_source_id = table.Column<int>(type: "integer", nullable: false),
                    service_source_node = table.Column<string>(type: "text", nullable: false),
                    service_name_snapshot = table.Column<string>(type: "text", nullable: false),
                    service_slug_snapshot = table.Column<string>(type: "text", nullable: false),
                    service_short_description_snapshot = table.Column<string>(type: "text", nullable: true),
                    service_description_snapshot = table.Column<string>(type: "text", nullable: true),
                    showcase_price_snapshot = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    sale_unit_snapshot = table.Column<string>(type: "text", nullable: true),
                    media_asset_id_snapshot = table.Column<Guid>(type: "uuid", nullable: true),
                    image_url_snapshot = table.Column<string>(type: "text", nullable: true),
                    image_alt_text_snapshot = table.Column<string>(type: "text", nullable: true),
                    requested_details = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    agreed_unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_order_items", x => x.service_order_item_id);
                    table.CheckConstraint("ck_service_order_items_agreed_price", "agreed_unit_price IS NULL OR agreed_unit_price >= 0");
                    table.CheckConstraint("ck_service_order_items_description", "service_short_description_snapshot IS NOT NULL OR service_description_snapshot IS NOT NULL");
                    table.CheckConstraint("ck_service_order_items_image_alt", "media_asset_id_snapshot IS NULL OR nullif(btrim(image_alt_text_snapshot), '') IS NOT NULL");
                    table.CheckConstraint("ck_service_order_items_name", "btrim(service_name_snapshot) <> ''");
                    table.CheckConstraint("ck_service_order_items_quantity", "quantity > 0");
                    table.CheckConstraint("ck_service_order_items_replication", "btrim(origin_node) <> '' AND row_version > 0");
                    table.CheckConstraint("ck_service_order_items_requested_details", "btrim(requested_details) <> ''");
                    table.CheckConstraint("ck_service_order_items_showcase_price", "showcase_price_snapshot IS NULL OR showcase_price_snapshot >= 0");
                    table.CheckConstraint("ck_service_order_items_slug", "service_slug_snapshot COLLATE \"C\" ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'");
                    table.CheckConstraint("ck_service_order_items_sort_order", "sort_order >= 0");
                    table.CheckConstraint("ck_service_order_items_source_id", "service_source_id > 0");
                    table.CheckConstraint("ck_service_order_items_source_node", "btrim(service_source_node) <> ''");
                    table.ForeignKey(
                        name: "fk_service_order_items_service_order_id",
                        column: x => x.service_order_id,
                        principalSchema: "service_orders",
                        principalTable: "service_orders",
                        principalColumn: "service_order_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_order_status_history",
                schema: "service_orders",
                columns: table => new
                {
                    status_history_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    performed_by_name = table.Column<string>(type: "text", nullable: true),
                    performed_by_admin_user_id = table.Column<int>(type: "integer", nullable: true),
                    performed_by_admin_user_home_node = table.Column<string>(type: "text", nullable: true),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_order_status_history", x => x.status_history_id);
                    table.CheckConstraint("ck_service_order_status_history_actor_triple", "((performed_by_name IS NULL AND performed_by_admin_user_id IS NULL AND performed_by_admin_user_home_node IS NULL) OR (performed_by_name IS NOT NULL AND performed_by_admin_user_id IS NOT NULL AND performed_by_admin_user_home_node IS NOT NULL))");
                    table.CheckConstraint("ck_service_order_status_history_actor_not_fictitious", "performed_by_name IS NULL OR btrim(performed_by_name) <> 'Sistema'");
                    table.CheckConstraint("ck_service_order_status_history_actor_values", "performed_by_name IS NULL OR (btrim(performed_by_name) <> '' AND performed_by_admin_user_id > 0 AND btrim(performed_by_admin_user_home_node) <> '')");
                    table.CheckConstraint("ck_service_order_status_history_change", "from_status IS NULL OR from_status <> to_status");
                    table.CheckConstraint("ck_service_order_status_history_from", "from_status IS NULL OR from_status IN ('received', 'in_progress', 'ready', 'completed', 'cancelled')");
                    table.CheckConstraint("ck_service_order_status_history_initial", "from_status IS NOT NULL OR to_status = 'received'");
                    table.CheckConstraint("ck_service_order_status_history_replication", "btrim(origin_node) <> '' AND row_version > 0");
                    table.CheckConstraint("ck_service_order_status_history_to", "to_status IN ('received', 'in_progress', 'ready', 'completed', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_service_order_status_history_service_order_id",
                        column: x => x.service_order_id,
                        principalSchema: "service_orders",
                        principalTable: "service_orders",
                        principalColumn: "service_order_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_service_order_assignment_events_order_occurred",
                schema: "service_orders",
                table: "service_order_assignment_events",
                columns: new[] { "service_order_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "uq_service_order_items_order_sort",
                schema: "service_orders",
                table: "service_order_items",
                columns: new[] { "service_order_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_service_order_series_node_code_year",
                schema: "service_orders",
                table: "service_order_series",
                columns: new[] { "node_code", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_service_order_status_history_order_occurred",
                schema: "service_orders",
                table: "service_order_status_history",
                columns: new[] { "service_order_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "idx_service_orders_received_at",
                schema: "service_orders",
                table: "service_orders",
                column: "received_at");

            migrationBuilder.CreateIndex(
                name: "idx_service_orders_status",
                schema: "service_orders",
                table: "service_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_service_orders_visible_code",
                schema: "service_orders",
                table: "service_orders",
                column: "visible_code",
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION service_orders.set_updated_at() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN NEW.updated_at := now(); RETURN NEW; END; $$;

                CREATE TRIGGER trg_service_orders_set_updated_at
                BEFORE UPDATE ON service_orders.service_orders
                FOR EACH ROW EXECUTE FUNCTION service_orders.set_updated_at();

                CREATE TRIGGER trg_service_order_items_set_updated_at
                BEFORE UPDATE ON service_orders.service_order_items
                FOR EACH ROW EXECUTE FUNCTION service_orders.set_updated_at();

                CREATE TRIGGER trg_service_order_assignment_events_set_updated_at
                BEFORE UPDATE ON service_orders.service_order_assignment_events
                FOR EACH ROW EXECUTE FUNCTION service_orders.set_updated_at();

                CREATE TRIGGER trg_service_order_status_history_set_updated_at
                BEFORE UPDATE ON service_orders.service_order_status_history
                FOR EACH ROW EXECUTE FUNCTION service_orders.set_updated_at();

                CREATE TRIGGER trg_service_order_series_set_updated_at
                BEFORE UPDATE ON service_orders.service_order_series
                FOR EACH ROW EXECUTE FUNCTION service_orders.set_updated_at();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_order_assignment_events",
                schema: "service_orders");

            migrationBuilder.DropTable(
                name: "service_order_items",
                schema: "service_orders");

            migrationBuilder.DropTable(
                name: "service_order_series",
                schema: "service_orders");

            migrationBuilder.DropTable(
                name: "service_order_status_history",
                schema: "service_orders");

            migrationBuilder.DropTable(
                name: "service_orders",
                schema: "service_orders");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS service_orders.set_updated_at();");
        }
    }
}
