using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;

namespace Sillar.Core.Data.Configurations;

internal sealed class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.ToTable("modules", table =>
        {
            table.HasCheckConstraint("ck_modules_code_not_empty", Check.NotEmpty("code"));
            // El código es también el nombre de un schema de PostgreSQL: si algo
            // se cuela con mayúsculas o con un guion, el schema no se puede
            // crear. La misma regla la valida el host antes de arrancar.
            table.HasCheckConstraint("ck_modules_code_format", "code ~ '^[a-z][a-z0-9_]{1,39}$'");
            table.HasCheckConstraint("ck_modules_display_name_not_empty", Check.NotEmpty("display_name"));
            table.HasCheckConstraint("ck_modules_version_not_empty", Check.NotEmpty("version"));
        });

        builder.HasKey(x => x.ModuleId).HasName("pk_modules");

        builder.Property(x => x.ModuleId)
            .HasColumnName("module_id")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(300);

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.IsCore)
            .HasColumnName("is_core")
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder.Property(x => x.DisplayOrder)
            .HasColumnName("display_order")
            .HasDefaultValue(0)
            .ValueGeneratedNever();

        builder.Property(x => x.CreatedAt).AsCreatedAt();
        builder.Property(x => x.UpdatedAt).AsUpdatedAt();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("uq_modules_code");
    }
}
