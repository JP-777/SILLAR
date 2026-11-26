using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Portal.Migrations
{
    /// <inheritdoc />
    public partial class HistorialDeInstalacionPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF conserva __migrations en este schema; no hay tablas de negocio.
            migrationBuilder.EnsureSchema(name: "portal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // EF aún necesita su historial durante el downgrade. La retirada
            // completa y transaccional corresponde a modules/portal/99_drop.sql.
        }
    }
}
