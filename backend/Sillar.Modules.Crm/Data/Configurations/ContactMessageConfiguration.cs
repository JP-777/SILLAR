using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Modules.Crm.Domain;

namespace Sillar.Modules.Crm.Data.Configurations;

internal sealed class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> builder)
    {
        builder.ToTable("contact_messages", table =>
        {
            table.HasCheckConstraint("ck_contact_messages_full_name_no_vacio", "btrim(full_name) <> ''");
            table.HasCheckConstraint("ck_contact_messages_message_no_vacio", "btrim(message) <> ''");
            table.HasCheckConstraint("ck_contact_messages_email_no_vacio", "email IS NULL OR btrim(email) <> ''");
            table.HasCheckConstraint("ck_contact_messages_phone_no_vacio", "phone IS NULL OR btrim(phone) <> ''");
            table.HasCheckConstraint("ck_contact_messages_contact_channel",
                "(email IS NOT NULL OR phone IS NOT NULL)");
            table.HasCheckConstraint("ck_contact_messages_subject_no_vacio", "subject IS NULL OR btrim(subject) <> ''");
        });

        builder.HasKey(x => x.ContactMessageId).HasName("pk_contact_messages");

        builder.Property(x => x.ContactMessageId)
            .HasColumnName("contact_message_id")
            .HasColumnType("integer")
            .UseIdentityAlwaysColumn();

        builder.Property(x => x.CustomerId).HasColumnName("customer_id").HasColumnType("uuid");

        builder.Property(x => x.FullName)
            .HasColumnName("full_name")
            .IsRequired();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasColumnType("character varying(150)")
            .HasMaxLength(150);

        builder.Property(x => x.Phone).HasColumnName("phone");
        builder.Property(x => x.Subject).HasColumnName("subject");
        builder.Property(x => x.Message).HasColumnName("message").IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .HasConstraintName("fk_contact_messages_customer_id")
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.CustomerId).HasDatabaseName("idx_contact_messages_customer");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("idx_contact_messages_created_at");
        builder.HasIndex(x => x.IsActive).HasDatabaseName("idx_contact_messages_active").HasFilter("is_active");
    }
}
