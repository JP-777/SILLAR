using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Sales.Migrations
{
    /// <inheritdoc />
    public partial class IdempotenciaDeLiquidacionExterna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_settlement_id",
                schema: "sales",
                table: "order_payments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                collation: "C");

            migrationBuilder.CreateIndex(
                name: "uq_order_payments_external_settlement_id",
                schema: "sales",
                table: "order_payments",
                column: "external_settlement_id",
                unique: true,
                filter: "external_settlement_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_payments_external_settlement_id_no_vacio",
                schema: "sales",
                table: "order_payments",
                sql: "external_settlement_id IS NULL OR external_settlement_id ~ '[^[:space:]]'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_order_payments_external_settlement_id",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_payments_external_settlement_id_no_vacio",
                schema: "sales",
                table: "order_payments");

            migrationBuilder.DropColumn(
                name: "external_settlement_id",
                schema: "sales",
                table: "order_payments");
        }
    }
}
