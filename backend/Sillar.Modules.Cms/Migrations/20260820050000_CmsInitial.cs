using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Sillar.Modules.Cms.Data;

#nullable disable

namespace Sillar.Modules.Cms.Migrations;

/// <summary>Migración inicial escrita a mano para el schema <c>cms</c>.</summary>
[DbContext(typeof(CmsDbContext))]
[Migration("20260820050000_CmsInitial")]
public sealed class CmsInitial : Migration
{
    private static readonly string[] TablesWithUpdatedAt =
        ["banners", "promotions", "featured_products", "featured_projects", "social_links"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "cms");

        migrationBuilder.CreateTable(
            name: "banners",
            schema: "cms",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                title = table.Column<string>(type: "text", nullable: true),
                subtitle = table.Column<string>(type: "text", nullable: true),
                image_desktop_id = table.Column<Guid>(type: "uuid", nullable: false),
                image_mobile_id = table.Column<Guid>(type: "uuid", nullable: true),
                alt_text = table.Column<string>(type: "text", nullable: false),
                link_url = table.Column<string>(type: "text", nullable: true),
                link_label = table.Column<string>(type: "text", nullable: true),
                display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                starts_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_banners", x => x.id);
                table.CheckConstraint("ck_banners_alt_text_no_vacio", "btrim(alt_text) <> ''");
                table.CheckConstraint("ck_banners_display_order", "display_order >= 0");
                table.CheckConstraint("ck_banners_enlace", "link_url IS NULL OR (link_label IS NOT NULL AND btrim(link_label) <> '')");
                table.CheckConstraint("ck_banners_link_url", "link_url IS NULL OR link_url COLLATE \"C\" ~ '^(/|https?://)'");
                table.CheckConstraint("ck_banners_title_no_vacio", "title IS NULL OR btrim(title) <> ''");
                table.CheckConstraint("ck_banners_vigencia", "starts_at IS NULL OR ends_at IS NULL OR ends_at > starts_at");
            });

        migrationBuilder.CreateTable(
            name: "promotions",
            schema: "cms",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                title = table.Column<string>(type: "text", nullable: true),
                subtitle = table.Column<string>(type: "text", nullable: true),
                image_id = table.Column<Guid>(type: "uuid", nullable: true),
                alt_text = table.Column<string>(type: "text", nullable: false),
                link_url = table.Column<string>(type: "text", nullable: true),
                link_label = table.Column<string>(type: "text", nullable: true),
                display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                starts_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                description = table.Column<string>(type: "text", nullable: true),
                badge_text = table.Column<string>(type: "text", maxLength: 20, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_promotions", x => x.id);
                table.CheckConstraint("ck_promotions_alt_text_no_vacio", "btrim(alt_text) <> ''");
                table.CheckConstraint("ck_promotions_badge_text", "badge_text IS NULL OR (btrim(badge_text) <> '' AND char_length(badge_text) <= 20)");
                table.CheckConstraint("ck_promotions_display_order", "display_order >= 0");
                table.CheckConstraint("ck_promotions_enlace", "link_url IS NULL OR (link_label IS NOT NULL AND btrim(link_label) <> '')");
                table.CheckConstraint("ck_promotions_link_url", "link_url IS NULL OR link_url COLLATE \"C\" ~ '^(/|https?://)'");
                table.CheckConstraint("ck_promotions_title_no_vacio", "title IS NULL OR btrim(title) <> ''");
                table.CheckConstraint("ck_promotions_vigencia", "starts_at IS NULL OR ends_at IS NULL OR ends_at > starts_at");
            });

        migrationBuilder.CreateTable(
            name: "featured_products",
            schema: "cms",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                product_id = table.Column<Guid>(type: "uuid", nullable: true),
                product_name = table.Column<string>(type: "text", nullable: false),
                product_slug = table.Column<string>(type: "text", nullable: true),
                image_id = table.Column<Guid>(type: "uuid", nullable: true),
                display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                starts_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                ends_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_featured_products", x => x.id);
                table.CheckConstraint("ck_featured_products_display_order", "display_order >= 0");
                table.CheckConstraint("ck_featured_products_product_name_no_vacio", "btrim(product_name) <> ''");
                table.CheckConstraint("ck_featured_products_product_slug_no_vacio", "product_slug IS NULL OR btrim(product_slug) <> ''");
                table.CheckConstraint("ck_featured_products_vigencia", "starts_at IS NULL OR ends_at IS NULL OR ends_at > starts_at");
            });

        migrationBuilder.CreateTable(
            name: "featured_projects",
            schema: "cms",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                title = table.Column<string>(type: "text", nullable: false),
                description = table.Column<string>(type: "text", nullable: true),
                image_id = table.Column<Guid>(type: "uuid", nullable: false),
                alt_text = table.Column<string>(type: "text", nullable: false),
                display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_featured_projects", x => x.id);
                table.CheckConstraint("ck_featured_projects_alt_text_no_vacio", "btrim(alt_text) <> ''");
                table.CheckConstraint("ck_featured_projects_display_order", "display_order >= 0");
                table.CheckConstraint("ck_featured_projects_title_no_vacio", "btrim(title) <> ''");
            });

        migrationBuilder.CreateTable(
            name: "social_links",
            schema: "cms",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                platform = table.Column<string>(type: "text", nullable: false),
                url = table.Column<string>(type: "text", nullable: false),
                display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_social_links", x => x.id);
                table.CheckConstraint("ck_social_links_display_order", "display_order >= 0");
                table.CheckConstraint("ck_social_links_plataforma", "platform IN ('facebook', 'instagram', 'tiktok', 'whatsapp', 'youtube')");
                table.CheckConstraint("ck_social_links_url", "url COLLATE \"C\" ~ '^https?://[^[:space:]]+$'");
            });

        migrationBuilder.CreateIndex(
            name: "idx_banners_publicados",
            schema: "cms",
            table: "banners",
            columns: new[] { "is_active", "starts_at", "ends_at" });

        // La unicidad de plataforma ignora mayúsculas pero respeta tildes.
        migrationBuilder.Sql(
            "ALTER TABLE cms.social_links ALTER COLUMN platform TYPE text COLLATE core.es_ci;");

        migrationBuilder.CreateIndex(
            name: "uq_social_links_plataforma",
            schema: "cms",
            table: "social_links",
            column: "platform",
            unique: true);

        // Referencias duras a medios: todas uuid → uuid (ADR-018).
        migrationBuilder.Sql(
            """
            ALTER TABLE cms.banners
                ADD CONSTRAINT fk_banners_image_desktop_id
                FOREIGN KEY (image_desktop_id) REFERENCES core.media_assets (media_asset_id)
                ON DELETE RESTRICT;
            ALTER TABLE cms.banners
                ADD CONSTRAINT fk_banners_image_mobile_id
                FOREIGN KEY (image_mobile_id) REFERENCES core.media_assets (media_asset_id)
                ON DELETE SET NULL;
            ALTER TABLE cms.promotions
                ADD CONSTRAINT fk_promotions_image_id
                FOREIGN KEY (image_id) REFERENCES core.media_assets (media_asset_id)
                ON DELETE SET NULL;
            ALTER TABLE cms.featured_products
                ADD CONSTRAINT fk_featured_products_image_id
                FOREIGN KEY (image_id) REFERENCES core.media_assets (media_asset_id)
                ON DELETE SET NULL;
            ALTER TABLE cms.featured_projects
                ADD CONSTRAINT fk_featured_projects_image_id
                FOREIGN KEY (image_id) REFERENCES core.media_assets (media_asset_id)
                ON DELETE RESTRICT;
            """);

        // product_id queda deliberadamente sin FK: catalog es dependencia blanda.
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION cms.set_updated_at()
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
                     BEFORE UPDATE ON cms.{table}
                     FOR EACH ROW
                     EXECUTE FUNCTION cms.set_updated_at();
                 """);
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "banners", schema: "cms");
        migrationBuilder.DropTable(name: "featured_products", schema: "cms");
        migrationBuilder.DropTable(name: "featured_projects", schema: "cms");
        migrationBuilder.DropTable(name: "promotions", schema: "cms");
        migrationBuilder.DropTable(name: "social_links", schema: "cms");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS cms.set_updated_at();");
    }
}
