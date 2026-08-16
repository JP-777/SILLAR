using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Catalog.Domain;

namespace Sillar.Modules.Catalog.Data.Configurations;

internal sealed class ProductItemConfiguration : IEntityTypeConfiguration<ProductItem>
{
    public void Configure(EntityTypeBuilder<ProductItem> builder)
    {
        builder.ToTable("product_items", table =>
        {
            table.HasCheckConstraint(
                "ck_product_items_price_no_negativo",
                "price_override IS NULL OR price_override >= 0");
            table.HasCheckConstraint("ck_product_items_sort_order", "sort_order >= 0");
            // Si tiene valor, no puede ser una cadena en blanco: eso sería una
            // variante sin nombre que no es la variante única.
            table.HasCheckConstraint(
                "ck_product_items_variant_value_no_vacio",
                "variant_value IS NULL OR btrim(variant_value) <> ''");
            table.HasCheckConstraint("ck_product_items_code_no_vacio", "code IS NULL OR btrim(code) <> ''");
            table.HasCheckConstraint("ck_product_items_barcode_no_vacio", "barcode IS NULL OR btrim(barcode) <> ''");
        });

        builder.MapCatalogKey("product_items");
        builder.MapReplication();

        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.VariantValue).HasColumnName("variant_value");
        builder.Property(x => x.Code).HasColumnName("code");
        builder.Property(x => x.Barcode).HasColumnName("barcode");

        builder.Property(x => x.PriceOverride)
            .HasColumnName("price_override")
            .HasColumnType("numeric(12,2)");

        builder.Property(x => x.ImageId).HasColumnName("image_id");

        builder.Property(x => x.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0)
            .ValueGeneratedNever();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        // ÚNICOS PARCIALES. El filtro no es un detalle: sin él, dos platos sin
        // código chocarían entre sí, porque un índice único normal trataría los
        // nulos como distintos en PostgreSQL pero la intención se perdería al
        // leer el esquema. Con el filtro queda dicho: solo se comparan los que
        // existen.
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("uq_product_items_code")
            .HasFilter("code IS NOT NULL");

        builder.HasIndex(x => x.Barcode)
            .IsUnique()
            .HasDatabaseName("uq_product_items_barcode")
            .HasFilter("barcode IS NOT NULL");

        // Dos variantes del mismo producto no pueden llamarse igual. La única
        // sin nombre queda fuera del índice, así que no estorba.
        builder.HasIndex(x => new { x.ProductId, x.VariantValue })
            .IsUnique()
            .HasDatabaseName("uq_product_items_valor")
            .HasFilter("variant_value IS NOT NULL");

        builder.HasIndex(x => x.ProductId).HasDatabaseName("idx_product_items_producto");

        // Cascade aquí sí: una variante no existe sin su producto. Y el producto
        // no se borra nunca —la baja es lógica—, así que esto solo actúa si
        // alguien limpia por SQL.
        builder.HasOne(x => x.Product)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.ProductId)
            .HasConstraintName("fk_product_items_product_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
