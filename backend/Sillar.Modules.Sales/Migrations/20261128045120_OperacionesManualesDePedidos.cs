using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Sales.Migrations
{
    /// <inheritdoc />
    public partial class OperacionesManualesDePedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_order_payments_order_id",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.AddColumn<string>(
                name: "reason",
                schema: "sales",
                table: "order_status_changes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "can_fulfill",
                schema: "sales",
                table: "order_payments",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "manual_request_id",
                schema: "sales",
                table: "order_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_order_payments_manual_request",
                schema: "sales",
                table: "order_payments",
                columns: new[] { "order_id", "manual_request_id" },
                unique: true,
                filter: "manual_request_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_order_payments_order_id",
                schema: "sales",
                table: "order_payments",
                column: "order_id",
                principalSchema: "sales",
                principalTable: "orders",
                principalColumn: "order_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION sales.proteger_hechos_manuales() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_TABLE_NAME = 'order_payments' THEN
                    IF ROW(OLD.order_id, OLD.method, OLD.amount, OLD.currency_code, OLD.registered_at,
                           OLD.registered_by, OLD.registered_by_admin_user_local_id, OLD.registered_by_admin_user_home_node,
                           OLD.manual_request_id, OLD.can_fulfill, OLD.reference, OLD.was_late, OLD.external_settlement_id)
                       IS DISTINCT FROM
                       ROW(NEW.order_id, NEW.method, NEW.amount, NEW.currency_code, NEW.registered_at,
                           NEW.registered_by, NEW.registered_by_admin_user_local_id, NEW.registered_by_admin_user_home_node,
                           NEW.manual_request_id, NEW.can_fulfill, NEW.reference, NEW.was_late, NEW.external_settlement_id) THEN
                      RAISE EXCEPTION 'El pago y su atribución son hechos inmutables.' USING ERRCODE = '23514';
                    END IF;
                  ELSE
                    IF ROW(OLD.order_id, OLD.from_status, OLD.to_status, OLD.changed_at, OLD.reason,
                           OLD.changed_by, OLD.changed_by_admin_user_local_id, OLD.changed_by_admin_user_home_node)
                       IS DISTINCT FROM
                       ROW(NEW.order_id, NEW.from_status, NEW.to_status, NEW.changed_at, NEW.reason,
                           NEW.changed_by, NEW.changed_by_admin_user_local_id, NEW.changed_by_admin_user_home_node) THEN
                      RAISE EXCEPTION 'El historial del pedido y su atribución son inmutables.' USING ERRCODE = '23514';
                    END IF;
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER trg_order_payments_hecho_inmutable BEFORE UPDATE ON sales.order_payments
                  FOR EACH ROW EXECUTE FUNCTION sales.proteger_hechos_manuales();
                CREATE TRIGGER trg_order_status_changes_hecho_inmutable BEFORE UPDATE ON sales.order_status_changes
                  FOR EACH ROW EXECUTE FUNCTION sales.proteger_hechos_manuales();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM sales.order_payments)
                     OR EXISTS (SELECT 1 FROM sales.order_status_changes WHERE reason IS NOT NULL) THEN
                    RAISE EXCEPTION 'No se puede retirar la evidencia ni la retención de pagos conservados.';
                  END IF;
                END $$;
                DROP TRIGGER trg_order_payments_hecho_inmutable ON sales.order_payments;
                DROP TRIGGER trg_order_status_changes_hecho_inmutable ON sales.order_status_changes;
                DROP FUNCTION sales.proteger_hechos_manuales();
                """);
            migrationBuilder.DropForeignKey(
                name: "fk_order_payments_order_id",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.DropIndex(
                name: "uq_order_payments_manual_request",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.DropColumn(
                name: "reason",
                schema: "sales",
                table: "order_status_changes");

            migrationBuilder.DropColumn(
                name: "can_fulfill",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.DropColumn(
                name: "manual_request_id",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.AddForeignKey(
                name: "fk_order_payments_order_id",
                schema: "sales",
                table: "order_payments",
                column: "order_id",
                principalSchema: "sales",
                principalTable: "orders",
                principalColumn: "order_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
