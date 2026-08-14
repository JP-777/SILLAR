using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;

namespace Sillar.Core.Data.Configurations;

internal sealed class AdminSessionConfiguration : IEntityTypeConfiguration<AdminSession>
{
    public void Configure(EntityTypeBuilder<AdminSession> builder)
    {
        builder.ToTable("admin_sessions", table =>
        {
            table.HasCheckConstraint("ck_admin_sessions_token_hash_not_empty", Check.NotEmpty("token_hash"));
            table.HasCheckConstraint("ck_admin_sessions_csrf_token_hash_not_empty", Check.NotEmpty("csrf_token_hash"));
        });

        builder.HasKey(x => x.AdminSessionId).HasName("pk_admin_sessions");

        // Lo genera la aplicación al abrir la sesión, no la base de datos.
        builder.Property(x => x.AdminSessionId)
            .HasColumnName("admin_session_id")
            .ValueGeneratedNever();

        builder.Property(x => x.AdminUserId).HasColumnName("admin_user_id");

        builder.Property(x => x.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.CsrfTokenHash)
            .HasColumnName("csrf_token_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.IssuedAt)
            .HasColumnName("issued_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.LastSeenAt)
            .HasColumnName("last_seen_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.RevokedAt)
            .HasColumnName("revoked_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(x => x.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(300);

        // Al eliminar un usuario se van sus sesiones: dejar viva la sesión de
        // alguien que ya no existe sería una puerta abierta.
        builder.HasOne(x => x.AdminUser)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.AdminUserId)
            .HasConstraintName("fk_admin_sessions_admin_user_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TokenHash)
            .IsUnique()
            .HasDatabaseName("uq_admin_sessions_token_hash");

        builder.HasIndex(x => x.AdminUserId)
            .HasDatabaseName("idx_admin_sessions_user");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("idx_admin_sessions_expires_at");
    }
}
