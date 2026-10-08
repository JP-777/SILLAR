using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sillar.Modules.ServiceOrders.Data;

#nullable disable

namespace Sillar.Modules.ServiceOrders.Migrations
{
    [DbContext(typeof(ServiceOrdersDbContext))]
    [Migration("20261007202000_AddServiceOrderCreateIdempotency")]
    public partial class AddServiceOrderCreateIdempotency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "create_idempotency_key",
                schema: "service_orders",
                table: "service_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE service_orders.service_orders
                   SET create_idempotency_key = service_order_id
                 WHERE create_idempotency_key IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "create_idempotency_key",
                schema: "service_orders",
                table: "service_orders",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_orders_create_idempotency_key",
                schema: "service_orders",
                table: "service_orders",
                sql: "create_idempotency_key <> '00000000-0000-0000-0000-000000000000'::uuid");

            migrationBuilder.CreateIndex(
                name: "uq_service_orders_create_idempotency_key",
                schema: "service_orders",
                table: "service_orders",
                column: "create_idempotency_key",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_service_orders_create_idempotency_key",
                schema: "service_orders",
                table: "service_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_service_orders_create_idempotency_key",
                schema: "service_orders",
                table: "service_orders");

            migrationBuilder.DropColumn(
                name: "create_idempotency_key",
                schema: "service_orders",
                table: "service_orders");
        }
    }
}
