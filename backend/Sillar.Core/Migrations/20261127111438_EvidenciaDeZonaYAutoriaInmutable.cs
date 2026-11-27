using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sillar.Core.Migrations
{
    /// <inheritdoc />
    public partial class EvidenciaDeZonaYAutoriaInmutable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_audit_log_admin_user_id",
                schema: "core",
                table: "audit_log");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_log_action",
                schema: "core",
                table: "audit_log");

            migrationBuilder.AddColumn<string>(
                name: "admin_user_home_node",
                schema: "core",
                table: "audit_log",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "admin_user_name",
                schema: "core",
                table: "audit_log",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "installation_time_zone_attestations",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    attestation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_key = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_version = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    attested_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    attested_by_admin_user_local_id = table.Column<int>(type: "integer", nullable: false),
                    attested_by_admin_user_home_node = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    attested_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    authority_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installation_time_zone_attestations", x => x.id);
                    table.CheckConstraint("ck_time_zone_actor", "attested_by_admin_user_local_id > 0 AND btrim(attested_by_name) <> '' AND btrim(attested_by_admin_user_home_node) <> ''");
                    table.CheckConstraint("ck_time_zone_source", "btrim(zone_id) <> '' AND btrim(source_name) <> '' AND btrim(source_version) <> '' AND btrim(authority_version) <> ''");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_log_action",
                schema: "core",
                table: "audit_log",
                sql: "action IN ('create', 'update', 'delete', 'activate', 'deactivate', 'login', 'login_failed', 'logout', 'setup', 'email_send', 'export')");

            // No se inventa la atribución que falta en filas históricas. NOT VALID
            // conserva esas filas y exige la fotografía completa en escrituras nuevas.
            migrationBuilder.Sql("""
                ALTER TABLE core.audit_log ADD CONSTRAINT ck_audit_log_actor_snapshot CHECK (
                  (admin_user_id IS NULL AND admin_user_name IS NULL AND admin_user_home_node IS NULL) OR
                  (admin_user_id IS NOT NULL AND admin_user_id > 0 AND admin_user_name IS NOT NULL AND btrim(admin_user_name) <> ''
                   AND admin_user_home_node IS NOT NULL AND btrim(admin_user_home_node) <> '')
                ) NOT VALID;
                CREATE FUNCTION core.impedir_cambio_autoria_auditoria() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                  IF (NEW.admin_user_id, NEW.admin_user_name, NEW.admin_user_home_node)
                     IS DISTINCT FROM (OLD.admin_user_id, OLD.admin_user_name, OLD.admin_user_home_node) THEN
                    RAISE EXCEPTION 'La atribución de auditoría es inmutable.' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $fn$;
                CREATE TRIGGER tr_audit_log_autoria_inmutable BEFORE UPDATE ON core.audit_log
                  FOR EACH ROW EXECUTE FUNCTION core.impedir_cambio_autoria_auditoria();
                CREATE FUNCTION core.impedir_cambio_evidencia_zona() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                  IF (to_jsonb(NEW) - 'revoked_at') IS DISTINCT FROM (to_jsonb(OLD) - 'revoked_at')
                     OR (OLD.revoked_at IS NOT NULL AND NEW.revoked_at IS DISTINCT FROM OLD.revoked_at) THEN
                    RAISE EXCEPTION 'La evidencia de zona es inmutable; una revocación no se revierte.' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $fn$;
                CREATE TRIGGER tr_time_zone_evidencia_inmutable BEFORE UPDATE ON core.installation_time_zone_attestations
                  FOR EACH ROW EXECUTE FUNCTION core.impedir_cambio_evidencia_zona();
                """);

            migrationBuilder.CreateIndex(
                name: "uq_time_zone_attestation_id",
                schema: "core",
                table: "installation_time_zone_attestations",
                column: "attestation_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Una reversión automática no puede borrar evidencia ya producida.
            migrationBuilder.Sql("""
                DO $guard$ BEGIN
                  IF EXISTS (SELECT 1 FROM core.installation_time_zone_attestations)
                     OR EXISTS (SELECT 1 FROM core.audit_log WHERE action='export' OR admin_user_name IS NOT NULL OR admin_user_home_node IS NOT NULL)
                     OR EXISTS (SELECT 1 FROM core.audit_log a WHERE a.admin_user_id IS NOT NULL
                                AND NOT EXISTS (SELECT 1 FROM core.admin_users u WHERE u.admin_user_id=a.admin_user_id)) THEN
                    RAISE EXCEPTION 'No se puede revertir esta migración sin perder evidencia de auditoría o zona. No se cambió nada.';
                  END IF;
                END $guard$;
                DROP TRIGGER tr_audit_log_autoria_inmutable ON core.audit_log;
                DROP FUNCTION core.impedir_cambio_autoria_auditoria();
                DROP TRIGGER tr_time_zone_evidencia_inmutable ON core.installation_time_zone_attestations;
                DROP FUNCTION core.impedir_cambio_evidencia_zona();
                """);

            migrationBuilder.DropTable(
                name: "installation_time_zone_attestations",
                schema: "core");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_log_action",
                schema: "core",
                table: "audit_log");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_log_actor_snapshot",
                schema: "core",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "admin_user_home_node",
                schema: "core",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "admin_user_name",
                schema: "core",
                table: "audit_log");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_log_action",
                schema: "core",
                table: "audit_log",
                sql: "action IN ('create', 'update', 'delete', 'activate', 'deactivate', 'login', 'login_failed', 'logout', 'setup', 'email_send')");

            migrationBuilder.AddForeignKey(
                name: "fk_audit_log_admin_user_id",
                schema: "core",
                table: "audit_log",
                column: "admin_user_id",
                principalSchema: "core",
                principalTable: "admin_users",
                principalColumn: "admin_user_id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
