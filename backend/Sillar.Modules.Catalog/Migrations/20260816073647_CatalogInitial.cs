using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sillar.Modules.Catalog.Migrations
{
    /// <summary>
    /// Crea el schema <c>catalog</c> completo: las seis tablas del SPEC, sus
    /// índices y las claves foráneas hacia <c>core.media_assets</c>.
    /// </summary>
    /// <remarks>
    /// Generada con <c>dotnet ef migrations add</c> y revisada a mano (ADR-009).
    /// Lo añadido después, y por qué, está comentado donde aparece:
    ///
    ///   · la extensión <c>pg_trgm</c>;
    ///   · las colaciones <c>core.es_ci</c> y <c>core.es_search</c>, aplicadas
    ///     con SQL explícito;
    ///   · las cuatro claves foráneas cruzadas a <c>core.media_assets</c>, que
    ///     EF no descubre porque no hay navegación hacia otro módulo;
    ///   · el índice de texto completo y el de trigramas;
    ///   · la función <c>catalog.set_updated_at()</c> y sus seis triggers;
    ///   · la reversión de todo ello en <c>Down</c>.
    ///
    /// Regenerada el 16 de agosto de 2026 (ADR-018): las cuatro columnas hacia
    /// <c>core.media_assets</c> pasan de <c>integer</c> a <c>uuid</c>, porque
    /// esa tabla ahora se replica también.
    /// </remarks>
    public partial class CatalogInitial : Migration
    {
        /// <summary>Tablas de este schema con columna <c>updated_at</c>.</summary>
        /// <remarks>Las seis: todas se replican y todas la llevan.</remarks>
        private static readonly string[] TablesWithUpdatedAt =
        [
            "categories",
            "brands",
            "products",
            "product_items",
            "product_categories",
            "product_images"
        ];

        /// <summary>Columnas con colación de identidad: unicidad que respeta tildes.</summary>
        private static readonly (string Table, string Column)[] IdentityCollated =
        [
            ("categories", "slug"),
            ("brands", "name"),
            ("brands", "slug"),
            ("products", "slug"),
            ("product_items", "code"),
            ("product_items", "barcode")
        ];

        /// <summary>Columnas con colación de búsqueda: ignora mayúsculas y tildes.</summary>
        private static readonly (string Table, string Column)[] SearchCollated =
        [
            ("categories", "name"),
            ("products", "name"),
            ("product_items", "variant_value")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            // ================================================================
            // pg_trgm
            //
            // Para buscar por fragmento de código en la caja. Se instala en el
            // schema por defecto y NO se elimina en 99_drop.sql: otro módulo
            // puede estar usándola, y quitarla al desinstalar M01 le rompería
            // sus índices.
            // ================================================================
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.CreateTable(
                name: "brands",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    logo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brands", x => x.id);
                    table.CheckConstraint("ck_brands_name_no_vacio", "btrim(name) <> ''");
                    table.CheckConstraint("ck_brands_slug_formato", "slug COLLATE \"C\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                });

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.CheckConstraint("ck_categories_name_no_vacio", "btrim(name) <> ''");
                    table.CheckConstraint("ck_categories_parent_no_self", "parent_id IS NULL OR parent_id <> id");
                    table.CheckConstraint("ck_categories_slug_formato", "slug COLLATE \"C\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.CheckConstraint("ck_categories_sort_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "fk_categories_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    short_description = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    primary_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    list_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    sale_unit = table.Column<string>(type: "text", nullable: true),
                    variant_label = table.Column<string>(type: "text", nullable: true),
                    is_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.CheckConstraint("ck_products_list_price_no_negativo", "list_price IS NULL OR list_price >= 0");
                    table.CheckConstraint("ck_products_name_no_vacio", "btrim(name) <> ''");
                    table.CheckConstraint("ck_products_slug_formato", "slug COLLATE \"C\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_products_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "catalog",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_products_primary_category_id",
                        column: x => x.primary_category_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_categories",
                schema: "catalog",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_categories", x => new { x.product_id, x.category_id });
                    table.ForeignKey(
                        name: "fk_product_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_categories_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_images",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alt_text = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_images", x => x.id);
                    table.CheckConstraint("ck_product_images_sort_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "fk_product_images_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_items",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_value = table.Column<string>(type: "text", nullable: true),
                    code = table.Column<string>(type: "text", nullable: true),
                    barcode = table.Column<string>(type: "text", nullable: true),
                    price_override = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    origin_node = table.Column<string>(type: "text", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_items", x => x.id);
                    table.CheckConstraint("ck_product_items_barcode_no_vacio", "barcode IS NULL OR btrim(barcode) <> ''");
                    table.CheckConstraint("ck_product_items_code_no_vacio", "code IS NULL OR btrim(code) <> ''");
                    table.CheckConstraint("ck_product_items_price_no_negativo", "price_override IS NULL OR price_override >= 0");
                    table.CheckConstraint("ck_product_items_sort_order", "sort_order >= 0");
                    table.CheckConstraint("ck_product_items_variant_value_no_vacio", "variant_value IS NULL OR btrim(variant_value) <> ''");
                    table.ForeignKey(
                        name: "fk_product_items_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_brands_name",
                schema: "catalog",
                table: "brands",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_brands_slug",
                schema: "catalog",
                table: "brands",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_categories_activas",
                schema: "catalog",
                table: "categories",
                column: "is_active",
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_categories_parent",
                schema: "catalog",
                table: "categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "uq_categories_slug",
                schema: "catalog",
                table: "categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_product_categories_categoria",
                schema: "catalog",
                table: "product_categories",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "uq_product_images_producto_archivo",
                schema: "catalog",
                table: "product_images",
                columns: new[] { "product_id", "media_asset_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_product_images_una_principal",
                schema: "catalog",
                table: "product_images",
                column: "product_id",
                unique: true,
                filter: "is_primary");

            migrationBuilder.CreateIndex(
                name: "idx_product_items_producto",
                schema: "catalog",
                table: "product_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "uq_product_items_barcode",
                schema: "catalog",
                table: "product_items",
                column: "barcode",
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_product_items_code",
                schema: "catalog",
                table: "product_items",
                column: "code",
                unique: true,
                filter: "code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_product_items_valor",
                schema: "catalog",
                table: "product_items",
                columns: new[] { "product_id", "variant_value" },
                unique: true,
                filter: "variant_value IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_products_categoria_principal",
                schema: "catalog",
                table: "products",
                column: "primary_category_id");

            migrationBuilder.CreateIndex(
                name: "idx_products_marca",
                schema: "catalog",
                table: "products",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "idx_products_publicos",
                schema: "catalog",
                table: "products",
                columns: new[] { "is_active", "is_public" },
                filter: "is_active AND is_public");

            migrationBuilder.CreateIndex(
                name: "uq_products_slug",
                schema: "catalog",
                table: "products",
                column: "slug",
                unique: true);

            // ================================================================
            // Colaciones
            //
            // Escrito a mano: el proveedor Npgsql genera COLLATE "core.es_ci",
            // entrecomillando el nombre calificado como si fuera un
            // identificador único, y PostgreSQL busca entonces una colación
            // llamada literalmente «core.es_ci», que no existe. Es el mismo
            // tropiezo documentado en CoreInitial.
            //
            // Van después de crear los índices y PostgreSQL los reconstruye
            // solo: ALTER COLUMN ... TYPE rehace todo lo que depende de la
            // columna. Está comprobado que los únicos quedan con la colación
            // aplicada, no con la de antes.
            //
            // LAS DOS NO SON INTERCAMBIABLES:
            //   es_ci     — ignora mayúsculas, RESPETA tildes. Para lo que
            //               identifica y es único: slug, código, marca.
            //               «Artesco» = «ARTESCO»; «Peña» ≠ «Pena».
            //   es_search — ignora mayúsculas Y tildes. Para lo que el usuario
            //               busca: nombres y valores de variante.
            //               «lapiz» encuentra «LÁPIZ».
            //
            // Confundirlas tiene consecuencias opuestas: con es_search en el
            // código, «peña» y «pena» serían el mismo producto; con es_ci en el
            // nombre, buscar «lapiz» no encontraría nada.
            // ================================================================
            foreach (var (table, column) in IdentityCollated)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE catalog.{table} ALTER COLUMN {column} TYPE text COLLATE core.es_ci;");
            }

            foreach (var (table, column) in SearchCollated)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE catalog.{table} ALTER COLUMN {column} TYPE text COLLATE core.es_search;");
            }

            // ================================================================
            // Claves foráneas cruzadas hacia core.media_assets
            //
            // Permitidas porque CORE es dependencia DURA (ADR-003), y declaradas
            // aquí —en la migración del módulo dependiente—, nunca en la de CORE.
            //
            // Las cuatro son uuid → uuid (ADR-018): media_assets se replica
            // igual que el catálogo, así que la referencia sigue siendo válida
            // en cualquier nodo al que viaje la fila.
            //
            // ON DELETE SET NULL en las tres opcionales: si alguien borra un
            // archivo, la categoría o la variante se quedan sin foto, no
            // desaparecen. En product_images es CASCADE porque esa fila no es
            // más que la asociación: sin archivo no significa nada.
            //
            // EF no las descubre solo: no hay propiedad de navegación hacia otro
            // módulo, y no puede haberla — un módulo nunca mapea las tablas de
            // otro.
            // ================================================================
            migrationBuilder.Sql(
                """
                ALTER TABLE catalog.categories
                    ADD CONSTRAINT fk_categories_image_id
                    FOREIGN KEY (image_id) REFERENCES core.media_assets (media_asset_id)
                    ON DELETE SET NULL;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE catalog.brands
                    ADD CONSTRAINT fk_brands_logo_id
                    FOREIGN KEY (logo_id) REFERENCES core.media_assets (media_asset_id)
                    ON DELETE SET NULL;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE catalog.product_items
                    ADD CONSTRAINT fk_product_items_image_id
                    FOREIGN KEY (image_id) REFERENCES core.media_assets (media_asset_id)
                    ON DELETE SET NULL;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE catalog.product_images
                    ADD CONSTRAINT fk_product_images_media_asset_id
                    FOREIGN KEY (media_asset_id) REFERENCES core.media_assets (media_asset_id)
                    ON DELETE CASCADE;
                """);

            // ================================================================
            // Búsqueda
            // ================================================================

            // Texto completo en español sobre nombre y descripción corta. La
            // configuración va literal —'spanish'— para que la expresión sea
            // inmutable y se pueda indexar.
            migrationBuilder.Sql(
                """
                CREATE INDEX idx_products_busqueda ON catalog.products
                    USING gin (to_tsvector('spanish', name || ' ' || coalesce(short_description, '')));
                """);

            // Trigramas sobre el código, para buscar por fragmento en la caja.
            //
            // OJO, Y NO ES UN DETALLE: el índice va sobre (code COLLATE "C"), no
            // sobre code. PostgreSQL NO admite LIKE ni ILIKE sobre una colación
            // no determinista, y code es es_ci:
            //
            //     SELECT ... WHERE code LIKE '%45%'
            //     ERROR 0A000: nondeterministic collations are not supported for LIKE
            //
            // Así que la consulta de la caja tiene que escribirse igual que el
            // índice:
            //
            //     WHERE code COLLATE "C" LIKE '%45%'
            //
            // Trampa comprobada: con la tabla vacía ese LIKE NO falla, porque el
            // error salta al ejecutar y no al planificar. Una prueba sobre una
            // tabla recién creada da un falso aprobado.
            //
            // La búsqueda exacta por código —la de la lectora— sí usa code
            // directamente y aprovecha uq_product_items_code.
            migrationBuilder.Sql(
                """
                CREATE INDEX idx_product_items_code_trgm ON catalog.product_items
                    USING gin ((code COLLATE "C") gin_trgm_ops);
                """);

            // ================================================================
            // updated_at automático
            //
            // Función propia del schema, no la de CORE: así desaparece al soltar
            // catalog y no deja restos, y desinstalar un módulo no toca nada de
            // otro. Es el precedente que dejó CoreInitial.
            // ================================================================
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION catalog.set_updated_at()
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
                         BEFORE UPDATE ON catalog.{table}
                         FOR EACH ROW
                         EXECUTE FUNCTION catalog.set_updated_at();
                     """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_images",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_items",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "products",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "brands",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "catalog");

            // Los triggers se van con sus tablas; la función, no.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS catalog.set_updated_at();");

            // pg_trgm NO se elimina: otro módulo puede estar usándola, y
            // quitarla aquí le rompería sus índices. Una extensión compartida se
            // instala una vez y se queda.
        }
    }
}
