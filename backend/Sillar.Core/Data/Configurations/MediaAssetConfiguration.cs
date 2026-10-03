using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sillar.Core.Domain;
using Sillar.Shared.Data.Replication;

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

            // La fotografía del autor va entera o no va: tres nulos (subida sin
            // sesión) o tres valores. Media fotografía no identifica a nadie.
            table.HasCheckConstraint(
                "ck_media_assets_autor_completo",
                "(created_by_admin_user_local_id IS NULL AND created_by_admin_user_name IS NULL "
                + "AND created_by_admin_user_home_node IS NULL) OR "
                + "(created_by_admin_user_local_id IS NOT NULL AND created_by_admin_user_name IS NOT NULL "
                + "AND created_by_admin_user_home_node IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_media_assets_autor_local_id_positivo",
                "created_by_admin_user_local_id IS NULL OR created_by_admin_user_local_id > 0");
            table.HasCheckConstraint(
                "ck_media_assets_autor_nombre_no_vacio",
                "created_by_admin_user_name IS NULL OR " + Check.NotEmpty("created_by_admin_user_name"));
            table.HasCheckConstraint(
                "ck_media_assets_autor_home_node_no_vacio",
                "created_by_admin_user_home_node IS NULL OR " + Check.NotEmpty("created_by_admin_user_home_node"));
        });

        builder.HasKey(x => x.MediaAssetId).HasName("pk_media_assets");

        // Generado por la aplicación con Guid.CreateVersion7(), no por la base
        // de datos: es el mismo valor que el nombre del archivo en disco
        // (ADR-018), y ese nombre se decide antes de escribir la fila.
        builder.Property(x => x.MediaAssetId)
            .HasColumnName("media_asset_id")
            .ValueGeneratedNever();

        // Las cuatro columnas de la ADR-016 regla 4, que ahora lleva también
        // media_assets (ADR-018): esta tabla se replica. Las cuatro juntas y en
        // un solo sitio — antes estaban partidas, dos aquí y dos abajo con
        // AsCreatedAt/AsUpdatedAt, que es como se pierde de vista que van juntas.
        builder.MapReplication();

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

        // Sin clave foránea hacia core.admin_users, y es deliberado: esta tabla
        // se replica y aquella no (ADR-018). Ver la fotografía en MediaAsset.
        builder.Property(x => x.CreatedByAdminUserLocalId)
            .HasColumnName("created_by_admin_user_local_id");

        builder.Property(x => x.CreatedByAdminUserName)
            .HasColumnName("created_by_admin_user_name")
            .HasMaxLength(150);

        // text, como origin_node (ReplicationMapping): un nodo no tiene longitud arbitraria.
        builder.Property(x => x.CreatedByAdminUserHomeNode)
            .HasColumnName("created_by_admin_user_home_node");

        builder.HasIndex(x => x.StoredName)
            .IsUnique()
            .HasDatabaseName("uq_media_assets_stored_name");

        builder.HasIndex(x => x.OwnerModuleCode)
            .HasDatabaseName("idx_media_assets_owner_module_code");

        // Era el índice de la antigua clave foránea; se conserva para buscar
        // lo subido por una cuenta, que se pregunta con su nodo.
        builder.HasIndex(x => new { x.CreatedByAdminUserHomeNode, x.CreatedByAdminUserLocalId })
            .HasDatabaseName("idx_media_assets_created_by_admin_user");

        builder.HasIndex(x => x.Checksum)
            .HasDatabaseName("idx_media_assets_checksum");
    }
}
