using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Sillar.Modules.Cms.Data;

#nullable disable

namespace Sillar.Modules.Cms.Migrations;

/// <summary>Modelo de M02 posterior a la migración inicial.</summary>
[DbContext(typeof(CmsDbContext))]
internal sealed class CmsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasDefaultSchema("cms")
            .HasAnnotation("ProductVersion", "10.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Sillar.Modules.Cms.Domain.Banner", b =>
        {
            MapCommon(b, "pk_banners");
            b.Property<string>("AltText").HasColumnType("text").HasColumnName("alt_text");
            b.Property<int>("DisplayOrder").HasColumnType("integer").HasDefaultValue(0).HasColumnName("display_order");
            b.Property<DateTimeOffset?>("EndsAt").HasColumnType("timestamptz").HasColumnName("ends_at");
            b.Property<Guid?>("ImageDesktopId").HasColumnType("uuid").HasColumnName("image_desktop_id");
            b.Property<Guid?>("ImageMobileId").HasColumnType("uuid").HasColumnName("image_mobile_id");
            b.Property<string>("LinkLabel").HasColumnType("text").HasColumnName("link_label");
            b.Property<string>("LinkUrl").HasColumnType("text").HasColumnName("link_url");
            b.Property<DateTimeOffset?>("StartsAt").HasColumnType("timestamptz").HasColumnName("starts_at");
            b.Property<string>("Subtitle").HasColumnType("text").HasColumnName("subtitle");
            b.Property<string>("Title").HasColumnType("text").HasColumnName("title");
            b.HasIndex("IsActive", "StartsAt", "EndsAt").HasDatabaseName("idx_banners_publicados");
            b.ToTable("banners", "cms", t =>
            {
                t.HasCheckConstraint("ck_banners_alt_text_no_vacio", "alt_text IS NULL OR btrim(alt_text) <> ''");
                t.HasCheckConstraint("ck_banners_alt_text_si_hay_imagen", "(image_desktop_id IS NULL AND image_mobile_id IS NULL) OR alt_text IS NOT NULL");
                t.HasCheckConstraint("ck_banners_display_order", "display_order >= 0");
                t.HasCheckConstraint("ck_banners_enlace", "link_url IS NULL OR (link_label IS NOT NULL AND btrim(link_label) <> '')");
                t.HasCheckConstraint("ck_banners_link_url", "link_url IS NULL OR link_url COLLATE \"C\" ~ '^(/|https?://)'");
                t.HasCheckConstraint("ck_banners_title_no_vacio", "title IS NULL OR btrim(title) <> ''");
                t.HasCheckConstraint("ck_banners_vigencia", "starts_at IS NULL OR ends_at IS NULL OR ends_at > starts_at");
            });
        });

        modelBuilder.Entity("Sillar.Modules.Cms.Domain.Promotion", b =>
        {
            MapCommon(b, "pk_promotions");
            b.Property<string>("AltText").HasColumnType("text").HasColumnName("alt_text");
            b.Property<string>("BadgeText").HasMaxLength(20).HasColumnType("text").HasColumnName("badge_text");
            b.Property<string>("Description").HasColumnType("text").HasColumnName("description");
            b.Property<int>("DisplayOrder").HasColumnType("integer").HasDefaultValue(0).HasColumnName("display_order");
            b.Property<DateTimeOffset?>("EndsAt").HasColumnType("timestamptz").HasColumnName("ends_at");
            b.Property<Guid?>("ImageId").HasColumnType("uuid").HasColumnName("image_id");
            b.Property<string>("LinkLabel").HasColumnType("text").HasColumnName("link_label");
            b.Property<string>("LinkUrl").HasColumnType("text").HasColumnName("link_url");
            b.Property<DateTimeOffset?>("StartsAt").HasColumnType("timestamptz").HasColumnName("starts_at");
            b.Property<string>("Subtitle").HasColumnType("text").HasColumnName("subtitle");
            b.Property<string>("Title").HasColumnType("text").HasColumnName("title");
            b.ToTable("promotions", "cms", t =>
            {
                t.HasCheckConstraint("ck_promotions_alt_text_no_vacio", "alt_text IS NULL OR btrim(alt_text) <> ''");
                t.HasCheckConstraint("ck_promotions_alt_text_si_hay_imagen", "image_id IS NULL OR alt_text IS NOT NULL");
                t.HasCheckConstraint("ck_promotions_badge_text", "badge_text IS NULL OR (btrim(badge_text) <> '' AND char_length(badge_text) <= 20)");
                t.HasCheckConstraint("ck_promotions_display_order", "display_order >= 0");
                t.HasCheckConstraint("ck_promotions_enlace", "link_url IS NULL OR (link_label IS NOT NULL AND btrim(link_label) <> '')");
                t.HasCheckConstraint("ck_promotions_link_url", "link_url IS NULL OR link_url COLLATE \"C\" ~ '^(/|https?://)'");
                t.HasCheckConstraint("ck_promotions_title_no_vacio", "title IS NULL OR btrim(title) <> ''");
                t.HasCheckConstraint("ck_promotions_vigencia", "starts_at IS NULL OR ends_at IS NULL OR ends_at > starts_at");
            });
        });

        modelBuilder.Entity("Sillar.Modules.Cms.Domain.FeaturedProduct", b =>
        {
            MapCommon(b, "pk_featured_products");
            b.Property<int>("DisplayOrder").HasColumnType("integer").HasDefaultValue(0).HasColumnName("display_order");
            b.Property<DateTimeOffset?>("EndsAt").HasColumnType("timestamptz").HasColumnName("ends_at");
            b.Property<Guid?>("ImageId").HasColumnType("uuid").HasColumnName("image_id");
            b.Property<Guid?>("ProductId").HasColumnType("uuid").HasColumnName("product_id");
            b.Property<string>("ProductName").IsRequired().HasColumnType("text").HasColumnName("product_name");
            b.Property<string>("ProductSlug").HasColumnType("text").HasColumnName("product_slug");
            b.Property<DateTimeOffset?>("StartsAt").HasColumnType("timestamptz").HasColumnName("starts_at");
            b.ToTable("featured_products", "cms", t =>
            {
                t.HasCheckConstraint("ck_featured_products_display_order", "display_order >= 0");
                t.HasCheckConstraint("ck_featured_products_product_name_no_vacio", "btrim(product_name) <> ''");
                t.HasCheckConstraint("ck_featured_products_product_slug_no_vacio", "product_slug IS NULL OR btrim(product_slug) <> ''");
                t.HasCheckConstraint("ck_featured_products_vigencia", "starts_at IS NULL OR ends_at IS NULL OR ends_at > starts_at");
            });
        });

        modelBuilder.Entity("Sillar.Modules.Cms.Domain.FeaturedProject", b =>
        {
            MapCommon(b, "pk_featured_projects");
            b.Property<string>("AltText").HasColumnType("text").HasColumnName("alt_text");
            b.Property<string>("Description").HasColumnType("text").HasColumnName("description");
            b.Property<int>("DisplayOrder").HasColumnType("integer").HasDefaultValue(0).HasColumnName("display_order");
            b.Property<Guid?>("ImageId").HasColumnType("uuid").HasColumnName("image_id");
            b.Property<string>("Title").IsRequired().HasColumnType("text").HasColumnName("title");
            b.ToTable("featured_projects", "cms", t =>
            {
                t.HasCheckConstraint("ck_featured_projects_alt_text_no_vacio", "alt_text IS NULL OR btrim(alt_text) <> ''");
                t.HasCheckConstraint("ck_featured_projects_alt_text_si_hay_imagen", "image_id IS NULL OR alt_text IS NOT NULL");
                t.HasCheckConstraint("ck_featured_projects_display_order", "display_order >= 0");
                t.HasCheckConstraint("ck_featured_projects_title_no_vacio", "btrim(title) <> ''");
            });
        });

        modelBuilder.Entity("Sillar.Modules.Cms.Domain.SocialLink", b =>
        {
            MapCommon(b, "pk_social_links");
            b.Property<int>("DisplayOrder").HasColumnType("integer").HasDefaultValue(0).HasColumnName("display_order");
            b.Property<string>("Platform").IsRequired().HasColumnType("text").HasColumnName("platform");
            b.Property<string>("Url").IsRequired().HasColumnType("text").HasColumnName("url");
            b.HasIndex("Platform").IsUnique().HasDatabaseName("uq_social_links_plataforma");
            b.ToTable("social_links", "cms", t =>
            {
                t.HasCheckConstraint("ck_social_links_display_order", "display_order >= 0");
                t.HasCheckConstraint("ck_social_links_plataforma", "platform IN ('facebook', 'instagram', 'tiktok', 'whatsapp', 'youtube')");
                t.HasCheckConstraint("ck_social_links_url", "url COLLATE \"C\" ~ '^https?://[^[:space:]]+$'");
            });
        });
#pragma warning restore 612, 618
    }

    private static void MapCommon(EntityTypeBuilder b, string primaryKey)
    {
        var id = b.Property<int>("Id")
            .ValueGeneratedOnAdd()
            .HasColumnType("integer")
            .HasColumnName("id");
        NpgsqlPropertyBuilderExtensions.UseIdentityAlwaysColumn(id);

        b.Property<DateTimeOffset>("CreatedAt")
            .ValueGeneratedOnAdd()
            .HasColumnType("timestamptz")
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");
        b.Property<bool>("IsActive")
            .ValueGeneratedOnAdd()
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .HasColumnName("is_active");
        b.Property<DateTimeOffset>("UpdatedAt")
            .ValueGeneratedOnAdd()
            .HasColumnType("timestamptz")
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");
        b.HasKey("Id").HasName(primaryKey);
    }
}
