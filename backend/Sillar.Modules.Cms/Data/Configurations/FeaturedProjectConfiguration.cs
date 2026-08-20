using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Data.Configurations;

internal sealed class FeaturedProjectConfiguration : IEntityTypeConfiguration<FeaturedProject>
{
    public void Configure(EntityTypeBuilder<FeaturedProject> builder)
    {
        builder.ToTable("featured_projects", table =>
        {
            table.HasCheckConstraint("ck_featured_projects_title_no_vacio", CmsConfiguration.NotEmpty("title"));
            table.HasCheckConstraint("ck_featured_projects_alt_text_no_vacio", CmsConfiguration.NotEmpty("alt_text"));
            table.HasCheckConstraint("ck_featured_projects_display_order", "display_order >= 0");
        });

        builder.MapCommon("featured_projects");
        builder.Property(x => x.Title).HasColumnName("title").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.ImageId).HasColumnName("image_id");
        builder.Property(x => x.AltText).HasColumnName("alt_text").IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
    }
}
