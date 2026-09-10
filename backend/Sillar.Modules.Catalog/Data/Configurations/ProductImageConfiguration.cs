using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Catalog.Domain;
using Sillar.Shared.Data.Replication;

namespace Sillar.Modules.Catalog.Data.Configurations;

internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images", table =>
        {
            table.HasCheckConstraint("ck_product_images_sort_order", "sort_order >= 0");
        });

        builder.MapCatalogKey("product_images");
        builder.MapReplication();

        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.MediaAssetId).HasColumnName("media_asset_id");
        builder.Property(x => x.AltText).HasColumnName("alt_text");

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0)
            .ValueGeneratedNever();

        builder.Property(x => x.IsPrimary)
            .HasColumnName("is_primary")
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder.HasIndex(x => x.ProductId).HasDatabaseName("idx_product_images_producto");

        // Máximo una principal por producto. Parcial sobre is_primary: las que
        // no lo son quedan fuera del índice y no compiten.
        builder.HasIndex(x => x.ProductId)
            .IsUnique()
            .HasDatabaseName("uq_product_images_una_principal")
            .HasFilter("is_primary");

        // La misma imagen no se asocia dos veces al mismo producto.
        builder.HasIndex(x => new { x.ProductId, x.MediaAssetId })
            .IsUnique()
            .HasDatabaseName("uq_product_images_producto_archivo");

        builder.HasOne(x => x.Product)
            .WithMany(x => x.Images)
            .HasForeignKey(x => x.ProductId)
            .HasConstraintName("fk_product_images_product_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
