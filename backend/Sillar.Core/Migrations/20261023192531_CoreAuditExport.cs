using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Core.Migrations
{
    /// <summary>
    /// Fase A, candidata NO APLICADA: amplía <c>ck_audit_log_action</c> con <c>'export'</c>.
    /// </summary>
    /// <remarks>
    /// <b>Riesgo de <c>Down</c>.</b> Volver a crear el CHECK sin <c>'export'</c>
    /// valida las filas existentes: si <c>core.audit_log</c> ya tiene alguna con
    /// <c>action = 'export'</c>, PostgreSQL rechaza el <c>ADD CONSTRAINT</c>
    /// (23514) y la reversión aborta. Borrar esas filas para poder revertir
    /// destruiría evidencia de auditoría: no es una salida. Una reversión con
    /// exportaciones registradas necesita una decisión aparte, no esta migración.
    /// </remarks>
    public partial class CoreAuditExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_log_action",
                schema: "core",
                table: "audit_log");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_log_action",
                schema: "core",
                table: "audit_log",
                sql: "action IN ('create', 'update', 'delete', 'activate', 'deactivate', 'login', 'login_failed', 'logout', 'setup', 'email_send', 'export')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_log_action",
                schema: "core",
                table: "audit_log");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_log_action",
                schema: "core",
                table: "audit_log",
                sql: "action IN ('create', 'update', 'delete', 'activate', 'deactivate', 'login', 'login_failed', 'logout', 'setup', 'email_send')");
        }
    }
}
