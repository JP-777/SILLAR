using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Sales.Domain;

namespace Sillar.Modules.Sales.Data.Configurations;

internal sealed class OrderSeriesConfiguration : IEntityTypeConfiguration<OrderSeries>
{
    public void Configure(EntityTypeBuilder<OrderSeries> builder)
    {
        builder.ToTable("order_series", table =>
        {
            table.HasCheckConstraint("ck_order_series_node_code_no_vacio", "btrim(node_code) <> ''");
            table.HasCheckConstraint("ck_order_series_last_number_no_negativo", "last_number >= 0");

            // Un año de cuatro cifras. No es cosmético: protege de que un reloj mal
            // configurado abra una serie del año 20 o del 12026, que serían códigos
            // visibles imposibles de dictar y de ordenar.
            table.HasCheckConstraint("ck_order_series_year_razonable", "year BETWEEN 2000 AND 9999");
        });

        builder.HasKey(x => x.OrderSeriesId).HasName("pk_order_series");

        // integer IDENTITY, no uuid: esta tabla NO se replica. La pregunta de la
        // ADR-016 se responde con un no rotundo — un contador que existiera en dos
        // nodos dejaría de contar.
        builder.Property(x => x.OrderSeriesId)
            .HasColumnName("order_series_id")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.NodeCode).HasColumnName("node_code").IsRequired();
        builder.Property(x => x.Year).HasColumnName("year").IsRequired();

        builder.Property(x => x.LastNumber)
            .HasColumnName("last_number")
            .HasDefaultValue(0)
            .ValueGeneratedNever();

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

        // La unicidad de (nodo, año) es lo que hace posible el INSERT … ON CONFLICT
        // del asignador: sin ella, dos transacciones simultáneas del primer pedido
        // de un año crearían dos filas y cada una contaría por su cuenta.
        builder.HasIndex(x => new { x.NodeCode, x.Year })
            .IsUnique()
            .HasDatabaseName("uq_order_series_node_code_year");
    }
}
