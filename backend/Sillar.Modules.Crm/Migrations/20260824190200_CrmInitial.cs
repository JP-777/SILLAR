using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sillar.Modules.Crm.Migrations
{
    /// <summary>
    /// Crea el schema <c>crm</c> completo: las seis tablas de M04, sus
    /// constraints, índices, claves foráneas y los triggers de
    /// <c>updated_at</c> e invalidación de <c>email_verified_at</c>.
    /// </summary>
    /// <remarks>
    /// Escrita a mano (ADR-009). La extensión <c>pg_trgm</c> se instala al
    /// inicio —mismo precedente que Catalog— y no se elimina en 99_drop.
    /// Las colaciones <c>core.es_ci</c> (email) y <c>core.es_search</c>
    /// (full_name) se aplican con SQL explícito tras crear la tabla,
    /// siguiendo el precedente de Catalog: Npgsql genera
    /// <c>COLLATE "core.es_ci"</c> (entrecomillando el nombre calificado
    /// como un identificador único), y PostgreSQL busca entonces una
    /// colación llamada literalmente «core.es_ci», que no existe.
    /// Los índices de búsqueda —trigramas sobre email con COLLATE "C" y
    /// texto completo sobre full_name con 'crm.spanish_unaccent'— también van con SQL
    /// explícito, porque EF no los descubre.
    /// </remarks>
    public partial class CrmInitial : Migration
    {
        /// <summary>Columnas con colación de identidad (core.es_ci).</summary>
        private static readonly (string Table, string Column)[] IdentityCollated =
        [
            ("customers", "email"),
            ("contact_messages", "email")
        ];

        /// <summary>Columnas con colación de búsqueda (core.es_search).</summary>
        private static readonly (string Table, string Column)[] SearchCollated =
        [
            ("customers", "full_name"),
            ("contact_messages", "full_name")
        ];

        /// <summary>Tablas con trigger set_updated_at.</summary>
        private static readonly string[] TablesWithUpdatedAt =
        [
            "customers",
            "customer_addresses",
            "customer_accounts",
            "contact_messages"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crm");

            // ================================================================
            // pg_trgm
            //
            // Para la búsqueda parcial de email. Se instala en el schema
            // por defecto y NO se elimina en 99_drop.sql: otro módulo puede
            // estar usándola, y quitarla al desinstalar M04 le rompería sus
            // índices. Mismo precedente que CatalogInitial.
            // ================================================================
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // ================================================================
            // unaccent
            //
            // Eliminación de diacríticos para la búsqueda textual de
            // full_name. Igual que pg_trgm, es infraestructura compartida:
            // se instala en el schema por defecto y NO se elimina en
            // 99_drop.sql. Otro módulo futuro puede usarla.
            // ================================================================
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");

            // ================================================================
            // Configuración de búsqueda textual propia de CRM
            //
            // crm.spanish_unaccent es una copia de pg_catalog.spanish con el
            // diccionario unaccent delante de spanish_stem. Así la búsqueda
            // conserva stemming español y además ignora diacríticos: «Peña»
            // produce «pen» igual que «pena».
            //
            // La configuración sí pertenece a CRM (vive en el schema crm),
            // así que DROP SCHEMA crm CASCADE la elimina. La extensión
            // global unaccent permanece.
            // ================================================================
            migrationBuilder.Sql(
                """
                CREATE TEXT SEARCH CONFIGURATION crm.spanish_unaccent
                    (COPY = pg_catalog.spanish);

                ALTER TEXT SEARCH CONFIGURATION crm.spanish_unaccent
                    ALTER MAPPING FOR hword, hword_part, word
                    WITH unaccent, spanish_stem;
                """);

            // ================================================================
            // crm.customers
            // ================================================================
            migrationBuilder.CreateTable(
                name: "customers",
                schema: "crm",
                columns: table => new
                {
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    document_type = table.Column<string>(type: "text", nullable: true),
                    document_number = table.Column<string>(type: "text", nullable: true),
                    internal_notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    blocked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    reactivation_requested_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    reactivation_resolved_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.customer_id);
                    table.CheckConstraint("ck_customers_full_name_no_vacio", "btrim(full_name) <> ''");
                    table.CheckConstraint("ck_customers_email_no_vacio", "btrim(email) <> ''");
                    table.CheckConstraint("ck_customers_document_pair",
                        "(document_type IS NULL AND document_number IS NULL) OR (document_type IS NOT NULL AND document_number IS NOT NULL)");
                    table.CheckConstraint("ck_customers_document_type",
                        "document_type IS NULL OR document_type IN ('dni', 'ruc')");
                    table.CheckConstraint("ck_customers_document_number_no_vacio",
                        "document_number IS NULL OR btrim(document_number) <> ''");
                    table.CheckConstraint("ck_customers_lifecycle_state",
                        """
                        (
                            is_active = true
                            AND deactivated_at IS NULL
                            AND blocked_at IS NULL
                        )
                        OR
                        (
                            is_active = false
                            AND deactivated_at IS NOT NULL
                            AND blocked_at IS NULL
                        )
                        OR
                        (
                            is_active = false
                            AND deactivated_at IS NULL
                            AND blocked_at IS NOT NULL
                        )
                        """);
                    table.CheckConstraint("ck_customers_reactivation_timestamps",
                        """
                        reactivation_resolved_at IS NULL
                        OR (
                            reactivation_requested_at IS NOT NULL
                            AND reactivation_resolved_at >= reactivation_requested_at
                        )
                        """);
                });

            // ================================================================
            // crm.customer_addresses
            // ================================================================
            migrationBuilder.CreateTable(
                name: "customer_addresses",
                schema: "crm",
                columns: table => new
                {
                    customer_address_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "text", nullable: true),
                    address_line = table.Column<string>(type: "text", nullable: false),
                    district = table.Column<string>(type: "text", nullable: true),
                    province = table.Column<string>(type: "text", nullable: true),
                    department = table.Column<string>(type: "text", nullable: true),
                    reference = table.Column<string>(type: "text", nullable: true),
                    is_preferred = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_addresses", x => x.customer_address_id);
                    table.CheckConstraint("ck_customer_addresses_address_line_no_vacio", "btrim(address_line) <> ''");
                    table.CheckConstraint("ck_customer_addresses_preferred_active", "NOT is_preferred OR is_active");
                    table.ForeignKey(
                        name: "fk_customer_addresses_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "crm",
                        principalTable: "customers",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ================================================================
            // crm.customer_accounts
            // ================================================================
            migrationBuilder.CreateTable(
                name: "customer_accounts",
                schema: "crm",
                columns: table => new
                {
                    customer_account_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    email_verified_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_accounts", x => x.customer_account_id);
                    table.CheckConstraint("ck_customer_accounts_password_hash_no_vacio", "btrim(password_hash) <> ''");
                    table.ForeignKey(
                        name: "fk_customer_accounts_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "crm",
                        principalTable: "customers",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ================================================================
            // crm.customer_sessions
            // ================================================================
            migrationBuilder.CreateTable(
                name: "customer_sessions",
                schema: "crm",
                columns: table => new
                {
                    customer_session_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    customer_account_id = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    csrf_token_hash = table.Column<string>(type: "text", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_sessions", x => x.customer_session_id);
                    table.CheckConstraint("ck_customer_sessions_token_hash_no_vacio", "btrim(token_hash) <> ''");
                    table.CheckConstraint("ck_customer_sessions_csrf_token_hash_no_vacio", "btrim(csrf_token_hash) <> ''");
                    table.CheckConstraint("ck_customer_sessions_last_seen_after_issued", "last_seen_at >= issued_at");
                    table.CheckConstraint("ck_customer_sessions_expires_after_issued", "expires_at > issued_at");
                    table.CheckConstraint("ck_customer_sessions_revoked_after_issued", "revoked_at IS NULL OR revoked_at >= issued_at");
                    table.ForeignKey(
                        name: "fk_customer_sessions_customer_account_id",
                        column: x => x.customer_account_id,
                        principalSchema: "crm",
                        principalTable: "customer_accounts",
                        principalColumn: "customer_account_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ================================================================
            // crm.customer_tokens
            // ================================================================
            migrationBuilder.CreateTable(
                name: "customer_tokens",
                schema: "crm",
                columns: table => new
                {
                    customer_token_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_tokens", x => x.customer_token_id);
                    table.CheckConstraint("ck_customer_tokens_purpose",
                        """
                        purpose IN (
                            'invitation',
                            'email_verification',
                            'password_reset'
                        )
                        """);
                    table.CheckConstraint("ck_customer_tokens_expires_after_created", "expires_at > created_at");
                    table.CheckConstraint("ck_customer_tokens_used_after_created", "used_at IS NULL OR used_at >= created_at");
                    table.CheckConstraint("ck_customer_tokens_token_hash_no_vacio", "btrim(token_hash) <> ''");
                    table.ForeignKey(
                        name: "fk_customer_tokens_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "crm",
                        principalTable: "customers",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ================================================================
            // crm.contact_messages
            //
            // Mensajes del formulario de contacto. No replica (ADR-017):
            // la captación es propia de WEB. customer_id es nullable porque
            // un visitante puede escribir sin tener ficha. ON DELETE SET NULL
            // porque el mensaje es un registro independiente de captación:
            // perder la asociación no debe borrar el mensaje.
            // ================================================================
            migrationBuilder.CreateTable(
                name: "contact_messages",
                schema: "crm",
                columns: table => new
                {
                    contact_message_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    subject = table.Column<string>(type: "text", nullable: true),
                    message = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contact_messages", x => x.contact_message_id);
                    table.CheckConstraint("ck_contact_messages_full_name_no_vacio", "btrim(full_name) <> ''");
                    table.CheckConstraint("ck_contact_messages_message_no_vacio", "btrim(message) <> ''");
                    table.CheckConstraint("ck_contact_messages_email_no_vacio", "email IS NULL OR btrim(email) <> ''");
                    table.CheckConstraint("ck_contact_messages_phone_no_vacio", "phone IS NULL OR btrim(phone) <> ''");
                    table.CheckConstraint("ck_contact_messages_contact_channel",
                        "(email IS NOT NULL OR phone IS NOT NULL)");
                    table.CheckConstraint("ck_contact_messages_subject_no_vacio",
                        "subject IS NULL OR btrim(subject) <> ''");
                    table.ForeignKey(
                        name: "fk_contact_messages_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "crm",
                        principalTable: "customers",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.SetNull);
                });

            // ================================================================
            // Índices
            // ================================================================
            migrationBuilder.CreateIndex(
                name: "uq_customers_email",
                schema: "crm",
                table: "customers",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_customers_document",
                schema: "crm",
                table: "customers",
                columns: new[] { "document_type", "document_number" },
                unique: true,
                filter: "document_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_customer_addresses_preferred",
                schema: "crm",
                table: "customer_addresses",
                column: "customer_id",
                unique: true,
                filter: "is_preferred AND is_active");

            migrationBuilder.CreateIndex(
                name: "uq_customer_accounts_customer_id",
                schema: "crm",
                table: "customer_accounts",
                column: "customer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_customer_sessions_token_hash",
                schema: "crm",
                table: "customer_sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_customer_tokens_token_hash",
                schema: "crm",
                table: "customer_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_contact_messages_customer",
                schema: "crm",
                table: "contact_messages",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_contact_messages_created_at",
                schema: "crm",
                table: "contact_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_contact_messages_active",
                schema: "crm",
                table: "contact_messages",
                column: "is_active",
                filter: "is_active");

            // ================================================================
            // Índices de búsqueda
            // ================================================================

            // Búsqueda parcial de email por trigramas.
            //
            // El índice va sobre (email COLLATE "C"), no sobre email.
            // PostgreSQL NO admite LIKE ni ILIKE sobre una colación no
            // determinista, y email es es_ci:
            //
            //     SELECT ... WHERE email LIKE '%texto%'
            //     ERROR 0A000: nondeterministic collations are not supported for LIKE
            //
            // Así que la consulta tiene que escribirse igual que el índice:
            //
            //     WHERE email COLLATE "C" ILIKE '%texto%'
            //
            // Mismo precedente que catalog.product_items.code.
            migrationBuilder.Sql(
                """
                CREATE INDEX idx_customers_email_trgm
                    ON crm.customers
                    USING gin ((email COLLATE "C") gin_trgm_ops);
                """);

            // Búsqueda textual de full_name.
            //
            // full_name es es_search (también ICU no determinista), así que
            // tampoco admite LIKE/ILIKE directo. Para nombres usamos búsqueda
            // textual con la configuración propia crm.spanish_unaccent, que
            // encadena unaccent (elimina diacríticos) con spanish_stem
            // (stemming español). Así «Peña» produce «pen» igual que «pena».
            //
            // La configuración va literal —'crm.spanish_unaccent'— para que
            // la expresión sea inmutable y se pueda indexar.
            //
            // La consulta del Paso 3 deberá usar la misma configuración:
            //     to_tsvector('crm.spanish_unaccent', full_name)
            //     @@ plainto_tsquery('crm.spanish_unaccent', @texto)
            migrationBuilder.Sql(
                """
                CREATE INDEX idx_customers_full_name_search
                    ON crm.customers
                    USING gin (to_tsvector('crm.spanish_unaccent', full_name));
                """);

            // ================================================================
            // Colaciones
            //
            // Escrito a mano: el proveedor Npgsql genera COLLATE "core.es_ci",
            // entrecomillando el nombre calificado como si fuera un
            // identificador único, y PostgreSQL busca entonces una colación
            // llamada literalmente «core.es_ci», que no existe. Es el mismo
            // tropiezo documentado en CoreInitial y CatalogInitial.
            //
            // Va después de crear los índices y PostgreSQL los reconstruye
            // solo: ALTER COLUMN ... TYPE rehace todo lo que depende de la
            // columna.
            //
            // core.es_ci — ignora mayúsculas, respeta tildes, colapsa
            // NFC/NFD equivalentes por ICU. Para lo que identifica y es único:
            // el correo.
            // «JOSE@x.pe» = «jose@x.pe»; «José@x.pe» ≠ «Jose@x.pe».
            //
            // core.es_search — ignora mayúsculas y tildes. Para lo que el
            // usuario busca: el nombre.
            // «Peña» = «pena» en búsqueda textual.
            // ================================================================
            // IdentityCollated son las columnas email, limitadas a 150.
            // El ALTER conserva el varchar(150) y aplica la colación.
            foreach (var (table, column) in IdentityCollated)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE crm.{table} ALTER COLUMN {column} TYPE character varying(150) COLLATE core.es_ci;");
            }

            foreach (var (table, column) in SearchCollated)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE crm.{table} ALTER COLUMN {column} TYPE text COLLATE core.es_search;");
            }

            // ================================================================
            // updated_at automático
            //
            // Función propia del schema, no la de CORE: así desaparece al
            // soltar crm y no deja restos. Es el precedente que dejó
            // CoreInitial y CatalogInitial.
            // ================================================================
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION crm.set_updated_at()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    NEW.updated_at := now();
                    RETURN NEW;
                END;
                $$;
                """);

            foreach (var table in TablesWithUpdatedAt)
            {
                migrationBuilder.Sql(
                    $"""
                     CREATE TRIGGER trg_{table}_set_updated_at
                         BEFORE UPDATE ON crm.{table}
                         FOR EACH ROW
                         EXECUTE FUNCTION crm.set_updated_at();
                     """);
            }

            // ================================================================
            // Invalidación de email_verified_at
            //
            // Mecanismo cerrado: trigger PostgreSQL. NO hook de EF. NO servicio.
            // Si cambia la dirección realmente almacenada, deja de ser válido
            // afirmar que esa dirección fue verificada.
            //
            // Funciona incluso si el cambio llega mediante SQL directo y no
            // mediante CrmDbContext. El WHEN (OLD.email IS DISTINCT FROM
            // NEW.email) deja que un cambio de solo mayúsculas —que bajo
            // core.es_ci es la misma fila— no dispare la invalidación.
            // ================================================================
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION crm.invalidate_customer_email_verification()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    UPDATE crm.customer_accounts
                       SET email_verified_at = NULL
                     WHERE customer_id = NEW.customer_id
                       AND email_verified_at IS NOT NULL;

                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_customers_invalidate_email_verification
                    AFTER UPDATE OF email ON crm.customers
                    FOR EACH ROW
                    WHEN (OLD.email IS DISTINCT FROM NEW.email)
                    EXECUTE FUNCTION crm.invalidate_customer_email_verification();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_tokens",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "contact_messages",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "customer_sessions",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "customer_accounts",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "customer_addresses",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "crm");

            // Los triggers se van con sus tablas; las funciones, no.
            // La configuración de búsqueda textual propia de CRM tampoco
            // se va con las tablas: hay que bajarla explícitamente.
            // Las extensiones pg_trgm y unaccent NO se eliminan: son
            // compartidas y otro módulo puede usarlas.
            migrationBuilder.Sql("DROP TEXT SEARCH CONFIGURATION IF EXISTS crm.spanish_unaccent;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS crm.set_updated_at();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS crm.invalidate_customer_email_verification();");
        }
    }
}
