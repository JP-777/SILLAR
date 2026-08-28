using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Core.Migrations
{
    /// <inheritdoc />
    public partial class CoreEmailSettings : Migration
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
                sql: "action IN ('create', 'update', 'delete', 'activate', 'deactivate', 'login', 'login_failed', 'logout', 'setup', 'email_send')");
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
                sql: "action IN ('create', 'update', 'delete', 'activate', 'deactivate', 'login', 'login_failed', 'logout', 'setup')");
        }
    }
}
