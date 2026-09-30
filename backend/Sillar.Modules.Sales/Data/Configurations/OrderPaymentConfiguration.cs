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

            // La atribución de un pago es completa SIEMPRE, con sus tres datos: un
            // pago lo registra una persona, no el sistema. Cada CHECK prohíbe un
            // valor ficticio distinto, y los tres hacen falta: el cero sería un
            // trabajador que no existe con apariencia de real, y un nodo en blanco
            // dejaría el identificador sin universo.
            table.HasCheckConstraint(
                "ck_order_payments_atribucion_local_positiva",
                "registered_by_admin_user_local_id > 0");

            table.HasCheckConstraint(
                "ck_order_payments_atribucion_home_node_no_vacio",
                "btrim(registered_by_admin_user_home_node) <> ''");
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

        // La atribución son TRES datos que forman una unidad, y en un pago los tres
        // son obligatorios porque un pago lo registra siempre una persona:
        //
        //   registered_by                        nombre congelado al actuar
        //   registered_by_admin_user_local_id    identificador dentro de su nodo
        //   registered_by_admin_user_home_node   nodo al que pertenece la CUENTA
        //
        // origin_node es independiente de los tres: dice dónde OCURRIÓ LA ACTUACIÓN,
        // y puede diferir del nodo de la cuenta —una cuenta del nodo A puede registrar
        // un pago desde el nodo B—. Ningún CHECK exige que coincidan, a propósito.
        //
        // Jamás una FK a core.admin_users: esa tabla no se replica y esta sí (ADR-018).
        builder.Property(x => x.RegisteredBy).HasColumnName("registered_by").IsRequired();

        // Dato de bitácora, no puntero. Se interpreta contra el nodo de pertenencia
        // de la cuenta —la columna de abajo—, NO contra origin_node.
        builder.Property(x => x.RegisteredByAdminUserLocalId)
            .HasColumnName("registered_by_admin_user_local_id")
            .IsRequired();

        // El nodo de la CUENTA. No es origin_node, que dice dónde ocurrió la
        // actuación, y no se deriva de él: una cuenta del nodo A puede registrar un
        // pago desde el nodo B. No hay CHECK que exija que coincidan, a propósito.
        builder.Property(x => x.RegisteredByAdminUserHomeNode)
            .HasColumnName("registered_by_admin_user_home_node")
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
