using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Catalog.Domain;

namespace Sillar.Modules.Catalog.Data.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", table =>
        {
            table.HasCheckConstraint("ck_products_name_no_vacio", Check.NotEmpty("name"));
            table.HasCheckConstraint("ck_products_slug_formato", Check.SlugFormat("slug"));
            // Nulo es «consultar precio»; cero es gratis. El CHECK deja pasar
            // ambos y solo prohíbe lo que no significa nada: un precio negativo.
            table.HasCheckConstraint("ck_products_list_price_no_negativo", "list_price IS NULL OR list_price >= 0");
        });

        builder.MapCatalogKey("products");
        builder.MapReplication();

        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.Slug).HasColumnName("slug").IsRequired();
        builder.Property(x => x.ShortDescription).HasColumnName("short_description");
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.PrimaryCategoryId).HasColumnName("primary_category_id");
        builder.Property(x => x.BrandId).HasColumnName("brand_id");

        builder.Property(x => x.ListPrice)
            .HasColumnName("list_price")
            .HasColumnType("numeric(12,2)");

        builder.Property(x => x.SaleUnit).HasColumnName("sale_unit");
        builder.Property(x => x.VariantLabel).HasColumnName("variant_label");

        builder.Property(x => x.IsPublic)
            .HasColumnName("is_public")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("uq_products_slug");
        builder.HasIndex(x => x.BrandId).HasDatabaseName("idx_products_marca");
        builder.HasIndex(x => x.PrimaryCategoryId).HasDatabaseName("idx_products_categoria_principal");

        // Lo que consulta la web pública en cada listado.
        builder.HasIndex(x => new { x.IsActive, x.IsPublic })
            .HasDatabaseName("idx_products_publicos")
            .HasFilter("is_active AND is_public");

        // Restrict: quitar una categoría o una marca no puede llevarse los
        // productos por delante. La baja es lógica en todo el proyecto.
        builder.HasOne(x => x.PrimaryCategory)
            .WithMany()
            .HasForeignKey(x => x.PrimaryCategoryId)
            .HasConstraintName("fk_products_primary_category_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Brand)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.BrandId)
            .HasConstraintName("fk_products_brand_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
