using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;

namespace Sillar.Core.Data.Configurations;

internal sealed class ModuleActivationConfiguration : IEntityTypeConfiguration<ModuleActivation>
{
    public void Configure(EntityTypeBuilder<ModuleActivation> builder)
    {
        builder.ToTable("module_activations");

        builder.HasKey(x => x.ModuleActivationId).HasName("pk_module_activations");

        builder.Property(x => x.ModuleActivationId)
            .HasColumnName("module_activation_id")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.ModuleId).HasColumnName("module_id");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder.Property(x => x.ActivatedAt)
            .HasColumnName("activated_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.DeactivatedAt)
            .HasColumnName("deactivated_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(250);

        builder.Property(x => x.CreatedAt).AsCreatedAt();
        builder.Property(x => x.UpdatedAt).AsUpdatedAt();

        builder.HasOne(x => x.Module)
            .WithOne(x => x.Activation)
            .HasForeignKey<ModuleActivation>(x => x.ModuleId)
            .HasConstraintName("fk_module_activations_module_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Un módulo, una activación.
        builder.HasIndex(x => x.ModuleId)
            .IsUnique()
            .HasDatabaseName("uq_module_activations_module_id");
    }
}
