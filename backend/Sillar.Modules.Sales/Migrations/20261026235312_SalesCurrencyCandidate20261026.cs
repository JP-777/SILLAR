using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Sales.Migrations
{
    /// <summary>
    /// CANDIDATA, NO APLICADA. Moneda ISO 4217 explícita por pedido y por pago.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La moneda nace NULL y se queda NULL en lo que ya existe.</b> Ningún pedido ni pago
    /// anterior recibe PEN ni ninguna otra: un relleno sería un hecho inventado que después
    /// no se distingue de uno real.
    /// </para>
    /// <para>
    /// Los pedidos nuevos no pueden nacer sin moneda, pero eso no lo impone la base —tiene
    /// que admitir los NULL históricos— sino la guarda de <c>CreadorDePedidos</c>.
    /// </para>
    /// <para>
    /// Es la única de las tres migraciones de esta entrega que no lleva una decisión sin
    /// ratificar: <c>SalesPaymentSourceCandidate20261026</c> y
    /// <c>SalesPaymentRetentionCandidate20261026</c> sí, y van aparte para poder caer solas.
    /// </para>
    /// </remarks>
    public partial class SalesCurrencyCandidate20261026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.Sql("""
                COMMENT ON COLUMN sales.orders.currency_code IS
                    'ISO 4217 del total. NULL = desconocida; nunca PEN implicito ni relleno.';
                COMMENT ON COLUMN sales.order_payments.currency_code IS
                    'ISO 4217 del importe. NULL = desconocida; nunca PEN implicito ni relleno.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_currency_code_iso",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_payments_currency_code_iso",
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
        }
    }
}
