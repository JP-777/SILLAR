using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Data.Configurations;

internal sealed class SocialLinkConfiguration : IEntityTypeConfiguration<SocialLink>
{
    public void Configure(EntityTypeBuilder<SocialLink> builder)
    {
        builder.ToTable("social_links", table =>
        {
            table.HasCheckConstraint("ck_social_links_plataforma", "platform IN ('facebook', 'instagram', 'tiktok', 'whatsapp', 'youtube')");
            table.HasCheckConstraint("ck_social_links_url", "url COLLATE \"C\" ~ '^https?://[^[:space:]]+$'");
            table.HasCheckConstraint("ck_social_links_display_order", "display_order >= 0");
        });

        builder.MapCommon("social_links");
        builder.Property(x => x.Platform).HasColumnName("platform").IsRequired();
        builder.Property(x => x.Url).HasColumnName("url").IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.HasIndex(x => x.Platform).IsUnique().HasDatabaseName("uq_social_links_plataforma");
    }
}
