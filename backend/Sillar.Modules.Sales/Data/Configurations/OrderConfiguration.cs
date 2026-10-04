using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Domain;
using Sillar.Shared.Data.Replication;

namespace Sillar.Modules.Sales.Data.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", table =>
        {
            // Los siete estados, nombrados desde el contrato: la lista vive en un
            // sitio y el CHECK se escribe desde ella, para que no puedan separarse.
            table.HasCheckConstraint(
                "ck_orders_status",
                $"status IN ({string.Join(", ", OrderStatus.All.Select(s => $"'{s}'"))})");

            table.HasCheckConstraint("ck_orders_order_code_no_vacio", "btrim(order_code) <> ''");
            table.HasCheckConstraint("ck_orders_customer_full_name_no_vacio", "btrim(customer_full_name) <> ''");
            table.HasCheckConstraint("ck_orders_customer_email_no_vacio", "btrim(customer_email) <> ''");
            table.HasCheckConstraint("ck_orders_total_amount_no_negativo", "total_amount >= 0");
        });

        builder.HasKey(x => x.OrderId).HasName("pk_orders");

        builder.Property(x => x.OrderId)
            .HasColumnName("order_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(x => x.OrderCode).HasColumnName("order_code").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.CustomerFullName).HasColumnName("customer_full_name").IsRequired();
        builder.Property(x => x.CustomerEmail).HasColumnName("customer_email").IsRequired();
        builder.Property(x => x.CustomerPhone).HasColumnName("customer_phone");

        // Sin CHECK que replique ck_customers_document_type: esa restricción es de
        // M04, y copiarla ataría el historial de pedidos a una regla ajena.
        builder.Property(x => x.CustomerDocumentType).HasColumnName("customer_document_type");
        builder.Property(x => x.CustomerDocumentNumber).HasColumnName("customer_document_number");

        builder.Property(x => x.Status).HasColumnName("status").IsRequired();

        builder.Property(x => x.PaymentDueAt)
            .HasColumnName("payment_due_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.MapReplication();

        // La clave foránea cruzada hacia crm.customers NO se declara aquí: va como
        // SQL en la migración, igual que las cuatro de Catalog hacia
        // core.media_assets. EF no puede descubrirla porque no hay —ni puede
        // haber— propiedad de navegación hacia la entidad de otro módulo: un
        // módulo nunca mapea las tablas de otro.

        builder.HasIndex(x => x.OrderCode)
            .IsUnique()
            .HasDatabaseName("uq_orders_order_code");

        builder.HasIndex(x => x.CustomerId).HasDatabaseName("idx_orders_customer_id");
        builder.HasIndex(x => x.Status).HasDatabaseName("idx_orders_status");
        builder.HasIndex(x => x.PaymentDueAt).HasDatabaseName("idx_orders_payment_due_at");
    }
}
