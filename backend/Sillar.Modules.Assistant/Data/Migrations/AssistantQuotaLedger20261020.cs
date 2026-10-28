using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sillar.Modules.Assistant.Data;

namespace Sillar.Modules.Assistant.Data.Migrations;

/// <summary>
/// M12/PG-06: tablas SQL administradas por el ledger transaccional Npgsql.
/// No se proyectan como DbSet en EF: el snapshot EF existente representa
/// exclusivamente el modelo ORM editorial. La migración sí forma parte
/// de IModuleMigrations y su historial propio assistant.__migrations.
/// Aplicación física SOLO sobre PostgreSQL efímero con protocolo aprobado.
/// </summary>
[DbContext(typeof(AssistantDbContext))]
[Migration("20261020170000_AssistantQuotaLedger")]
public sealed class AssistantQuotaLedger20261020 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- CANDIDATO PG-06. Solo esquema propio 'assistant'.
            -- NO APLICAR A BASES EXISTENTES. DDL SQL gestionado por migración EF; snapshot ORM sin cambio.
            CREATE TABLE assistant.quota_accounts (
                installation_id uuid PRIMARY KEY,
                mode smallint NOT NULL,
                engine_configured boolean NOT NULL,
                limit_units bigint NOT NULL,
                consumed_units bigint NOT NULL DEFAULT 0,
                reserved_units bigint NOT NULL DEFAULT 0,
                concurrency_limit integer NOT NULL,
                running_requests integer NOT NULL DEFAULT 0,
                policy_version bigint NOT NULL DEFAULT 0,
                updated_at timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT ck_assistant_quota_account_mode CHECK (mode IN (0,1,2)),
                CONSTRAINT ck_assistant_quota_account_faq CHECK (mode <> 0 OR engine_configured = false),
                CONSTRAINT ck_assistant_quota_account_nonnegative CHECK (
                   limit_units >= 0 AND consumed_units >= 0 AND reserved_units >= 0
                   AND concurrency_limit >= 0 AND running_requests >= 0 AND policy_version >= 0),
                CONSTRAINT ck_assistant_quota_account_budget CHECK (
                   reserved_units <= limit_units AND consumed_units <= limit_units - reserved_units),
                CONSTRAINT ck_assistant_quota_account_running CHECK (running_requests <= concurrency_limit),
                CONSTRAINT ck_assistant_quota_account_install CHECK (
                   installation_id <> '00000000-0000-0000-0000-000000000000'::uuid)
            );

            CREATE TABLE assistant.quota_reservations (
                installation_id uuid NOT NULL,
                reservation_id uuid NOT NULL,
                idempotency_key varchar(128) COLLATE "C" NOT NULL,
                mode smallint NOT NULL,
                worst_case_units bigint NOT NULL,
                actual_units bigint NULL,
                state varchar(20) NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now(),
                dispatched_at timestamptz NULL,
                settled_at timestamptz NULL,
                PRIMARY KEY (installation_id, reservation_id),
                CONSTRAINT uq_assistant_quota_idempotency UNIQUE (installation_id, idempotency_key),
                CONSTRAINT ck_assistant_quota_idempotency_ascii CHECK (idempotency_key ~ '^[!-~]{1,128}$'),
                CONSTRAINT fk_assistant_quota_reservation_account FOREIGN KEY (installation_id)
                    REFERENCES assistant.quota_accounts(installation_id) ON DELETE RESTRICT,
                CONSTRAINT ck_assistant_quota_reservation_mode CHECK (mode IN (1,2)),
                CONSTRAINT ck_assistant_quota_reservation_units CHECK (
                    worst_case_units > 0 AND (actual_units IS NULL OR
                        (actual_units >= 0 AND actual_units <= worst_case_units))),
                CONSTRAINT ck_assistant_quota_reservation_state CHECK
                    (state IN ('reserved','dispatched','uncertain','completed','cancelled')),
                CONSTRAINT ck_assistant_quota_reservation_timestamps CHECK (
                   (state = 'reserved' AND dispatched_at IS NULL AND settled_at IS NULL AND actual_units IS NULL) OR
                   (state IN ('dispatched','uncertain') AND dispatched_at IS NOT NULL AND settled_at IS NULL AND actual_units IS NULL) OR
                   (state = 'completed' AND dispatched_at IS NOT NULL AND settled_at IS NOT NULL AND actual_units IS NOT NULL) OR
                   (state = 'cancelled' AND dispatched_at IS NULL AND settled_at IS NOT NULL AND actual_units IS NULL))
            );
            CREATE INDEX ix_assistant_quota_reservations_pending
                ON assistant.quota_reservations (installation_id, state, created_at)
                WHERE state IN ('reserved', 'dispatched', 'uncertain');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No borra el esquema assistant ni ninguna tabla CORE/Sales.
        migrationBuilder.Sql("DROP TABLE assistant.quota_reservations;");
        migrationBuilder.Sql("DROP TABLE assistant.quota_accounts;");
    }
}
