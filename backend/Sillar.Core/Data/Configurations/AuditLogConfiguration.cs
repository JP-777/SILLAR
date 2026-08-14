using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;
using Sillar.Core.Domain.Values;

namespace Sillar.Core.Data.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log", table =>
        {
            table.HasCheckConstraint("ck_audit_log_action", Check.OneOf("action", AuditAction.All));
        });

        builder.HasKey(x => x.AuditLogId).HasName("pk_audit_log");

        builder.Property(x => x.AuditLogId)
            .HasColumnName("audit_log_id")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AdminUserId).HasColumnName("admin_user_id");

        // Snapshot del correo: el registro conserva quién actuó aunque después
        // se elimine la cuenta.
        builder.Property(x => x.AdminUserEmail)
            .HasColumnName("admin_user_email")
            .HasMaxLength(150);

        builder.Property(x => x.ModuleCode)
            .HasColumnName("module_code")
            .HasMaxLength(40);

        builder.Property(x => x.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(60);

        builder.Property(x => x.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(60);

        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Summary)
            .HasColumnName("summary")
            .HasMaxLength(300);

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.HasOne(x => x.AdminUser)
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .HasConstraintName("fk_audit_log_admin_user_id")
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.OccurredAt)
            .HasDatabaseName("idx_audit_log_occurred_at");

        builder.HasIndex(x => x.AdminUserId)
            .HasDatabaseName("idx_audit_log_admin_user_id");

        builder.HasIndex(x => x.ModuleCode)
            .HasDatabaseName("idx_audit_log_module_code");
    }
}
