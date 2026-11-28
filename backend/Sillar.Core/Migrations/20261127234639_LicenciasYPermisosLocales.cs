using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Core.Migrations
{
    /// <inheritdoc />
    public partial class LicenciasYPermisosLocales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "module_licenses",
                schema: "core",
                columns: table => new
                {
                    module_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, collation: "C"),
                    is_licensed = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_module_licenses", x => x.module_code);
                    table.CheckConstraint("ck_module_license_code", "module_code ~ '^[a-z][a-z0-9_]{0,39}$'");
                });

            migrationBuilder.CreateTable(
                name: "module_operation_permissions",
                schema: "core",
                columns: table => new
                {
                    admin_user_id = table.Column<int>(type: "integer", nullable: false),
                    module_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, collation: "C"),
                    operation_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, collation: "C"),
                    allowed = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_module_operation_permissions", x => new { x.admin_user_id, x.module_code, x.operation_code });
                    table.CheckConstraint("ck_module_permission_module", "module_code ~ '^[a-z][a-z0-9_]{0,39}$'");
                    table.CheckConstraint("ck_module_permission_operation", "operation_code ~ '^[a-z][a-z0-9_]{0,59}$'");
                    table.ForeignKey(
                        name: "fk_module_permission_admin_user",
                        column: x => x.admin_user_id,
                        principalSchema: "core",
                        principalTable: "admin_users",
                        principalColumn: "admin_user_id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM core.module_licenses)
                        OR EXISTS (SELECT 1 FROM core.module_operation_permissions) THEN
                        RAISE EXCEPTION 'No se revierte la autoridad local: existen licencias o permisos conservados. No se ha borrado nada.';
                    END IF;
                END $guard$;
                """);
            migrationBuilder.DropTable(
                name: "module_licenses",
                schema: "core");

            migrationBuilder.DropTable(
                name: "module_operation_permissions",
                schema: "core");
        }
    }
}
