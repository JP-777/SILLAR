using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;
using Sillar.Core.Domain.Values;

namespace Sillar.Core.Data.Configurations;

internal sealed class ModuleDependencyConfiguration : IEntityTypeConfiguration<ModuleDependency>
{
    public void Configure(EntityTypeBuilder<ModuleDependency> builder)
    {
        builder.ToTable("module_dependencies", table =>
        {
            table.HasCheckConstraint(
                "ck_module_dependencies_kind",
                Check.OneOf("kind", ModuleDependencyKind.All));
            table.HasCheckConstraint(
                "ck_module_dependencies_no_self",
                "module_id <> depends_on_module_id");
        });

        builder.HasKey(x => x.ModuleDependencyId).HasName("pk_module_dependencies");

        builder.Property(x => x.ModuleDependencyId)
            .HasColumnName("module_dependency_id")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.ModuleId).HasColumnName("module_id");
        builder.Property(x => x.DependsOnModuleId).HasColumnName("depends_on_module_id");

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasMaxLength(10)
            .IsRequired();

        // Las aristas pertenecen al módulo que depende: si desaparece del
        // catálogo, se van con él.
        builder.HasOne(x => x.Module)
            .WithMany(x => x.Dependencies)
            .HasForeignKey(x => x.ModuleId)
            .HasConstraintName("fk_module_dependencies_module_id")
            .OnDelete(DeleteBehavior.Cascade);

        // En cambio, un módulo del que alguien depende no se puede quitar del
        // catálogo mientras esa arista exista.
        builder.HasOne(x => x.DependsOnModule)
            .WithMany(x => x.Dependents)
            .HasForeignKey(x => x.DependsOnModuleId)
            .HasConstraintName("fk_module_dependencies_depends_on_module_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ModuleId, x.DependsOnModuleId })
            .IsUnique()
            .HasDatabaseName("uq_module_dependencies_module_depends_on");

        // Índice de la clave foránea, nombrado a mano para seguir la convención.
        builder.HasIndex(x => x.DependsOnModuleId)
            .HasDatabaseName("idx_module_dependencies_depends_on_module_id");
    }
}
