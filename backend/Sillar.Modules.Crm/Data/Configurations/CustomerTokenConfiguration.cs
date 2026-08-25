using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Crm.Domain;

namespace Sillar.Modules.Crm.Data.Configurations;

internal sealed class CustomerTokenConfiguration : IEntityTypeConfiguration<CustomerToken>
{
    public void Configure(EntityTypeBuilder<CustomerToken> builder)
    {
        builder.ToTable("customer_tokens", table =>
        {
            table.HasCheckConstraint("ck_customer_tokens_purpose",
                """
                purpose IN (
                    'invitation',
                    'email_verification',
                    'password_reset'
                )
                """);
            table.HasCheckConstraint("ck_customer_tokens_expires_after_created", "expires_at > created_at");
            table.HasCheckConstraint("ck_customer_tokens_used_after_created", "used_at IS NULL OR used_at >= created_at");
            table.HasCheckConstraint("ck_customer_tokens_token_hash_no_vacio", "btrim(token_hash) <> ''");
        });

        builder.HasKey(x => x.CustomerTokenId).HasName("pk_customer_tokens");

        builder.Property(x => x.CustomerTokenId)
            .HasColumnName("customer_token_id")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.CustomerId).HasColumnName("customer_id").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.Purpose).HasColumnName("purpose").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UsedAt).HasColumnName("used_at").HasColumnType("timestamptz");

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .HasConstraintName("fk_customer_tokens_customer_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("uq_customer_tokens_token_hash");
    }
}
