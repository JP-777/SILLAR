using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Sales.Migrations
{
    /// <summary>
    /// CANDIDATA, NO APLICADA. Moneda explícita por pedido y por pago, y origen del pago
    /// separado del medio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La moneda nace NULL y se queda NULL en lo que ya existe.</b> Ningún pedido ni
    /// pago anterior recibe PEN ni ninguna otra: no hay autoridad de moneda ratificada, y
    /// un relleno sería un hecho inventado que después no se distingue de uno real.
    /// </para>
    /// <para>
    /// <b>El origen sí se rellena, y no es inferencia.</b> Cada fila existente de
    /// <c>order_payments</c> cumple <c>ck_order_payments_atribucion_local_positiva</c> y
    /// <c>ck_order_payments_atribucion_home_node_no_vacio</c>: la base ya garantiza que la
    /// registró una persona. <c>staff</c> es lo que esos CHECK prueban.
    /// </para>
    /// <para>
    /// <b>La FK de pagos pasa de CASCADE a RESTRICT</b> por retención financiera: borrar
    /// físicamente un pedido no puede llevarse sus pagos por delante.
    /// </para>
    /// </remarks>
    public partial class SalesCurrencyAndSourceCandidate20261026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_order_payments_order_id",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                schema: "sales",
                table: "orders",
                type: "character(3)",
                nullable: true,
                collation: "C");

            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                schema: "sales",
                table: "order_payments",
                type: "character(3)",
                nullable: true,
                collation: "C");

            // Nace anulable, se rellena con lo que los CHECK de atribución ya prueban y
            // solo entonces pasa a NOT NULL. Sin valor por defecto: un escritor futuro
            // tiene que decir el origen, no heredarlo.
            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "sales",
                table: "order_payments",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("UPDATE sales.order_payments SET source = 'staff' WHERE source IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "source",
                schema: "sales",
                table: "order_payments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_currency_code_iso",
                schema: "sales",
                table: "orders",
                sql: "currency_code ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_payments_currency_code_iso",
                schema: "sales",
                table: "order_payments",
                sql: "currency_code ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_payments_source",
                schema: "sales",
                table: "order_payments",
                sql: "source IN ('staff')");

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
                COMMENT ON COLUMN sales.orders.currency_code IS
                    'ISO 4217 del total. NULL = desconocida; nunca PEN implicito ni relleno.';
                COMMENT ON COLUMN sales.order_payments.currency_code IS
                    'ISO 4217 del importe. NULL = desconocida; nunca PEN implicito ni relleno.';
                COMMENT ON COLUMN sales.order_payments.source IS
                    'Quien asienta el pago (staff). No es el medio: eso es method.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_order_payments_order_id",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_currency_code_iso",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_payments_currency_code_iso",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_payments_source",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.DropColumn(
                name: "currency_code",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "currency_code",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.DropColumn(
                name: "source",
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
