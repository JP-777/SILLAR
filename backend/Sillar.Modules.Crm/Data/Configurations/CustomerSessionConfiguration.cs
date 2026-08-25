using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Crm.Domain;

namespace Sillar.Modules.Crm.Data.Configurations;

internal sealed class CustomerSessionConfiguration : IEntityTypeConfiguration<CustomerSession>
{
    public void Configure(EntityTypeBuilder<CustomerSession> builder)
    {
        builder.ToTable("customer_sessions", table =>
        {
            table.HasCheckConstraint("ck_customer_sessions_token_hash_no_vacio", "btrim(token_hash) <> ''");
            table.HasCheckConstraint("ck_customer_sessions_csrf_token_hash_no_vacio", "btrim(csrf_token_hash) <> ''");
            table.HasCheckConstraint("ck_customer_sessions_last_seen_after_issued", "last_seen_at >= issued_at");
            table.HasCheckConstraint("ck_customer_sessions_expires_after_issued", "expires_at > issued_at");
            table.HasCheckConstraint("ck_customer_sessions_revoked_after_issued", "revoked_at IS NULL OR revoked_at >= issued_at");
        });

        builder.HasKey(x => x.CustomerSessionId).HasName("pk_customer_sessions");

        builder.Property(x => x.CustomerSessionId)
            .HasColumnName("customer_session_id")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.CustomerAccountId).HasColumnName("customer_account_id").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.Property(x => x.CsrfTokenHash).HasColumnName("csrf_token_hash").IsRequired();
        builder.Property(x => x.IssuedAt).HasColumnName("issued_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamptz");
        builder.Property(x => x.IpAddress).HasColumnName("ip_address");
        builder.Property(x => x.UserAgent).HasColumnName("user_agent");

        builder.HasOne<CustomerAccount>()
            .WithMany()
            .HasForeignKey(x => x.CustomerAccountId)
            .HasConstraintName("fk_customer_sessions_customer_account_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("uq_customer_sessions_token_hash");
    }
}
