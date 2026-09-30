using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sillar.Modules.Sales.Migrations
{
    /// <summary>
    /// Crea el schema <c>sales</c> completo: las siete tablas de M03, sus
    /// constraints, índices, el trigger de <c>updated_at</c> y las dos claves
    /// foráneas cruzadas hacia <c>catalog</c> y <c>crm</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generada y después <b>revisada y anotada a mano</b>, como pide la ADR-009:
    /// «hay que revisar cada migración antes de darla por buena». Lo que EF no
    /// descubre va con <c>MigrationBuilder.Sql(...)</c> al final — el trigger de
    /// <c>updated_at</c> y las dos FK cruzadas.
    /// </para>
    /// <para>
    /// <b>Rehecha el 30 de septiembre de 2026</b> para incorporar la atribución del
    /// personal como los <b>tres</b> datos ratificados: nombre congelado,
    /// identificador local y <b>nodo de pertenencia de la cuenta</b>. Se rehízo la
    /// inicial en vez de añadir una segunda migración
    /// porque <b>M03 no ha llegado a <c>main</c> y no existe ninguna instalación</b>:
    /// el schema de un módulo se describe una vez. En cuanto M03 se integre, las
    /// migraciones vuelven a ser solo-añadir.
    /// </para>
    /// <para>
    /// <b>Ninguna colación no determinista.</b> M03 no introduce ninguna: el código
    /// visible se busca exacto y los nombres congelados son evidencia histórica, no
    /// claves de búsqueda. Por eso aquí no hay <c>core.es_ci</c> ni
    /// <c>core.es_search</c>, ni <c>pg_trgm</c>, ni configuración de búsqueda
    /// textual propia.
    /// </para>
    /// <para>
    /// <b>Qué se replica y qué no</b>, decidido antes de este archivo y visible en
    /// las columnas: <c>orders</c>, <c>order_lines</c>, <c>order_payments</c> y
    /// <c>order_status_changes</c> llevan <c>uuid</c> v7 más <c>origin_node</c> y
    /// <c>row_version</c>; <c>order_series</c>, <c>carts</c> y <c>cart_items</c>
    /// llevan <c>integer GENERATED ALWAYS AS IDENTITY</c> y ninguna columna de
    /// replicación.
    /// </para>
    /// <para>
    /// <b>No hay ninguna clave foránea hacia <c>core.admin_users</c>.</b> La
    /// atribución del personal es una fotografía inmutable de <b>tres</b> datos, y
    /// todos son <b>dato de bitácora, no puntero</b>. El identificador local se
    /// interpreta contra el <b>nodo de pertenencia de la cuenta</b>, que tiene su
    /// propia columna. <c>origin_node</c> conserva su semántica —<b>dónde ocurrió la
    /// actuación</b>— y los dos nodos <b>pueden diferir</b>: ningún <c>CHECK</c>
    /// exige que coincidan.
    /// </para>
    /// </remarks>
    public partial class SalesInitial : Migration
    {
        /// <summary>Tablas con trigger <c>set_updated_at</c>.</summary>
        /// <remarks>
        /// Las siete: todas llevan <c>updated_at</c>, replicadas y locales. En las
        /// replicadas la columna es parte del contrato de replicación; en las
        /// locales sirve para saber cuándo se tocó un carrito o cuándo una serie
        /// entregó su último número.
        /// </remarks>
        private static readonly string[] TablesWithUpdatedAt =
        [
            "orders",
            "order_lines",
            "order_payments",
            "order_status_changes",
            "order_series",
            "carts",
            "cart_items"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.CreateTable(
                name: "carts",
                schema: "sales",
                columns: table => new
                {
                    cart_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carts", x => x.cart_id);
                });

            migrationBuilder.CreateTable(
                name: "order_series",
                schema: "sales",
                columns: table => new
                {
                    order_series_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    node_code = table.Column<string>(type: "text", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_series", x => x.order_series_id);
                    table.CheckConstraint("ck_order_series_last_number_no_negativo", "last_number >= 0");
                    table.CheckConstraint("ck_order_series_node_code_no_vacio", "btrim(node_code) <> ''");
                    table.CheckConstraint("ck_order_series_year_razonable", "year BETWEEN 2000 AND 9999");
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "sales",
                columns: table => new
                {
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_code = table.Column<string>(type: "text", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_full_name = table.Column<string>(type: "text", nullable: false),
                    customer_email = table.Column<string>(type: "text", nullable: false),
                    customer_phone = table.Column<string>(type: "text", nullable: true),
                    customer_document_type = table.Column<string>(type: "text", nullable: true),
                    customer_document_number = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    payment_due_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.order_id);
                    table.CheckConstraint("ck_orders_customer_email_no_vacio", "btrim(customer_email) <> ''");
                    table.CheckConstraint("ck_orders_customer_full_name_no_vacio", "btrim(customer_full_name) <> ''");
                    table.CheckConstraint("ck_orders_order_code_no_vacio", "btrim(order_code) <> ''");
                    table.CheckConstraint("ck_orders_status", "status IN ('pending_payment', 'payment_to_verify', 'preparing', 'ready_for_pickup', 'delivered', 'expired', 'cancelled')");
                    table.CheckConstraint("ck_orders_total_amount_no_negativo", "total_amount >= 0");
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                schema: "sales",
                columns: table => new
                {
                    cart_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    cart_id = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_items", x => x.cart_item_id);
                    table.CheckConstraint("ck_cart_items_quantity_positiva", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_cart_items_cart_id",
                        column: x => x.cart_id,
                        principalSchema: "sales",
                        principalTable: "carts",
                        principalColumn: "cart_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_lines",
                schema: "sales",
                columns: table => new
                {
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "text", nullable: false),
                    variant_value = table.Column<string>(type: "text", nullable: true),
                    sale_unit = table.Column<string>(type: "text", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_lines", x => x.order_line_id);
                    table.CheckConstraint("ck_order_lines_product_name_no_vacio", "btrim(product_name) <> ''");
                    table.CheckConstraint("ck_order_lines_quantity_positiva", "quantity > 0");
                    table.CheckConstraint("ck_order_lines_unit_price_no_negativo", "unit_price >= 0");
                    table.ForeignKey(
                        name: "fk_order_lines_order_id",
                        column: x => x.order_id,
                        principalSchema: "sales",
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_payments",
                schema: "sales",
                columns: table => new
                {
                    order_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: true),
                    registered_by = table.Column<string>(type: "text", nullable: false),
                    registered_by_admin_user_local_id = table.Column<int>(type: "integer", nullable: false),
                    registered_by_admin_user_home_node = table.Column<string>(type: "text", nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    was_late = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_payments", x => x.order_payment_id);
                    table.CheckConstraint("ck_order_payments_amount_no_negativo", "amount >= 0");
                    table.CheckConstraint("ck_order_payments_atribucion_home_node_no_vacio", "btrim(registered_by_admin_user_home_node) <> ''");
                    table.CheckConstraint("ck_order_payments_atribucion_local_positiva", "registered_by_admin_user_local_id > 0");
                    table.CheckConstraint("ck_order_payments_method", "method IN ('yape', 'efectivo')");
                    table.CheckConstraint("ck_order_payments_registered_by_no_vacio", "btrim(registered_by) <> ''");
                    table.ForeignKey(
                        name: "fk_order_payments_order_id",
                        column: x => x.order_id,
                        principalSchema: "sales",
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_status_changes",
                schema: "sales",
                columns: table => new
                {
                    order_status_change_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    changed_by = table.Column<string>(type: "text", nullable: true),
                    changed_by_admin_user_local_id = table.Column<int>(type: "integer", nullable: true),
                    changed_by_admin_user_home_node = table.Column<string>(type: "text", nullable: true),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_status_changes", x => x.order_status_change_id);
                    table.CheckConstraint("ck_order_status_changes_atribucion_completa", "(changed_by IS NULL AND changed_by_admin_user_local_id IS NULL   AND changed_by_admin_user_home_node IS NULL) OR (changed_by IS NOT NULL AND changed_by_admin_user_local_id IS NOT NULL   AND changed_by_admin_user_home_node IS NOT NULL)");
                    table.CheckConstraint("ck_order_status_changes_atribucion_home_node_no_vacio", "changed_by_admin_user_home_node IS NULL OR btrim(changed_by_admin_user_home_node) <> ''");
                    table.CheckConstraint("ck_order_status_changes_atribucion_local_positiva", "changed_by_admin_user_local_id IS NULL OR changed_by_admin_user_local_id > 0");
                    table.CheckConstraint("ck_order_status_changes_changed_by_no_vacio", "changed_by IS NULL OR btrim(changed_by) <> ''");
                    table.CheckConstraint("ck_order_status_changes_from_status", "from_status IS NULL OR from_status IN ('pending_payment', 'payment_to_verify', 'preparing', 'ready_for_pickup', 'delivered', 'expired', 'cancelled')");
                    table.CheckConstraint("ck_order_status_changes_no_es_el_mismo", "from_status IS NULL OR from_status <> to_status");
                    table.CheckConstraint("ck_order_status_changes_to_status", "to_status IN ('pending_payment', 'payment_to_verify', 'preparing', 'ready_for_pickup', 'delivered', 'expired', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_order_status_changes_order_id",
                        column: x => x.order_id,
                        principalSchema: "sales",
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_cart_items_cart_id_item_id",
                schema: "sales",
                table: "cart_items",
                columns: new[] { "cart_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_carts_customer_id",
                schema: "sales",
                table: "carts",
                column: "customer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_order_lines_item_id",
                schema: "sales",
                table: "order_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "idx_order_lines_order_id",
                schema: "sales",
                table: "order_lines",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "idx_order_payments_order_id",
                schema: "sales",
                table: "order_payments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "idx_order_payments_was_late",
                schema: "sales",
                table: "order_payments",
                column: "was_late",
                filter: "was_late");

            migrationBuilder.CreateIndex(
                name: "uq_order_series_node_code_year",
                schema: "sales",
                table: "order_series",
                columns: new[] { "node_code", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_order_status_changes_order_id",
                schema: "sales",
                table: "order_status_changes",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "idx_orders_customer_id",
                schema: "sales",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_orders_payment_due_at",
                schema: "sales",
                table: "orders",
                column: "payment_due_at");

            migrationBuilder.CreateIndex(
                name: "idx_orders_status",
                schema: "sales",
                table: "orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_orders_order_code",
                schema: "sales",
                table: "orders",
                column: "order_code",
                unique: true);

            // ================================================================
            // sales.set_updated_at()
            //
            // Una función por schema, igual que CoreInitial, CatalogInitial y
            // CrmInitial. Vive en sales, así que DROP SCHEMA sales CASCADE se la
            // lleva y no queda nada de M03 en la base.
            // ================================================================
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION sales.set_updated_at()
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
                         BEFORE UPDATE ON sales.{table}
                         FOR EACH ROW
                         EXECUTE FUNCTION sales.set_updated_at();
                     """);
            }

            // ================================================================
            // Comentarios de columna: «dato de bitácora, no puntero»
            //
            // La frase va en la base, no solo en el código C#, y no es
            // decoración. Quien inspeccione el schema con \d+ o desde una
            // herramienta gráfica ve un entero llamado *_admin_user_id_* y su
            // primer impulso es resolverlo con un JOIN contra core.admin_users.
            // Funcionaría —en un solo nodo, que es lo peor que puede pasar—.
            // El comentario es el único sitio donde el aviso alcanza a quien
            // nunca abrirá el C#.
            // ================================================================
            migrationBuilder.Sql(
                """
                COMMENT ON COLUMN sales.order_payments.registered_by_admin_user_local_id IS
                    'Dato de bitacora, no puntero. Identificador del trabajador dentro de SU NODO DE '
                    'PERTENENCIA, que es registered_by_admin_user_home_node y NO origin_node. Sin FK a '
                    'core.admin_users y sin JOIN (ADR-018): esa tabla no se replica y esta si.';
                """);

            migrationBuilder.Sql(
                """
                COMMENT ON COLUMN sales.order_payments.registered_by_admin_user_home_node IS
                    'Nodo al que pertenece la CUENTA del trabajador. NO es origin_node, que dice donde '
                    'OCURRIO LA ACTUACION, y NO se deriva de el: una cuenta del nodo A puede registrar '
                    'un pago desde el nodo B. No hay CHECK que exija que coincidan, a proposito.';
                """);

            migrationBuilder.Sql(
                """
                COMMENT ON COLUMN sales.order_status_changes.changed_by_admin_user_local_id IS
                    'Dato de bitacora, no puntero. Se interpreta contra changed_by_admin_user_home_node, '
                    'NO contra origin_node ni contra el origin_node del pedido. Nulo junto con los otros '
                    'dos significa que lo hizo el sistema: nunca un trabajador ficticio.';
                """);

            migrationBuilder.Sql(
                """
                COMMENT ON COLUMN sales.order_status_changes.changed_by_admin_user_home_node IS
                    'Nodo al que pertenece la CUENTA del trabajador. Independiente de origin_node, que es '
                    'el nodo de la actuacion, y no derivado de el. Los tres datos de atribucion van '
                    'juntos o los tres nulos.';
                """);

            migrationBuilder.Sql(
                """
                COMMENT ON COLUMN sales.order_payments.origin_node IS
                    'Nodo DONDE OCURRIO LA ACTUACION (semantica de replicacion, ADR-016 regla 4). NO es '
                    'el nodo de pertenencia de la cuenta del trabajador: eso es '
                    'registered_by_admin_user_home_node, y pueden diferir.';
                """);

            migrationBuilder.Sql(
                """
                COMMENT ON COLUMN sales.order_status_changes.origin_node IS
                    'Nodo DONDE OCURRIO LA ACTUACION (semantica de replicacion, ADR-016 regla 4). NO es '
                    'el nodo de pertenencia de la cuenta: eso es changed_by_admin_user_home_node.';
                """);

            // ================================================================
            // Claves foráneas cruzadas
            //
            // Las dos están permitidas porque sus módulos son dependencias DURAS,
            // y van declaradas aquí —en la migración del módulo dependiente—,
            // nunca en la de M01 ni la de M04.
            //
            // Y las dos pasan el barrido de la ADR-018: uuid → uuid entre tablas
            // que se replican las dos. orders y order_lines viajan, y
            // crm.customers y catalog.product_items están al otro lado, así que
            // la referencia sigue siendo válida en cualquier nodo.
            //
            // EF no las descubre solo, y no puede: no hay propiedad de navegación
            // hacia la entidad de otro módulo, porque un módulo nunca mapea las
            // tablas de otro. Mismo precedente que las cuatro de Catalog hacia
            // core.media_assets.
            //
            // RESTRICT en las dos, y es deliberado:
            //   · un cliente con pedidos no se puede borrar físicamente, y si
            //     alguien lo intenta debe fallar con un error explícito;
            //   · una variante con ventas tampoco — es el comportamiento que
            //     ARQUITECTURA_MODULAR describe como el correcto.
            // Ninguna es CASCADE ni SET NULL: un pedido sin su cliente o sin su
            // variante no es un pedido degradado, es un pedido corrupto.
            // ================================================================
            migrationBuilder.Sql(
                """
                ALTER TABLE sales.orders
                    ADD CONSTRAINT fk_orders_customer_id
                    FOREIGN KEY (customer_id) REFERENCES crm.customers (customer_id)
                    ON DELETE RESTRICT;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE sales.order_lines
                    ADD CONSTRAINT fk_order_lines_item_id
                    FOREIGN KEY (item_id) REFERENCES catalog.product_items (id)
                    ON DELETE RESTRICT;
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Las FK cruzadas se bajan explícitamente antes de las tablas: apuntan a
        /// otros schemas, así que <c>DropTable</c> no las conoce. La función
        /// <c>sales.set_updated_at()</c> tampoco se va con las tablas —los triggers
        /// sí, porque pertenecen a ellas— y hay que bajarla a mano.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE sales.order_lines DROP CONSTRAINT IF EXISTS fk_order_lines_item_id;");

            migrationBuilder.Sql(
                "ALTER TABLE sales.orders DROP CONSTRAINT IF EXISTS fk_orders_customer_id;");

            migrationBuilder.DropTable(
                name: "cart_items",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "order_lines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "order_payments",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "order_series",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "order_status_changes",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "carts",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "sales");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sales.set_updated_at();");
        }
    }
}
