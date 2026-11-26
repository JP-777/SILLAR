using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Sales.Migrations
{
    /// <summary>
    /// CANDIDATA, NO APLICADA. <b>DECISIÓN CANDIDATA, NO RATIFICADA:</b>
    /// <c>fk_order_payments_order_id</c> pasa de CASCADE a RESTRICT.
    /// </summary>
    /// <remarks>
    /// Retención financiera: borrar físicamente un pedido no puede llevarse sus pagos. El
    /// pedido no se borra nunca —baja lógica—, así que en la operación normal no cambia
    /// nada; cambia qué ocurre ante un borrado indebido. Va sola para poder caer sola.
    /// </remarks>
    public partial class SalesPaymentRetentionCandidate20261026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_order_payments_order_id",
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
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_order_payments_order_id",
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
