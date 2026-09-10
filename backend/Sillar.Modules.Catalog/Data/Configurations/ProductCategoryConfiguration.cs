using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Catalog.Domain;
using Sillar.Shared.Data.Replication;

namespace Sillar.Modules.Catalog.Data.Configurations;

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("product_categories");

        // Clave compuesta y sin id propio: dos uuid ya son globalmente únicos.
        builder.HasKey(x => new { x.ProductId, x.CategoryId }).HasName("pk_product_categories");

        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");

        builder.MapReplication();

        // Para listar los productos de una categoría, que es la consulta de la
        // web pública. La otra dirección ya la cubre la clave primaria.
        builder.HasIndex(x => x.CategoryId).HasDatabaseName("idx_product_categories_categoria");

        builder.HasOne(x => x.Product)
            .WithMany(x => x.Categories)
            .HasForeignKey(x => x.ProductId)
            .HasConstraintName("fk_product_categories_product_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: quitar una categoría con productos debe fallar y que alguien
        // decida. Desactivar una categoría no desactiva sus productos (regla 9).
        builder.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .HasConstraintName("fk_product_categories_category_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
