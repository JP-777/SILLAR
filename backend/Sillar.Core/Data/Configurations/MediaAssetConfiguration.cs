using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;

namespace Sillar.Core.Data.Configurations;

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets", table =>
        {
            table.HasCheckConstraint("ck_media_assets_size_bytes", "size_bytes > 0");
            table.HasCheckConstraint("ck_media_assets_stored_name_not_empty", Check.NotEmpty("stored_name"));
            table.HasCheckConstraint("ck_media_assets_relative_path_not_empty", Check.NotEmpty("relative_path"));
            table.HasCheckConstraint("ck_media_assets_mime_type_not_empty", Check.NotEmpty("mime_type"));
        });

        builder.HasKey(x => x.MediaAssetId).HasName("pk_media_assets");

        // Generado por la aplicación con Guid.CreateVersion7(), no por la base
        // de datos: es el mismo valor que el nombre del archivo en disco
        // (ADR-018), y ese nombre se decide antes de escribir la fila.
        builder.Property(x => x.MediaAssetId)
            .HasColumnName("media_asset_id")
            .ValueGeneratedNever();

        // Las cuatro columnas de la ADR-016 regla 4, que ahora lleva también
        // media_assets (ADR-018): esta tabla se replica.
        builder.Property(x => x.OriginNode)
            .HasColumnName("origin_node")
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasDefaultValue(1L)
            .ValueGeneratedNever();

        builder.Property(x => x.StoredName)
            .HasColumnName("stored_name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.OriginalName)
            .HasColumnName("original_name")
            .HasMaxLength(255);

        builder.Property(x => x.RelativePath)
            .HasColumnName("relative_path")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.MimeType)
            .HasColumnName("mime_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes");
        builder.Property(x => x.Width).HasColumnName("width");
        builder.Property(x => x.Height).HasColumnName("height");

        builder.Property(x => x.AltText)
            .HasColumnName("alt_text")
            .HasMaxLength(180);

        // Texto y sin clave foránea: el módulo puede desinstalarse y el archivo
        // tiene que sobrevivir marcado como huérfano.
        builder.Property(x => x.OwnerModuleCode)
            .HasColumnName("owner_module_code")
            .HasMaxLength(40);

        builder.Property(x => x.Checksum)
            .HasColumnName("checksum")
            .HasMaxLength(64);

        builder.Property(x => x.IsOrphan)
            .HasColumnName("is_orphan")
            .HasDefaultValue(false)
            .ValueGeneratedNever();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .ValueGeneratedNever();

        builder.Property(x => x.CreatedBy).HasColumnName("created_by");

        builder.Property(x => x.CreatedAt).AsCreatedAt();
        builder.Property(x => x.UpdatedAt).AsUpdatedAt();

        // Si se elimina el usuario, el archivo se queda: pierde el autor, no el
        // contenido.
        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .HasConstraintName("fk_media_assets_created_by")
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.StoredName)
            .IsUnique()
            .HasDatabaseName("uq_media_assets_stored_name");

        builder.HasIndex(x => x.OwnerModuleCode)
            .HasDatabaseName("idx_media_assets_owner_module_code");

        // EF crea solo el índice de la clave foránea; se nombra a mano para que
        // siga la convención idx_ en lugar de la suya.
        builder.HasIndex(x => x.CreatedBy)
            .HasDatabaseName("idx_media_assets_created_by");

        builder.HasIndex(x => x.Checksum)
            .HasDatabaseName("idx_media_assets_checksum");
    }
}
