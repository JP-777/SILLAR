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

            // La atribución de un pago es completa siempre: un pago lo registra una
            // persona, no el sistema. El identificador cero queda prohibido porque
            // sería un trabajador ficticio con apariencia de real.
            table.HasCheckConstraint(
                "ck_order_payments_atribucion_local_positiva",
                "registered_by_admin_user_id_origin > 0");
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

        // La atribución es un PAR: nombre congelado más identificador local. Jamás
        // una FK a core.admin_users — esa tabla no se replica y esta sí (ADR-018).
        builder.Property(x => x.RegisteredBy).HasColumnName("registered_by").IsRequired();

        // Dato de bitácora, no puntero. Se interpreta junto al origin_node de ESTA
        // fila, que es el nodo donde la persona actuó.
        builder.Property(x => x.RegisteredByAdminUserIdOrigin)
            .HasColumnName("registered_by_admin_user_id_origin")
            .IsRequired();

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
