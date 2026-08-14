using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;
using Sillar.Core.Domain.Values;

namespace Sillar.Core.Data.Configurations;

internal sealed class SiteSettingConfiguration : IEntityTypeConfiguration<SiteSetting>
{
    public void Configure(EntityTypeBuilder<SiteSetting> builder)
    {
        builder.ToTable("site_settings", table =>
        {
            table.HasCheckConstraint("ck_site_settings_key_not_empty", Check.NotEmpty("setting_key"));
            table.HasCheckConstraint(
                "ck_site_settings_value_type",
                Check.OneOf("value_type", SettingValueType.All));
        });

        builder.HasKey(x => x.SiteSettingId).HasName("pk_site_settings");

        builder.Property(x => x.SiteSettingId)
            .HasColumnName("site_setting_id")
            .UseIdentityAlwaysColumn();

        // La colación core.es_ci se aplica con SQL explícito en la migración,
        // no aquí. Ver el comentario en AdminUserConfiguration.
        builder.Property(x => x.SettingKey)
            .HasColumnName("setting_key")
            .HasMaxLength(100)
            .IsRequired();

        // Texto sin límite: aquí caben desde un número de teléfono hasta un
        // horario largo o un documento JSON.
        builder.Property(x => x.SettingValue)
            .HasColumnName("setting_value")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.ValueType)
            .HasColumnName("value_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(250);

        // Falso por defecto también en la base de datos: si alguien inserta una
        // configuración sin decidir nada, queda privada.
        builder.Property(x => x.IsPublic)
            .HasColumnName("is_public")
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.Property(x => x.CreatedAt).AsCreatedAt();
        builder.Property(x => x.UpdatedAt).AsUpdatedAt();

        builder.HasIndex(x => x.SettingKey)
            .IsUnique()
            .HasDatabaseName("uq_site_settings_setting_key");
    }
}
