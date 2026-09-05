using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Crm.Domain;
using Sillar.Shared.Data.Replication;

namespace Sillar.Modules.Crm.Data.Configurations;

internal sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("customer_addresses", table =>
        {
            table.HasCheckConstraint("ck_customer_addresses_address_line_no_vacio", "btrim(address_line) <> ''");
            table.HasCheckConstraint("ck_customer_addresses_preferred_active", "NOT is_preferred OR is_active");
        });

        builder.HasKey(x => x.CustomerAddressId).HasName("pk_customer_addresses");

        builder.Property(x => x.CustomerAddressId)
            .HasColumnName("customer_address_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(x => x.CustomerId).HasColumnName("customer_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.Label).HasColumnName("label");
        builder.Property(x => x.AddressLine).HasColumnName("address_line").IsRequired();
        builder.Property(x => x.District).HasColumnName("district");
        builder.Property(x => x.Province).HasColumnName("province");
        builder.Property(x => x.Department).HasColumnName("department");
        builder.Property(x => x.Reference).HasColumnName("reference");

        builder.Property(x => x.IsPreferred)
            .HasColumnName("is_preferred")
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.MapReplication();

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .HasConstraintName("fk_customer_addresses_customer_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CustomerId)
            .IsUnique()
            .HasDatabaseName("uq_customer_addresses_preferred")
            .HasFilter("is_preferred AND is_active");
    }
}
