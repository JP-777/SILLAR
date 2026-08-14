using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;
using Sillar.Core.Domain.Values;

namespace Sillar.Core.Data.Configurations;

internal sealed class InstallationConfiguration : IEntityTypeConfiguration<Installation>
{
    public void Configure(EntityTypeBuilder<Installation> builder)
    {
        builder.ToTable("installation", table =>
        {
            // El par de restricciones que impide una segunda fila: 'singleton'
            // solo puede valer true y además es único.
            table.HasCheckConstraint("ck_installation_singleton", "singleton");
            table.HasCheckConstraint(
                "ck_installation_license_type",
                Check.OneOf("license_type", LicenseType.All));
            table.HasCheckConstraint(
                "ck_installation_business_name_not_empty",
                Check.NotEmpty("business_name"));
            table.HasCheckConstraint(
                "ck_installation_product_version_not_empty",
                Check.NotEmpty("product_version"));
        });

        builder.HasKey(x => x.InstallationId).HasName("pk_installation");

        builder.Property(x => x.InstallationId)
            .HasColumnName("installation_id")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.Singleton)
            .HasColumnName("singleton")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.Property(x => x.BusinessName)
            .HasColumnName("business_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.InstallationKey)
            .HasColumnName("installation_key")
            .IsRequired();

        builder.Property(x => x.ProductVersion)
            .HasColumnName("product_version")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.LicenseType)
            .HasColumnName("license_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.LicensedUntil)
            .HasColumnName("licensed_until")
            .HasColumnType("timestamptz");

        builder.Property(x => x.IsSetupComplete)
            .HasColumnName("is_setup_complete")
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder.Property(x => x.CreatedAt).AsCreatedAt();
        builder.Property(x => x.UpdatedAt).AsUpdatedAt();

        builder.HasIndex(x => x.Singleton)
            .IsUnique()
            .HasDatabaseName("uq_installation_singleton");

        builder.HasIndex(x => x.InstallationKey)
            .IsUnique()
            .HasDatabaseName("uq_installation_installation_key");
    }
}
