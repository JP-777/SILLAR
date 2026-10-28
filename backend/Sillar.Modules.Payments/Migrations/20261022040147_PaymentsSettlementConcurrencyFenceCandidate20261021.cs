using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Payments.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Candidata de la valla transaccional propia de M11. Se conserva para
    /// revisión estática; este cambio no autoriza ejecutarla en PostgreSQL.
    /// </remarks>
    public partial class PaymentsSettlementConcurrencyFenceCandidate20261021 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provider_settlement_concurrency_fences",
                schema: "payments",
                columns: table => new
                {
                    fence_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, collation: "C"),
                    external_settlement_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, collation: "C"),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_settlement_concurrency_fences", x => x.fence_sha256);
                    table.CheckConstraint("ck_payment_settlement_fence_identity", "btrim(external_settlement_id) <> ''");
                    table.CheckConstraint("ck_payment_settlement_fence_revision", "revision > 0");
                    table.CheckConstraint("ck_payment_settlement_fence_sha256", "fence_sha256 COLLATE \"C\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_payment_settlement_fence_timestamps", "updated_at >= created_at");
                });

            migrationBuilder.CreateIndex(
                name: "idx_payment_settlement_fence_identity",
                schema: "payments",
                table: "provider_settlement_concurrency_fences",
                column: "external_settlement_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_settlement_concurrency_fences",
                schema: "payments");
        }
    }
}
