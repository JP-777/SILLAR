using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Data.Configurations;

internal sealed class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.ToTable("banners", table =>
        {
            table.HasCheckConstraint("ck_banners_title_no_vacio", CmsConfiguration.OptionalNotEmpty("title"));
            table.HasCheckConstraint("ck_banners_alt_text_si_hay_imagen", CmsConfiguration.BannerAltTextWithImage);
            table.HasCheckConstraint("ck_banners_alt_text_no_vacio", CmsConfiguration.OptionalNotEmpty("alt_text"));
            table.HasCheckConstraint("ck_banners_display_order", "display_order >= 0");
            table.HasCheckConstraint("ck_banners_vigencia", CmsConfiguration.ValidPeriod);
            table.HasCheckConstraint("ck_banners_enlace", CmsConfiguration.ValidLink);
            table.HasCheckConstraint("ck_banners_link_url", CmsConfiguration.ValidLinkUrl);
        });

        builder.MapCommon("banners");
        builder.Property(x => x.Title).HasColumnName("title");
        builder.Property(x => x.Subtitle).HasColumnName("subtitle");
        builder.Property(x => x.ImageDesktopId).HasColumnName("image_desktop_id");
        builder.Property(x => x.ImageMobileId).HasColumnName("image_mobile_id");
        builder.Property(x => x.AltText).HasColumnName("alt_text");
        builder.Property(x => x.LinkUrl).HasColumnName("link_url");
        builder.Property(x => x.LinkLabel).HasColumnName("link_label");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(x => x.StartsAt).HasColumnName("starts_at").HasColumnType("timestamptz");
        builder.Property(x => x.EndsAt).HasColumnName("ends_at").HasColumnType("timestamptz");
        builder.HasIndex(x => new { x.IsActive, x.StartsAt, x.EndsAt }).HasDatabaseName("idx_banners_publicados");
    }
}
