using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Sillar.Modules.Services.Data;

#nullable disable
namespace Sillar.Modules.Services.Migrations;
[DbContext(typeof(ServicesDbContext))]
[Migration("20260928090000_ServicesInitial")]
public sealed class ServicesInitial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("services");
        migrationBuilder.CreateTable(name: "service_entries", schema: "services", columns: table => new
        {
            id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
            name = table.Column<string>(type: "text", nullable: false), slug = table.Column<string>(type: "text", nullable: false),
            short_description = table.Column<string>(type: "text", nullable: true), description = table.Column<string>(type: "text", nullable: true),
            price = table.Column<decimal>(type: "numeric(12,2)", nullable: true), sale_unit = table.Column<string>(type: "text", nullable: true),
            image_id = table.Column<Guid>(type: "uuid", nullable: true), image_alt_text = table.Column<string>(type: "text", nullable: true),
            publication_state = table.Column<string>(type: "text", nullable: false, defaultValue: "draft"),
            display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
            created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
            updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
        }, constraints: table =>
        {
            table.PrimaryKey("pk_service_entries", x => x.id);
            table.CheckConstraint("ck_service_entries_name_no_vacio", "btrim(name) <> ''");
            table.CheckConstraint("ck_service_entries_slug_formato", "slug COLLATE \"C\" ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'");
            table.CheckConstraint("ck_service_entries_price", "price IS NULL OR price >= 0");
            table.CheckConstraint("ck_service_entries_display_order", "display_order >= 0");
            table.CheckConstraint("ck_service_entries_description", "short_description IS NOT NULL OR description IS NOT NULL");
            table.CheckConstraint("ck_service_entries_image_alt", "image_id IS NULL OR image_alt_text IS NOT NULL");
            table.CheckConstraint("ck_service_entries_publication_state", "publication_state IN ('draft','published','archived')");
            table.ForeignKey("fk_service_entries_image_id", x => x.image_id, "media_assets", "media_asset_id", "core", onDelete: ReferentialAction.SetNull);
        });
        migrationBuilder.Sql("ALTER TABLE services.service_entries ALTER COLUMN name TYPE text COLLATE core.es_search; ALTER TABLE services.service_entries ALTER COLUMN slug TYPE text COLLATE core.es_ci;");
        migrationBuilder.CreateIndex("uq_service_entries_slug", "service_entries", "slug", "services", unique: true);
        migrationBuilder.Sql("""
            CREATE FUNCTION services.set_updated_at() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN NEW.updated_at := now(); RETURN NEW; END; $$;
            CREATE TRIGGER trg_service_entries_set_updated_at BEFORE UPDATE ON services.service_entries
            FOR EACH ROW EXECUTE FUNCTION services.set_updated_at();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("service_entries", "services");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS services.set_updated_at();");
    }
}
