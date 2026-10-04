using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Sales.Domain;
using Sillar.Shared.Data.Replication;

namespace Sillar.Modules.Sales.Data.Configurations;

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("order_lines", table =>
        {
            table.HasCheckConstraint("ck_order_lines_quantity_positiva", "quantity > 0");

            // >= 0 y no > 0: cero es GRATIS y se vende. Lo que no llega a ser línea
            // es el precio nulo, que significa «a consultar».
            table.HasCheckConstraint("ck_order_lines_unit_price_no_negativo", "unit_price >= 0");
            table.HasCheckConstraint("ck_order_lines_product_name_no_vacio", "btrim(product_name) <> ''");
        });

        builder.HasKey(x => x.OrderLineId).HasName("pk_order_lines");

        builder.Property(x => x.OrderLineId)
            .HasColumnName("order_line_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(x => x.OrderId).HasColumnName("order_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").HasColumnType("uuid").IsRequired();

        // Sin FK: solo agrupa en informes. Nada vende ni cuenta contra el producto.
        builder.Property(x => x.ProductId).HasColumnName("product_id").HasColumnType("uuid").IsRequired();

        builder.Property(x => x.ProductName).HasColumnName("product_name").IsRequired();
        builder.Property(x => x.VariantValue).HasColumnName("variant_value");
        builder.Property(x => x.SaleUnit).HasColumnName("sale_unit");
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();

        builder.Property(x => x.UnitPrice)
            .HasColumnName("unit_price")
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.MapReplication();

        // FK interna del schema: esta sí la declara EF. Cascade porque una línea sin
        // su pedido no significa nada — y el pedido no se borra físicamente, así que
        // en la práctica solo actúa al desinstalar el módulo.
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .HasConstraintName("fk_order_lines_order_id")
            .OnDelete(DeleteBehavior.Cascade);

        // La FK cruzada hacia catalog.product_items va en la migración, como SQL.

        builder.HasIndex(x => x.OrderId).HasDatabaseName("idx_order_lines_order_id");
        builder.HasIndex(x => x.ItemId).HasDatabaseName("idx_order_lines_item_id");
    }
}
