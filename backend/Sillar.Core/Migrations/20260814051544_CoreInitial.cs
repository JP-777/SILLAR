using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sillar.Core.Migrations
{
    /// <summary>
    /// Crea el schema <c>core</c> completo: las nueve tablas del SPEC, las
    /// colaciones <c>core.es_ci</c> y <c>core.es_search</c>, y el trigger de
    /// <c>updated_at</c>.
    /// </summary>
    /// <remarks>
    /// Generada con <c>dotnet ef migrations add</c> y revisada a mano (ADR-009).
    /// Lo añadido después de generarla, y por qué, está comentado en el sitio
    /// donde aparece:
    ///
    ///   · <c>core.es_ci</c> aplicada a <c>admin_users.email</c> y a
    ///     <c>site_settings.setting_key</c> con SQL explícito;
    ///   · la función <c>core.set_updated_at()</c> y sus seis triggers;
    ///   · la reversión de todo ello en <c>Down</c>.
    ///
    /// <c>core.es_search</c> se crea pero todavía no la usa ninguna columna:
    /// vive aquí porque es infraestructura compartida y la estrenará M01.
    /// </remarks>
    public partial class CoreInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:core.es_ci", "es-PE-u-ks-level2,es-PE-u-ks-level2,icu,False")
                .Annotation("Npgsql:CollationDefinition:core.es_search", "es-PE-u-ks-level1,es-PE-u-ks-level1,icu,False");

            migrationBuilder.CreateTable(
                name: "admin_users",
                schema: "core",
                columns: table => new
                {
                    admin_user_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_users", x => x.admin_user_id);
                    table.CheckConstraint("ck_admin_users_email_not_empty", "btrim(email) <> ''");
                    table.CheckConstraint("ck_admin_users_failed_login_count", "failed_login_count >= 0");
                    table.CheckConstraint("ck_admin_users_full_name_not_empty", "btrim(full_name) <> ''");
                    table.CheckConstraint("ck_admin_users_password_hash_not_empty", "btrim(password_hash) <> ''");
                    table.CheckConstraint("ck_admin_users_role", "role IN ('super_admin', 'admin', 'editor')");
                });

            migrationBuilder.CreateTable(
                name: "installation",
                schema: "core",
                columns: table => new
                {
                    installation_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    singleton = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    business_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    installation_key = table.Column<Guid>(type: "uuid", nullable: false),
                    product_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    license_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    licensed_until = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    is_setup_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installation", x => x.installation_id);
                    table.CheckConstraint("ck_installation_business_name_not_empty", "btrim(business_name) <> ''");
                    table.CheckConstraint("ck_installation_license_type", "license_type IN ('trial', 'subscription', 'perpetual')");
                    table.CheckConstraint("ck_installation_product_version_not_empty", "btrim(product_version) <> ''");
                    table.CheckConstraint("ck_installation_singleton", "singleton");
                });

            migrationBuilder.CreateTable(
                name: "modules",
                schema: "core",
                columns: table => new
                {
                    module_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_core = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modules", x => x.module_id);
                    table.CheckConstraint("ck_modules_code_format", "code ~ '^[a-z][a-z0-9_]{1,39}$'");
                    table.CheckConstraint("ck_modules_code_not_empty", "btrim(code) <> ''");
                    table.CheckConstraint("ck_modules_display_name_not_empty", "btrim(display_name) <> ''");
                    table.CheckConstraint("ck_modules_version_not_empty", "btrim(version) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "site_settings",
                schema: "core",
                columns: table => new
                {
                    site_setting_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    setting_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    setting_value = table.Column<string>(type: "text", nullable: false),
                    value_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    is_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_settings", x => x.site_setting_id);
                    table.CheckConstraint("ck_site_settings_key_not_empty", "btrim(setting_key) <> ''");
                    table.CheckConstraint("ck_site_settings_value_type", "value_type IN ('text', 'number', 'boolean', 'url', 'email', 'json')");
                });

            migrationBuilder.CreateTable(
                name: "admin_sessions",
                schema: "core",
                columns: table => new
                {
                    admin_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_user_id = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    csrf_token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_sessions", x => x.admin_session_id);
                    table.CheckConstraint("ck_admin_sessions_csrf_token_hash_not_empty", "btrim(csrf_token_hash) <> ''");
                    table.CheckConstraint("ck_admin_sessions_token_hash_not_empty", "btrim(token_hash) <> ''");
                    table.ForeignKey(
                        name: "fk_admin_sessions_admin_user_id",
                        column: x => x.admin_user_id,
                        principalSchema: "core",
                        principalTable: "admin_users",
                        principalColumn: "admin_user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "core",
                columns: table => new
                {
                    audit_log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    admin_user_id = table.Column<int>(type: "integer", nullable: true),
                    admin_user_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    module_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    entity_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    entity_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.audit_log_id);
                    table.CheckConstraint("ck_audit_log_action", "action IN ('create', 'update', 'delete', 'activate', 'deactivate', 'login', 'login_failed', 'logout', 'setup')");
                    table.ForeignKey(
                        name: "fk_audit_log_admin_user_id",
                        column: x => x.admin_user_id,
                        principalSchema: "core",
                        principalTable: "admin_users",
                        principalColumn: "admin_user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                schema: "core",
                columns: table => new
                {
                    media_asset_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    stored_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    original_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    relative_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    alt_text = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    owner_module_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_orphan = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.media_asset_id);
                    table.CheckConstraint("ck_media_assets_mime_type_not_empty", "btrim(mime_type) <> ''");
                    table.CheckConstraint("ck_media_assets_relative_path_not_empty", "btrim(relative_path) <> ''");
                    table.CheckConstraint("ck_media_assets_size_bytes", "size_bytes > 0");
                    table.CheckConstraint("ck_media_assets_stored_name_not_empty", "btrim(stored_name) <> ''");
                    table.ForeignKey(
                        name: "fk_media_assets_created_by",
                        column: x => x.created_by,
                        principalSchema: "core",
                        principalTable: "admin_users",
                        principalColumn: "admin_user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "module_activations",
                schema: "core",
                columns: table => new
                {
                    module_activation_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    module_id = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    notes = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_module_activations", x => x.module_activation_id);
                    table.ForeignKey(
                        name: "fk_module_activations_module_id",
                        column: x => x.module_id,
                        principalSchema: "core",
                        principalTable: "modules",
                        principalColumn: "module_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "module_dependencies",
                schema: "core",
                columns: table => new
                {
                    module_dependency_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    module_id = table.Column<int>(type: "integer", nullable: false),
                    depends_on_module_id = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_module_dependencies", x => x.module_dependency_id);
                    table.CheckConstraint("ck_module_dependencies_kind", "kind IN ('hard', 'soft')");
                    table.CheckConstraint("ck_module_dependencies_no_self", "module_id <> depends_on_module_id");
                    table.ForeignKey(
                        name: "fk_module_dependencies_depends_on_module_id",
                        column: x => x.depends_on_module_id,
                        principalSchema: "core",
                        principalTable: "modules",
                        principalColumn: "module_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_module_dependencies_module_id",
                        column: x => x.module_id,
                        principalSchema: "core",
                        principalTable: "modules",
                        principalColumn: "module_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ================================================================
            // Colación core.es_ci en las columnas que identifican
            //
            // Escrito a mano: el proveedor Npgsql genera COLLATE "core.es_ci",
            // entrecomillando el nombre calificado como si fuera un identificador
            // único, y PostgreSQL entonces busca una colación llamada
            // literalmente «core.es_ci», que no existe.
            //
            // Va antes de crear los índices: cambiar la colación de una columna
            // reconstruye sus índices, así que se crean ya en su forma final.
            // ================================================================
            migrationBuilder.Sql(
                """
                ALTER TABLE core.admin_users
                    ALTER COLUMN email TYPE character varying(150) COLLATE core.es_ci;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE core.site_settings
                    ALTER COLUMN setting_key TYPE character varying(100) COLLATE core.es_ci;
                """);

            migrationBuilder.CreateIndex(
                name: "idx_admin_sessions_expires_at",
                schema: "core",
                table: "admin_sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_admin_sessions_user",
                schema: "core",
                table: "admin_sessions",
                column: "admin_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_admin_sessions_token_hash",
                schema: "core",
                table: "admin_sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_admin_users_is_active",
                schema: "core",
                table: "admin_users",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "uq_admin_users_email",
                schema: "core",
                table: "admin_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_audit_log_admin_user_id",
                schema: "core",
                table: "audit_log",
                column: "admin_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_audit_log_module_code",
                schema: "core",
                table: "audit_log",
                column: "module_code");

            migrationBuilder.CreateIndex(
                name: "idx_audit_log_occurred_at",
                schema: "core",
                table: "audit_log",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "uq_installation_installation_key",
                schema: "core",
                table: "installation",
                column: "installation_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_installation_singleton",
                schema: "core",
                table: "installation",
                column: "singleton",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_media_assets_checksum",
                schema: "core",
                table: "media_assets",
                column: "checksum");

            migrationBuilder.CreateIndex(
                name: "idx_media_assets_created_by",
                schema: "core",
                table: "media_assets",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "idx_media_assets_owner_module_code",
                schema: "core",
                table: "media_assets",
                column: "owner_module_code");

            migrationBuilder.CreateIndex(
                name: "uq_media_assets_stored_name",
                schema: "core",
                table: "media_assets",
                column: "stored_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_module_activations_module_id",
                schema: "core",
                table: "module_activations",
                column: "module_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_module_dependencies_depends_on_module_id",
                schema: "core",
                table: "module_dependencies",
                column: "depends_on_module_id");

            migrationBuilder.CreateIndex(
                name: "uq_module_dependencies_module_depends_on",
                schema: "core",
                table: "module_dependencies",
                columns: new[] { "module_id", "depends_on_module_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_modules_code",
                schema: "core",
                table: "modules",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_site_settings_setting_key",
                schema: "core",
                table: "site_settings",
                column: "setting_key",
                unique: true);

            // ================================================================
            // updated_at automático
            //
            // Escrito a mano: EF Core no genera triggers. La función vive en el
            // schema del módulo y no en public, de modo que desaparece al soltar
            // core y no deja restos en la base de datos. Cada módulo tendrá la
            // suya dentro de su propio schema.
            // ================================================================
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION core.set_updated_at()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    NEW.updated_at := now();
                    RETURN NEW;
                END;
                $$;
                """);

            // Solo las tablas que tienen updated_at. Quedan fuera, y es
            // correcto: module_dependencies y admin_sessions no se editan, y
            // audit_log lleva occurred_at porque un registro de auditoría no se
            // modifica nunca.
            foreach (var table in TablesWithUpdatedAt)
            {
                migrationBuilder.Sql(
                    $"""
                     CREATE TRIGGER trg_{table}_set_updated_at
                         BEFORE UPDATE ON core.{table}
                         FOR EACH ROW
                         EXECUTE FUNCTION core.set_updated_at();
                     """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_sessions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "core");

            migrationBuilder.DropTable(
                name: "installation",
                schema: "core");

            migrationBuilder.DropTable(
                name: "media_assets",
                schema: "core");

            migrationBuilder.DropTable(
                name: "module_activations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "module_dependencies",
                schema: "core");

            migrationBuilder.DropTable(
                name: "site_settings",
                schema: "core");

            migrationBuilder.DropTable(
                name: "admin_users",
                schema: "core");

            migrationBuilder.DropTable(
                name: "modules",
                schema: "core");

            // Los triggers se van con sus tablas; la función y las colaciones,
            // no. Deshacer la migración tiene que dejar el schema como estaba.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS core.set_updated_at();");
            migrationBuilder.Sql("DROP COLLATION IF EXISTS core.es_ci;");
            migrationBuilder.Sql("DROP COLLATION IF EXISTS core.es_search;");
        }

        /// <summary>
        /// Tablas de CORE con columna <c>updated_at</c> mantenida por trigger.
        /// </summary>
        private static readonly string[] TablesWithUpdatedAt =
        [
            "installation",
            "modules",
            "module_activations",
            "admin_users",
            "site_settings",
            "media_assets"
        ];
    }
}
