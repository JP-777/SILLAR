using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Sales.Domain;

namespace Sillar.Modules.Sales.Data.Configurations;

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items", table =>
        {
            table.HasCheckConstraint("ck_cart_items_quantity_positiva", "quantity > 0");
        });

        builder.HasKey(x => x.CartItemId).HasName("pk_cart_items");

        builder.Property(x => x.CartItemId)
            .HasColumnName("cart_item_id")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.CartId).HasColumnName("cart_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasOne<Cart>()
            .WithMany()
            .HasForeignKey(x => x.CartId)
            .HasConstraintName("fk_cart_items_cart_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Una variante una vez por carrito: añadir la misma otra vez cambia la
        // cantidad, no crea una segunda línea.
        builder.HasIndex(x => new { x.CartId, x.ItemId })
            .IsUnique()
            .HasDatabaseName("uq_cart_items_cart_id_item_id");
    }
}
