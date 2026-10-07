using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Tracking.Migrations
{
    /// <inheritdoc />
    public partial class TrackingInitial : Migration
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

                    IF to_regclass('service_orders.service_orders') IS NULL THEN
                        faltan := array_append(
                            faltan,
                            'M05b Servicios — Órdenes (schema service_orders)');
                    END IF;

                    IF cardinality(faltan) > 0 THEN
                        RAISE EXCEPTION
                            'M06 Seguimiento no se puede instalar porque falta: %. Instala eso antes y repite la instalación de M06.',
                            array_to_string(faltan, ' y ')
                            USING ERRCODE = 'undefined_table';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.EnsureSchema(
                name: "tracking");

            migrationBuilder.CreateTable(
                name: "order_tracking",
                schema: "tracking",
                columns: table => new
                {
                    order_tracking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    board_priority = table.Column<int>(type: "integer", nullable: true),
                    internal_due_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    pinned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_touched_by_name = table.Column<string>(type: "text", nullable: true),
                    last_touched_by_admin_user_id = table.Column<int>(type: "integer", nullable: true),
                    last_touched_by_admin_user_home_node = table.Column<string>(type: "text", nullable: true),
                    last_touched_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_tracking", x => x.order_tracking_id);
                    table.CheckConstraint("ck_order_tracking_last_touched_not_fictitious", "last_touched_by_name IS NULL OR btrim(last_touched_by_name) <> 'Sistema'");
                    table.CheckConstraint("ck_order_tracking_last_touched_triple", "((last_touched_by_name IS NULL AND last_touched_by_admin_user_id IS NULL AND last_touched_by_admin_user_home_node IS NULL) OR (last_touched_by_name IS NOT NULL AND last_touched_by_admin_user_id IS NOT NULL AND last_touched_by_admin_user_home_node IS NOT NULL))");
                    table.CheckConstraint("ck_order_tracking_last_touched_values", "last_touched_by_name IS NULL OR (btrim(last_touched_by_name) <> '' AND last_touched_by_admin_user_id > 0 AND btrim(last_touched_by_admin_user_home_node) <> '')");
                    table.CheckConstraint("ck_order_tracking_priority", "board_priority IS NULL OR board_priority >= 0");
                    table.CheckConstraint("ck_order_tracking_replication", "btrim(origin_node) <> '' AND row_version > 0");
                });

            migrationBuilder.CreateTable(
                name: "tracking_notes",
                schema: "tracking",
                columns: table => new
                {
                    tracking_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_tracking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    author_name = table.Column<string>(type: "text", nullable: true),
                    author_admin_user_id = table.Column<int>(type: "integer", nullable: true),
                    author_admin_user_home_node = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tracking_notes", x => x.tracking_note_id);
                    table.CheckConstraint("ck_tracking_notes_author_not_fictitious", "author_name IS NULL OR btrim(author_name) <> 'Sistema'");
                    table.CheckConstraint("ck_tracking_notes_author_triple", "((author_name IS NULL AND author_admin_user_id IS NULL AND author_admin_user_home_node IS NULL) OR (author_name IS NOT NULL AND author_admin_user_id IS NOT NULL AND author_admin_user_home_node IS NOT NULL))");
                    table.CheckConstraint("ck_tracking_notes_author_values", "author_name IS NULL OR (btrim(author_name) <> '' AND author_admin_user_id > 0 AND btrim(author_admin_user_home_node) <> '')");
                    table.CheckConstraint("ck_tracking_notes_body", "btrim(body) <> ''");
                    table.CheckConstraint("ck_tracking_notes_replication", "btrim(origin_node) <> '' AND row_version > 0");
                    table.ForeignKey(
                        name: "fk_tracking_notes_order_tracking_id",
                        column: x => x.order_tracking_id,
                        principalSchema: "tracking",
                        principalTable: "order_tracking",
                        principalColumn: "order_tracking_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_order_tracking_service_order_id",
                schema: "tracking",
                table: "order_tracking",
                column: "service_order_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_order_tracking_service_order_id",
                schema: "tracking",
                table: "order_tracking",
                column: "service_order_id",
                principalSchema: "service_orders",
                principalTable: "service_orders",
                principalColumn: "service_order_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "idx_tracking_notes_order_created",
                schema: "tracking",
                table: "tracking_notes",
                columns: new[] { "order_tracking_id", "created_at" });

            migrationBuilder.Sql(
                """
                CREATE FUNCTION tracking.set_updated_at()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    NEW.updated_at := now();
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_order_tracking_set_updated_at
                BEFORE UPDATE ON tracking.order_tracking
                FOR EACH ROW
                EXECUTE FUNCTION tracking.set_updated_at();

                CREATE TRIGGER trg_tracking_notes_set_updated_at
                BEFORE UPDATE ON tracking.tracking_notes
                FOR EACH ROW
                EXECUTE FUNCTION tracking.set_updated_at();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tracking_notes",
                schema: "tracking");

            migrationBuilder.DropTable(
                name: "order_tracking",
                schema: "tracking");

            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS tracking.set_updated_at();");
        }
    }
}
