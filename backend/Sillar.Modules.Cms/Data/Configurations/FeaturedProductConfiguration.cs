using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Data.Configurations;

internal sealed class FeaturedProductConfiguration : IEntityTypeConfiguration<FeaturedProduct>
{
    public void Configure(EntityTypeBuilder<FeaturedProduct> builder)
    {
        builder.ToTable("featured_products", table =>
        {
            table.HasCheckConstraint("ck_featured_products_product_name_no_vacio", CmsConfiguration.NotEmpty("product_name"));
            table.HasCheckConstraint("ck_featured_products_product_slug_no_vacio", CmsConfiguration.OptionalNotEmpty("product_slug"));
            table.HasCheckConstraint("ck_featured_products_product_price", "product_price IS NULL OR product_price >= 0");
            table.HasCheckConstraint("ck_featured_products_product_category_no_vacia", CmsConfiguration.OptionalNotEmpty("product_category"));
            table.HasCheckConstraint("ck_featured_products_display_order", "display_order >= 0");
            table.HasCheckConstraint("ck_featured_products_vigencia", CmsConfiguration.ValidPeriod);
        });

        builder.MapCommon("featured_products");
        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.ProductName).HasColumnName("product_name").IsRequired();
        builder.Property(x => x.ProductSlug).HasColumnName("product_slug");
        builder.Property(x => x.ImageId).HasColumnName("image_id");
        builder.Property(x => x.ProductPrice).HasColumnName("product_price").HasPrecision(10, 2);
        builder.Property(x => x.ProductPriceVaries).HasColumnName("product_price_varies");
        builder.Property(x => x.ProductCategory).HasColumnName("product_category");
        builder.Property(x => x.ProductIsPublic).HasColumnName("product_is_public");
        builder.Property(x => x.ProductIsActive).HasColumnName("product_is_active").HasDefaultValue(true);
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(x => x.StartsAt).HasColumnName("starts_at").HasColumnType("timestamptz");
        builder.Property(x => x.EndsAt).HasColumnName("ends_at").HasColumnType("timestamptz");
    }
}
