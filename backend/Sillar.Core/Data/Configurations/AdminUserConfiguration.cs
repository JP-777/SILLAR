using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;
using Sillar.Core.Domain.Values;

namespace Sillar.Core.Data.Configurations;

internal sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.ToTable("admin_users", table =>
        {
            table.HasCheckConstraint("ck_admin_users_role", Check.OneOf("role", AdminRole.All));
            table.HasCheckConstraint("ck_admin_users_failed_login_count", "failed_login_count >= 0");
            table.HasCheckConstraint("ck_admin_users_full_name_not_empty", Check.NotEmpty("full_name"));
            table.HasCheckConstraint("ck_admin_users_email_not_empty", Check.NotEmpty("email"));
            table.HasCheckConstraint("ck_admin_users_password_hash_not_empty", Check.NotEmpty("password_hash"));
        });

        builder.HasKey(x => x.AdminUserId).HasName("pk_admin_users");

        builder.Property(x => x.AdminUserId)
            .HasColumnName("admin_user_id")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(150)
            .IsRequired();

        // El correo identifica el acceso: la comparación ignora mayúsculas
        // gracias a la colación core.es_ci, y el índice único impide registrar
        // el mismo correo dos veces con distinta caja.
        //
        // La colación NO se declara aquí: el proveedor Npgsql entrecomilla el
        // nombre calificado entero —COLLATE "core.es_ci"— y PostgreSQL entonces
        // busca una colación llamada literalmente así. Se aplica con SQL
        // explícito en la migración CoreInitial.
        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Phone)
            .HasColumnName("phone")
            .HasMaxLength(30);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.Property(x => x.LastLoginAt)
            .HasColumnName("last_login_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.FailedLoginCount)
            .HasColumnName("failed_login_count")
            .HasDefaultValue(0)
            .ValueGeneratedNever();

        builder.Property(x => x.LockedUntil)
            .HasColumnName("locked_until")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt).AsCreatedAt();
        builder.Property(x => x.UpdatedAt).AsUpdatedAt();

        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("uq_admin_users_email");

        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("idx_admin_users_is_active");
    }
}
