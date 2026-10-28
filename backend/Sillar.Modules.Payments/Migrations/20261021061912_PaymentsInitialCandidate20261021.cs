using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Payments.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Candidata inicial del esquema propio <c>payments</c>. Fue generada para
    /// revisión estática; este cambio no autoriza ejecutarla contra ninguna base.
    /// </remarks>
    public partial class PaymentsInitialCandidate20261021 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payments");

            migrationBuilder.CreateTable(
                name: "provider_inbox_events",
                schema: "payments",
                columns: table => new
                {
                    inbox_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, collation: "C"),
                    provider_event_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, collation: "C"),
                    external_settlement_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, collation: "C"),
                    body_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processing_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    hold_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    commercial_effect_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    commercial_effect_reference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, collation: "C"),
                    last_failure_code = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_inbox_events", x => x.inbox_event_id);
                    table.CheckConstraint("ck_payment_inbox_application", "(processing_state = 'completed' AND commercial_effect_state IN ('applied','already_applied')) OR (processing_state <> 'completed' AND (commercial_effect_state NOT IN ('applied','already_applied') OR processing_state = 'held'))");
                    table.CheckConstraint("ck_payment_inbox_attempt_count", "attempt_count >= 0");
                    table.CheckConstraint("ck_payment_inbox_commercial_effect_state", "commercial_effect_state IN ('not_requested','indeterminate','conflict','decision_required','applied','already_applied')");
                    table.CheckConstraint("ck_payment_inbox_commercial_hold", "(commercial_effect_state IN ('conflict','decision_required') AND processing_state = 'held' AND last_failure_code IS NOT NULL AND btrim(last_failure_code) <> '') OR commercial_effect_state NOT IN ('conflict','decision_required')");
                    table.CheckConstraint("ck_payment_inbox_commercial_reference", "(commercial_effect_state IN ('applied','already_applied') AND commercial_effect_reference IS NOT NULL AND btrim(commercial_effect_reference) <> '') OR (commercial_effect_state NOT IN ('applied','already_applied') AND commercial_effect_reference IS NULL)");
                    table.CheckConstraint("ck_payment_inbox_event", "btrim(provider_event_id) <> ''");
                    table.CheckConstraint("ck_payment_inbox_hold_reason", "(processing_state = 'held' AND hold_reason IS NOT NULL AND btrim(hold_reason) <> '') OR (processing_state <> 'held' AND hold_reason IS NULL)");
                    table.CheckConstraint("ck_payment_inbox_lease", "(processing_state = 'processing' AND lease_token IS NOT NULL AND lease_until IS NOT NULL AND lease_until > updated_at) OR (processing_state <> 'processing' AND lease_token IS NULL AND lease_until IS NULL)");
                    table.CheckConstraint("ck_payment_inbox_processing_state", "processing_state IN ('pending_review','held','ready','processing','indeterminate','completed')");
                    table.CheckConstraint("ck_payment_inbox_provider", "btrim(provider) <> ''");
                    table.CheckConstraint("ck_payment_inbox_retry", "(processing_state = 'indeterminate' AND commercial_effect_state = 'indeterminate' AND next_attempt_at IS NOT NULL AND next_attempt_at > updated_at AND last_failure_code IS NOT NULL AND btrim(last_failure_code) <> '') OR (processing_state <> 'indeterminate' AND next_attempt_at IS NULL)");
                    table.CheckConstraint("ck_payment_inbox_revision", "revision > 0");
                    table.CheckConstraint("ck_payment_inbox_settlement", "btrim(external_settlement_id) <> ''");
                    table.CheckConstraint("ck_payment_inbox_sha256", "body_sha256 COLLATE \"C\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_payment_inbox_timestamps", "updated_at >= created_at");
                });

            migrationBuilder.CreateTable(
                name: "provider_evidence_receipts",
                schema: "payments",
                columns: table => new
                {
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, collation: "C"),
                    provider_event_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, collation: "C"),
                    external_settlement_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, collation: "C"),
                    body_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verification_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reception_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attestation_reference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, collation: "C"),
                    rejection_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    analysis_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    inbox_event_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_evidence_receipts", x => x.receipt_id);
                    table.CheckConstraint("ck_payment_receipts_analysis_sha256", "analysis_sha256 IS NULL OR analysis_sha256 COLLATE \"C\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_payment_receipts_authentication", "(verification_state = 'authenticated' AND verified_at IS NOT NULL AND attestation_reference IS NOT NULL AND btrim(attestation_reference) <> '' AND inbox_event_id IS NOT NULL AND analysis_sha256 IS NOT NULL AND reception_state IN ('held','duplicate','pending_durable_review') AND rejection_reason IS NULL) OR (verification_state <> 'authenticated' AND verified_at IS NULL AND attestation_reference IS NULL AND inbox_event_id IS NULL AND analysis_sha256 IS NULL)");
                    table.CheckConstraint("ck_payment_receipts_event", "btrim(provider_event_id) <> ''");
                    table.CheckConstraint("ck_payment_receipts_provider", "btrim(provider) <> ''");
                    table.CheckConstraint("ck_payment_receipts_reception_state", "reception_state IN ('received','rejected','indeterminate','held','duplicate','pending_durable_review')");
                    table.CheckConstraint("ck_payment_receipts_resolution_reason", "(verification_state IN ('rejected','indeterminate') AND rejection_reason IS NOT NULL AND btrim(rejection_reason) <> '') OR (verification_state NOT IN ('rejected','indeterminate') AND rejection_reason IS NULL)");
                    table.CheckConstraint("ck_payment_receipts_settlement", "btrim(external_settlement_id) <> ''");
                    table.CheckConstraint("ck_payment_receipts_sha256", "body_sha256 COLLATE \"C\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_payment_receipts_state_alignment", "(verification_state = 'received' AND reception_state = 'received') OR (verification_state = 'rejected' AND reception_state = 'rejected') OR (verification_state = 'indeterminate' AND reception_state = 'indeterminate') OR (verification_state = 'authenticated' AND reception_state IN ('held','duplicate','pending_durable_review'))");
                    table.CheckConstraint("ck_payment_receipts_verification_state", "verification_state IN ('received','authenticated','rejected','indeterminate')");
                    table.ForeignKey(
                        name: "fk_payment_receipts_inbox_event",
                        column: x => x.inbox_event_id,
                        principalSchema: "payments",
                        principalTable: "provider_inbox_events",
                        principalColumn: "inbox_event_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_processing_attempts",
                schema: "payments",
                columns: table => new
                {
                    processing_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inbox_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    failure_code = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_processing_attempts", x => x.processing_attempt_id);
                    table.CheckConstraint("ck_payment_attempt_completion", "(outcome = 'running' AND completed_at IS NULL AND failure_code IS NULL) OR (outcome = 'succeeded' AND completed_at IS NOT NULL AND failure_code IS NULL) OR (outcome IN ('held','indeterminate') AND completed_at IS NOT NULL AND failure_code IS NOT NULL AND btrim(failure_code) <> '')");
                    table.CheckConstraint("ck_payment_attempt_number", "attempt_number > 0");
                    table.CheckConstraint("ck_payment_attempt_outcome", "outcome IN ('running','succeeded','held','indeterminate')");
                    table.CheckConstraint("ck_payment_attempt_times", "completed_at IS NULL OR completed_at >= started_at");
                    table.ForeignKey(
                        name: "fk_payment_attempts_inbox_event",
                        column: x => x.inbox_event_id,
                        principalSchema: "payments",
                        principalTable: "provider_inbox_events",
                        principalColumn: "inbox_event_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_settlement_fact_observations",
                schema: "payments",
                columns: table => new
                {
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, collation: "C"),
                    provider_event_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, collation: "C"),
                    external_settlement_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, collation: "C"),
                    body_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false, collation: "C"),
                    provider_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    classification = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_settlement_fact_observations", x => x.receipt_id);
                    table.CheckConstraint("ck_payment_settlement_fact_amount", "gross_amount > 0");
                    table.CheckConstraint("ck_payment_settlement_fact_classification", "classification IN ('new','duplicate','conflict','decision_required')");
                    table.CheckConstraint("ck_payment_settlement_fact_currency", "currency_code COLLATE \"C\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_payment_settlement_fact_event", "btrim(provider_event_id) <> ''");
                    table.CheckConstraint("ck_payment_settlement_fact_provider", "btrim(provider) <> ''");
                    table.CheckConstraint("ck_payment_settlement_fact_settlement", "btrim(external_settlement_id) <> ''");
                    table.CheckConstraint("ck_payment_settlement_fact_sha256", "body_sha256 COLLATE \"C\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_payment_settlement_fact_timestamps", "recorded_at >= verified_at");
                    table.ForeignKey(
                        name: "fk_payment_settlement_fact_receipt",
                        column: x => x.receipt_id,
                        principalSchema: "payments",
                        principalTable: "provider_evidence_receipts",
                        principalColumn: "receipt_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_payment_receipts_event_received",
                schema: "payments",
                table: "provider_evidence_receipts",
                columns: new[] { "provider", "provider_event_id", "received_at" });

            migrationBuilder.CreateIndex(
                name: "idx_payment_receipts_inbox_event",
                schema: "payments",
                table: "provider_evidence_receipts",
                column: "inbox_event_id");

            migrationBuilder.CreateIndex(
                name: "idx_payment_inbox_settlement",
                schema: "payments",
                table: "provider_inbox_events",
                column: "external_settlement_id");

            migrationBuilder.CreateIndex(
                name: "idx_payment_inbox_work",
                schema: "payments",
                table: "provider_inbox_events",
                columns: new[] { "processing_state", "next_attempt_at", "lease_until" });

            migrationBuilder.CreateIndex(
                name: "uq_payment_inbox_provider_event",
                schema: "payments",
                table: "provider_inbox_events",
                columns: new[] { "provider", "provider_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_payment_attempt_outcome",
                schema: "payments",
                table: "provider_processing_attempts",
                column: "outcome");

            migrationBuilder.CreateIndex(
                name: "uq_payment_attempt_inbox_number",
                schema: "payments",
                table: "provider_processing_attempts",
                columns: new[] { "inbox_event_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_payment_attempt_running_inbox",
                schema: "payments",
                table: "provider_processing_attempts",
                column: "inbox_event_id",
                unique: true,
                filter: "outcome = 'running'");

            migrationBuilder.CreateIndex(
                name: "idx_payment_settlement_fact_provider_event",
                schema: "payments",
                table: "provider_settlement_fact_observations",
                columns: new[] { "provider", "provider_event_id" });

            migrationBuilder.CreateIndex(
                name: "idx_payment_settlement_fact_settlement",
                schema: "payments",
                table: "provider_settlement_fact_observations",
                column: "external_settlement_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_processing_attempts",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "provider_settlement_fact_observations",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "provider_evidence_receipts",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "provider_inbox_events",
                schema: "payments");
        }
    }
}
