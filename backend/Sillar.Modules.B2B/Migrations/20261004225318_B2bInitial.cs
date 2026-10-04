using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sillar.Modules.B2B.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// <b>Rehecha el 4 de octubre de 2026</b> para incorporar la atribución del
    /// personal como los <b>tres</b> datos que R-14 exige —nombre congelado,
    /// identificador local y nodo de la cuenta—, y para describir el schema una sola
    /// vez. Se rehizo la inicial en vez de añadir una migración correctiva porque
    /// <b>M07 no ha llegado a <c>main</c> y no existe ninguna instalación</b>; absorbe
    /// también la tabla de series de <c>B2bQuoteNumberSeries</c>, que era enteramente
    /// generada. En cuanto M07 se integre, las migraciones vuelven a ser solo-añadir.
    /// </remarks>
    public partial class B2bInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guarda de instalación (C6, dirección «instalar sin dependencias»).
            // M07 depende duro de M01 y M04. Sin ellos, las claves foráneas de
            // abajo fallarían con «schema "catalog" does not exist», que no dice
            // qué hacer. Esto lo dice, y aborta antes de crear nada: la migración
            // corre en una transacción, así que no queda ni el schema.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    faltan text[] := ARRAY[]::text[];
                BEGIN
                    IF to_regclass('catalog.products') IS NULL OR to_regclass('catalog.product_items') IS NULL THEN
                        faltan := array_append(faltan, 'M01 Catálogo (schema catalog)');
                    END IF;
                    IF to_regclass('crm.customers') IS NULL THEN
                        faltan := array_append(faltan, 'M04 Clientes (schema crm)');
                    END IF;
                    IF cardinality(faltan) > 0 THEN
                        RAISE EXCEPTION 'M07 Solicitudes B2B no se puede instalar porque falta: %. Instala eso antes y repite la instalación de M07.',
                            array_to_string(faltan, ' y ')
                            USING ERRCODE = 'undefined_table';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.EnsureSchema(
                name: "b2b");

            migrationBuilder.EnsureSchema(
                name: "b2b");

            migrationBuilder.CreateTable(
                name: "institution_requests",
                schema: "b2b",
                columns: table => new
                {
                    institution_request_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_name = table.Column<string>(type: "text", nullable: false),
                    institution_document = table.Column<string>(type: "text", nullable: true),
                    contact_person = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    event_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "recibida"),
                    staff_notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_institution_requests", x => x.institution_request_id);
                    table.CheckConstraint("ck_institution_requests_description", "btrim(description) <> ''");
                    table.CheckConstraint("ck_institution_requests_institution_name", "btrim(institution_name) <> ''");
                    table.CheckConstraint("ck_institution_requests_quantity", "quantity > 0");
                    table.CheckConstraint("ck_institution_requests_status", "status IN ('recibida', 'en_revision', 'cotizada', 'cerrada', 'rechazada')");
                });

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

            migrationBuilder.CreateTable(
                name: "special_order_leads",
                schema: "b2b",
                columns: table => new
                {
                    special_order_lead_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name = table.Column<string>(type: "text", nullable: false),
                    product_slug = table.Column<string>(type: "text", nullable: false),
                    pending_relink = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: true),
                    needed_by = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "recibida"),
                    staff_notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_special_order_leads", x => x.special_order_lead_id);
                    table.CheckConstraint("ck_special_order_leads_description", "btrim(description) <> ''");
                    table.CheckConstraint("ck_special_order_leads_product_name", "btrim(product_name) <> ''");
                    table.CheckConstraint("ck_special_order_leads_product_slug", "btrim(product_slug) <> ''");
                    table.CheckConstraint("ck_special_order_leads_quantity", "quantity IS NULL OR quantity > 0");
                    table.CheckConstraint("ck_special_order_leads_status", "status IN ('recibida', 'en_revision', 'cotizada', 'cerrada', 'rechazada')");
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                schema: "b2b",
                columns: table => new
                {
                    quote_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    quote_number = table.Column<string>(type: "text", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    special_order_lead_id = table.Column<int>(type: "integer", nullable: true),
                    institution_request_id = table.Column<int>(type: "integer", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "borrador"),
                    invalidated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    invalidated_reason = table.Column<string>(type: "text", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    payment_method = table.Column<string>(type: "text", nullable: true),
                    payment_reference = table.Column<string>(type: "text", nullable: true),
                    paid_registered_by = table.Column<string>(type: "text", nullable: true),
                    paid_registered_by_admin_user_local_id = table.Column<int>(type: "integer", nullable: true),
                    paid_registered_by_admin_user_home_node = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotes", x => x.quote_id);
                    table.CheckConstraint("ck_quotes_atribucion_completa", "(paid_registered_by IS NULL AND paid_registered_by_admin_user_local_id IS NULL   AND paid_registered_by_admin_user_home_node IS NULL) OR (paid_registered_by IS NOT NULL AND paid_registered_by_admin_user_local_id IS NOT NULL   AND paid_registered_by_admin_user_home_node IS NOT NULL)");
                    table.CheckConstraint("ck_quotes_atribucion_home_node", "paid_registered_by_admin_user_home_node IS NULL OR btrim(paid_registered_by_admin_user_home_node) <> ''");
                    table.CheckConstraint("ck_quotes_atribucion_local_positiva", "paid_registered_by_admin_user_local_id IS NULL OR paid_registered_by_admin_user_local_id > 0");
                    table.CheckConstraint("ck_quotes_atribucion_nombre", "paid_registered_by IS NULL OR btrim(paid_registered_by) <> ''");
                    table.CheckConstraint("ck_quotes_number", "btrim(quote_number) <> ''");
                    table.CheckConstraint("ck_quotes_origen", "(special_order_lead_id IS NULL) <> (institution_request_id IS NULL)");
                    table.CheckConstraint("ck_quotes_pago_tiene_atribucion", "paid_at IS NULL OR paid_registered_by IS NOT NULL");
                    table.CheckConstraint("ck_quotes_payment_method", "payment_method IS NULL OR payment_method IN ('yape', 'efectivo', 'tarjeta')");
                    table.CheckConstraint("ck_quotes_status", "status IN ('borrador', 'enviada', 'aprobada', 'pagada', 'anulada')");
                    table.CheckConstraint("ck_quotes_total_amount", "total_amount >= 0");
                    table.ForeignKey(
                        name: "fk_quotes_institution_request",
                        column: x => x.institution_request_id,
                        principalSchema: "b2b",
                        principalTable: "institution_requests",
                        principalColumn: "institution_request_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quotes_special_order_lead",
                        column: x => x.special_order_lead_id,
                        principalSchema: "b2b",
                        principalTable: "special_order_leads",
                        principalColumn: "special_order_lead_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quote_lines",
                schema: "b2b",
                columns: table => new
                {
                    quote_line_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    quote_id = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name = table.Column<string>(type: "text", nullable: true),
                    variant_value = table.Column<string>(type: "text", nullable: true),
                    sale_unit = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    catalog_price_at_quote = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_lines", x => x.quote_line_id);
                    table.CheckConstraint("ck_quote_lines_catalog_price", "catalog_price_at_quote IS NULL OR catalog_price_at_quote >= 0");
                    table.CheckConstraint("ck_quote_lines_description", "btrim(description) <> ''");
                    table.CheckConstraint("ck_quote_lines_quantity", "quantity > 0");
                    table.CheckConstraint("ck_quote_lines_snapshot", "(item_id IS NOT NULL OR (product_name IS NULL AND variant_value IS NULL AND sale_unit IS NULL AND catalog_price_at_quote IS NULL)) AND (item_id IS NULL OR (product_name IS NOT NULL AND btrim(product_name) <> ''))");
                    table.CheckConstraint("ck_quote_lines_sort_order", "sort_order >= 0");
                    table.CheckConstraint("ck_quote_lines_unit_price", "unit_price >= 0");
                    table.ForeignKey(
                        name: "fk_quote_lines_quote",
                        column: x => x.quote_id,
                        principalSchema: "b2b",
                        principalTable: "quotes",
                        principalColumn: "quote_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_institution_requests_customer",
                schema: "b2b",
                table: "institution_requests",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_institution_requests_status",
                schema: "b2b",
                table: "institution_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_quote_lines_item",
                schema: "b2b",
                table: "quote_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "idx_quote_lines_quote",
                schema: "b2b",
                table: "quote_lines",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "idx_quotes_customer",
                schema: "b2b",
                table: "quotes",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_quotes_status",
                schema: "b2b",
                table: "quotes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_institution_request_id",
                schema: "b2b",
                table: "quotes",
                column: "institution_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_quotes_special_order_lead_id",
                schema: "b2b",
                table: "quotes",
                column: "special_order_lead_id");

            migrationBuilder.CreateIndex(
                name: "uq_quotes_number",
                schema: "b2b",
                table: "quotes",
                column: "quote_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_special_order_leads_customer",
                schema: "b2b",
                table: "special_order_leads",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_special_order_leads_product",
                schema: "b2b",
                table: "special_order_leads",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_special_order_leads_status",
                schema: "b2b",
                table: "special_order_leads",
                column: "status");
        

            migrationBuilder.Sql(
                """
                ALTER TABLE b2b.special_order_leads
                    ADD CONSTRAINT fk_special_order_leads_customer
                    FOREIGN KEY (customer_id) REFERENCES crm.customers (customer_id);
                ALTER TABLE b2b.special_order_leads
                    ADD CONSTRAINT fk_special_order_leads_product
                    FOREIGN KEY (product_id) REFERENCES catalog.products (id);
                ALTER TABLE b2b.institution_requests
                    ADD CONSTRAINT fk_institution_requests_customer
                    FOREIGN KEY (customer_id) REFERENCES crm.customers (customer_id);
                ALTER TABLE b2b.quotes
                    ADD CONSTRAINT fk_quotes_customer
                    FOREIGN KEY (customer_id) REFERENCES crm.customers (customer_id);
                ALTER TABLE b2b.quote_lines
                    ADD CONSTRAINT fk_quote_lines_item
                    FOREIGN KEY (item_id) REFERENCES catalog.product_items (id);
                """);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION b2b.set_updated_at()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    NEW.updated_at := now();
                    RETURN NEW;
                END;
                $$;
                """);

            foreach (var table in new[] { "special_order_leads", "institution_requests", "quotes" })
            {
                migrationBuilder.Sql(
                    $"""
                     CREATE TRIGGER trg_{table}_set_updated_at
                         BEFORE UPDATE ON b2b.{table}
                         FOR EACH ROW
                         EXECUTE FUNCTION b2b.set_updated_at();
                     """);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// La función <c>b2b.set_updated_at()</c> no se va con las tablas —los
        /// triggers sí, porque pertenecen a ellas— así que se baja explícitamente y
        /// antes, con <c>CASCADE</c> para que se lleve los triggers que la usan.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS b2b.set_updated_at() CASCADE;");

            migrationBuilder.DropTable(
                name: "quote_lines",
                schema: "b2b");

            migrationBuilder.DropTable(
                name: "quote_number_series",
                schema: "b2b");

            migrationBuilder.DropTable(
                name: "quotes",
                schema: "b2b");

            migrationBuilder.DropTable(
                name: "institution_requests",
                schema: "b2b");

            migrationBuilder.DropTable(
                name: "special_order_leads",
                schema: "b2b");
        }
    }
}
