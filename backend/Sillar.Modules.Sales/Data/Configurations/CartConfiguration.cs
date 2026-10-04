using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Sales.Domain;

namespace Sillar.Modules.Sales.Data.Configurations;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("carts");

        builder.HasKey(x => x.CartId).HasName("pk_carts");

        // integer IDENTITY: el carrito no se replica (ADR-017 lo pone en el lado
        // exclusivo de WEB), así que no lleva uuid ni columnas de replicación.
        builder.Property(x => x.CartId)
            .HasColumnName("cart_id")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.CustomerId).HasColumnName("customer_id").HasColumnType("uuid").IsRequired();

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

        // Un carrito abierto por cliente. Si el cliente vuelve, encuentra el suyo;
        // no acumula uno por visita.
        builder.HasIndex(x => x.CustomerId)
            .IsUnique()
            .HasDatabaseName("uq_carts_customer_id");
    }
}
