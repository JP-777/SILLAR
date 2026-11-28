using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;

namespace Sillar.Core.Data.Configurations;

internal sealed class ModuleOperationPermissionConfiguration : IEntityTypeConfiguration<ModuleOperationPermission>
{
    public void Configure(EntityTypeBuilder<ModuleOperationPermission> b)
    {
        b.ToTable("module_operation_permissions", t =>
        {
            t.HasCheckConstraint("ck_module_permission_module", "module_code ~ '^[a-z][a-z0-9_]{0,39}$'");
            t.HasCheckConstraint("ck_module_permission_operation", "operation_code ~ '^[a-z][a-z0-9_]{0,59}$'");
        });
        b.HasKey(x => new { x.AdminUserId, x.ModuleCode, x.OperationCode }).HasName("pk_module_operation_permissions");
        b.Property(x => x.AdminUserId).HasColumnName("admin_user_id");
        b.Property(x => x.ModuleCode).HasColumnName("module_code").HasMaxLength(40).UseCollation("C");
        b.Property(x => x.OperationCode).HasColumnName("operation_code").HasMaxLength(60).UseCollation("C");
        b.Property(x => x.Allowed).HasColumnName("allowed");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
        // Ambas tablas son locales. La historia de quién cambió el permiso vive
        // por separado en audit_log, con fotografía sin FK.
        b.HasOne<AdminUser>().WithMany().HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_module_permission_admin_user");
    }
}
