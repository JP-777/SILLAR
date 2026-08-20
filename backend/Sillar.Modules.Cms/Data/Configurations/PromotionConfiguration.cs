using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Data.Configurations;

internal sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("promotions", table =>
        {
            table.HasCheckConstraint("ck_promotions_title_no_vacio", CmsConfiguration.OptionalNotEmpty("title"));
            table.HasCheckConstraint("ck_promotions_alt_text_si_hay_imagen", CmsConfiguration.AltTextWithImage);
            table.HasCheckConstraint("ck_promotions_alt_text_no_vacio", CmsConfiguration.OptionalNotEmpty("alt_text"));
            table.HasCheckConstraint("ck_promotions_badge_text", "badge_text IS NULL OR (btrim(badge_text) <> '' AND char_length(badge_text) <= 20)");
            table.HasCheckConstraint("ck_promotions_display_order", "display_order >= 0");
            table.HasCheckConstraint("ck_promotions_vigencia", CmsConfiguration.ValidPeriod);
            table.HasCheckConstraint("ck_promotions_enlace", CmsConfiguration.ValidLink);
            table.HasCheckConstraint("ck_promotions_link_url", CmsConfiguration.ValidLinkUrl);
        });

        builder.MapCommon("promotions");
        builder.Property(x => x.Title).HasColumnName("title");
        builder.Property(x => x.Subtitle).HasColumnName("subtitle");
        builder.Property(x => x.ImageId).HasColumnName("image_id");
        builder.Property(x => x.AltText).HasColumnName("alt_text");
        builder.Property(x => x.LinkUrl).HasColumnName("link_url");
        builder.Property(x => x.LinkLabel).HasColumnName("link_label");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(x => x.StartsAt).HasColumnName("starts_at").HasColumnType("timestamptz");
        builder.Property(x => x.EndsAt).HasColumnName("ends_at").HasColumnType("timestamptz");
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.BadgeText).HasColumnName("badge_text").HasColumnType("text").HasMaxLength(20);
    }
}
