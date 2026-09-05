using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Crm.Domain;
using Sillar.Shared.Data.Replication;

namespace Sillar.Modules.Crm.Data.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers", table =>
        {
            table.HasCheckConstraint("ck_customers_full_name_no_vacio", "btrim(full_name) <> ''");
            table.HasCheckConstraint("ck_customers_email_no_vacio", "btrim(email) <> ''");
            table.HasCheckConstraint("ck_customers_document_pair",
                "(document_type IS NULL AND document_number IS NULL) OR (document_type IS NOT NULL AND document_number IS NOT NULL)");
            table.HasCheckConstraint("ck_customers_document_type",
                "document_type IS NULL OR document_type IN ('dni', 'ruc')");
            table.HasCheckConstraint("ck_customers_document_number_no_vacio",
                "document_number IS NULL OR btrim(document_number) <> ''");
            table.HasCheckConstraint("ck_customers_lifecycle_state",
                """
                (
                    is_active = true
                    AND deactivated_at IS NULL
                    AND blocked_at IS NULL
                )
                OR
                (
                    is_active = false
                    AND deactivated_at IS NOT NULL
                    AND blocked_at IS NULL
                )
                OR
                (
                    is_active = false
                    AND deactivated_at IS NULL
                    AND blocked_at IS NOT NULL
                )
                """);
            table.HasCheckConstraint("ck_customers_reactivation_timestamps",
                """
                reactivation_resolved_at IS NULL
                OR (
                    reactivation_requested_at IS NOT NULL
                    AND reactivation_resolved_at >= reactivation_requested_at
                )
                """);
        });

        builder.HasKey(x => x.CustomerId).HasName("pk_customers");

        builder.Property(x => x.CustomerId)
            .HasColumnName("customer_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(x => x.FullName)
            .HasColumnName("full_name")
            .IsRequired();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasColumnType("character varying(150)")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Phone).HasColumnName("phone");
        builder.Property(x => x.DocumentType).HasColumnName("document_type");
        builder.Property(x => x.DocumentNumber).HasColumnName("document_number");
        builder.Property(x => x.InternalNotes).HasColumnName("internal_notes");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.Property(x => x.DeactivatedAt).HasColumnName("deactivated_at").HasColumnType("timestamptz");
        builder.Property(x => x.BlockedAt).HasColumnName("blocked_at").HasColumnType("timestamptz");
        builder.Property(x => x.ReactivationRequestedAt).HasColumnName("reactivation_requested_at").HasColumnType("timestamptz");
        builder.Property(x => x.ReactivationResolvedAt).HasColumnName("reactivation_resolved_at").HasColumnType("timestamptz");

        builder.MapReplication();

        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("uq_customers_email");
        builder.HasIndex(x => new { x.DocumentType, x.DocumentNumber })
            .IsUnique()
            .HasDatabaseName("uq_customers_document")
            .HasFilter("document_number IS NOT NULL");
    }
}
