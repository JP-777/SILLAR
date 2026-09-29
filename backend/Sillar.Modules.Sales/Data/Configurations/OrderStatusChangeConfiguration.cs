using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Domain;
using Sillar.Shared.Data.Replication;

namespace Sillar.Modules.Sales.Data.Configurations;

internal sealed class OrderStatusChangeConfiguration : IEntityTypeConfiguration<OrderStatusChange>
{
    public void Configure(EntityTypeBuilder<OrderStatusChange> builder)
    {
        var valores = string.Join(", ", OrderStatus.All.Select(s => $"'{s}'"));

        builder.ToTable("order_status_changes", table =>
        {
            table.HasCheckConstraint("ck_order_status_changes_to_status", $"to_status IN ({valores})");

            // from_status admite nulo solo en el primer asiento, cuando el pedido
            // nace y no venía de ningún estado.
            table.HasCheckConstraint(
                "ck_order_status_changes_from_status",
                $"from_status IS NULL OR from_status IN ({valores})");

            table.HasCheckConstraint(
                "ck_order_status_changes_no_es_el_mismo",
                "from_status IS NULL OR from_status <> to_status");

            // Nulo significa «lo hizo el sistema» —el vencimiento del plazo lo
            // provoca el tiempo—. Una cadena en blanco diría que alguien lo hizo y
            // no sabemos quién, que es peor que decir que no fue nadie.
            table.HasCheckConstraint(
                "ck_order_status_changes_changed_by_no_vacio",
                "changed_by IS NULL OR btrim(changed_by) <> ''");

            // La atribución va junta o no va: las dos nulas es «lo hizo el
            // sistema», las dos presentes es una persona. Media atribución es peor
            // que ninguna porque parece completa, y esto lo impide la base y no una
            // convención.
            table.HasCheckConstraint(
                "ck_order_status_changes_atribucion_completa",
                "(changed_by IS NULL AND changed_by_admin_user_id_origin IS NULL) " +
                "OR (changed_by IS NOT NULL AND changed_by_admin_user_id_origin IS NOT NULL)");

            // Cero prohibido: sería un trabajador ficticio con apariencia de real.
            table.HasCheckConstraint(
                "ck_order_status_changes_atribucion_local_positiva",
                "changed_by_admin_user_id_origin IS NULL OR changed_by_admin_user_id_origin > 0");
        });

        builder.HasKey(x => x.OrderStatusChangeId).HasName("pk_order_status_changes");

        builder.Property(x => x.OrderStatusChangeId)
            .HasColumnName("order_status_change_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(x => x.OrderId).HasColumnName("order_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.FromStatus).HasColumnName("from_status");
        builder.Property(x => x.ToStatus).HasColumnName("to_status").IsRequired();

        builder.Property(x => x.ChangedAt)
            .HasColumnName("changed_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // La atribución es un PAR. Jamás una FK a core.admin_users.
        builder.Property(x => x.ChangedBy).HasColumnName("changed_by");

        // Dato de bitácora, no puntero. Se interpreta junto al origin_node de ESTA
        // fila, no del pedido: pueden diferir, y la política de ese caso está
        // abierta (SPEC §0.3 (k)).
        builder.Property(x => x.ChangedByAdminUserIdOrigin)
            .HasColumnName("changed_by_admin_user_id_origin");

        builder.MapReplication();

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .HasConstraintName("fk_order_status_changes_order_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.OrderId).HasDatabaseName("idx_order_status_changes_order_id");
    }
}
