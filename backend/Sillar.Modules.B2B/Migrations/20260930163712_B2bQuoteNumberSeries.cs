using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.B2B.Migrations
{
    /// <inheritdoc />
    public partial class B2bQuoteNumberSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quote_number_series",
                schema: "b2b",
                columns: table => new
                {
                    series_code = table.Column<string>(type: "text", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_value = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_number_series", x => new { x.series_code, x.year });
                    table.CheckConstraint("ck_quote_number_series_last_value", "last_value >= 0");
                    table.CheckConstraint("ck_quote_number_series_series_code", "series_code ~ '^[A-Z]$'");
                    table.CheckConstraint("ck_quote_number_series_year", "year BETWEEN 2000 AND 9999");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quote_number_series",
                schema: "b2b");
        }
    }
}
