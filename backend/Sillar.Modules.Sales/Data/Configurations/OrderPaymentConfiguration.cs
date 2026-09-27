using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Domain;
using Sillar.Shared.Data.Replication;

namespace Sillar.Modules.Sales.Data.Configurations;

internal sealed class OrderPaymentConfiguration : IEntityTypeConfiguration<OrderPayment>
{
    public void Configure(EntityTypeBuilder<OrderPayment> builder)
    {
        builder.ToTable("order_payments", table =>
        {
            // Solo lo ratificado. El efectivo está escalado, no autorizado ni
            // eliminado: añadir un valor después es barato, quitarlo con filas no.
            table.HasCheckConstraint(
                "ck_order_payments_method",
                $"method IN ({string.Join(", ", PaymentMethod.All.Select(m => $"'{m}'"))})");

            table.HasCheckConstraint("ck_order_payments_amount_no_negativo", "amount >= 0");
            table.HasCheckConstraint("ck_order_payments_registered_by_no_vacio", "btrim(registered_by) <> ''");
        });

        builder.HasKey(x => x.OrderPaymentId).HasName("pk_order_payments");

        builder.Property(x => x.OrderPaymentId)
            .HasColumnName("order_payment_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(x => x.OrderId).HasColumnName("order_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.Method).HasColumnName("method").IsRequired();

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.Property(x => x.Reference).HasColumnName("reference");

        // El NOMBRE, jamás una FK a core.admin_users: esa tabla no se replica y
        // esta sí (ADR-018), y el dato que hace falta dentro de un año es quién
        // cobró — que sobrevive a que la cuenta se dé de baja o se renombre.
        builder.Property(x => x.RegisteredBy).HasColumnName("registered_by").IsRequired();

        builder.Property(x => x.RegisteredAt)
            .HasColumnName("registered_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.WasLate)
            .HasColumnName("was_late")
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder.MapReplication();

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .HasConstraintName("fk_order_payments_order_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.OrderId).HasDatabaseName("idx_order_payments_order_id");

        // Encuentra los pagos tardíos sin recalcular fechas: was_late es un hecho
        // guardado, no una comparación que se rehace después.
        builder.HasIndex(x => x.WasLate)
            .HasDatabaseName("idx_order_payments_was_late")
            .HasFilter("was_late");
    }
}
