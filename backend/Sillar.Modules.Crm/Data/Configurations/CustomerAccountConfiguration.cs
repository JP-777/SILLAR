using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Crm.Domain;

namespace Sillar.Modules.Crm.Data.Configurations;

internal sealed class CustomerAccountConfiguration : IEntityTypeConfiguration<CustomerAccount>
{
    public void Configure(EntityTypeBuilder<CustomerAccount> builder)
    {
        builder.ToTable("customer_accounts", table =>
        {
            table.HasCheckConstraint("ck_customer_accounts_password_hash_no_vacio", "btrim(password_hash) <> ''");
        });

        builder.HasKey(x => x.CustomerAccountId).HasName("pk_customer_accounts");

        builder.Property(x => x.CustomerAccountId)
            .HasColumnName("customer_account_id")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.CustomerId)
            .HasColumnName("customer_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(x => x.EmailVerifiedAt).HasColumnName("email_verified_at").HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .HasConstraintName("fk_customer_accounts_customer_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CustomerId).IsUnique().HasDatabaseName("uq_customer_accounts_customer_id");
    }
}
