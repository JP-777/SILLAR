using Sillar.Shared.Replication;

namespace Sillar.Core.Domain;

/// <summary>
/// Metadatos de un archivo subido. El binario vive en el volumen de disco
/// (ADR-011); aquí solo está su ficha.
/// </summary>
/// <remarks>
/// Se replica (ADR-018): un catálogo que viaja a otro nodo tiene que llevarse
/// también las fichas de sus imágenes. La clave es <c>uuid</c> v7 generada por
/// la aplicación, y es <b>el mismo valor</b> que <see cref="StoredName"/> antes
/// de la extensión: un archivo encontrado en el disco se rastrea hasta su fila
/// sin buscar nada, y al revés.
/// </remarks>
public class MediaAsset : IReplicatedEntity
{
    /// <summary>Identificador. El mismo valor que el nombre del archivo en disco.</summary>
    public Guid MediaAssetId { get; set; }

    /// <inheritdoc />
    public string OriginNode { get; set; } = string.Empty;

    /// <inheritdoc />
    public long RowVersion { get; set; } = 1;

    /// <summary>
    /// Nombre en disco, <b>generado</b> por el sistema. Nunca el que envió quien
    /// subió el archivo: es la defensa contra recorrido de rutas y contra
    /// nombres hostiles.
    /// </summary>
    public required string StoredName { get; set; }

    /// <summary>Nombre original, solo para mostrarlo en el panel.</summary>
    public string? OriginalName { get; set; }

    /// <summary>Ruta dentro del volumen de medios.</summary>
    public required string RelativePath { get; set; }

    /// <summary>Tipo real verificado por contenido, no deducido de la extensión.</summary>
    public required string MimeType { get; set; }

    /// <summary>Tamaño en bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Ancho en píxeles. Solo imágenes.</summary>
    public int? Width { get; set; }

    /// <summary>Alto en píxeles. Solo imágenes.</summary>
    public int? Height { get; set; }

    /// <summary>Texto alternativo, para accesibilidad.</summary>
    public string? AltText { get; set; }

    /// <summary>
    /// Módulo que subió el archivo.
    /// </summary>
    /// <remarks>
    /// Texto y sin clave foránea a propósito: el módulo puede desinstalarse y el
    /// archivo tiene que sobrevivir, marcado como huérfano.
    /// </remarks>
    public string? OwnerModuleCode { get; set; }

    /// <summary>SHA-256 del contenido, para detectar duplicados.</summary>
    public string? Checksum { get; set; }

    /// <summary>El módulo que lo subió ya no está instalado.</summary>
    public bool IsOrphan { get; set; }

    /// <summary>Eliminación lógica.</summary>
    public bool IsActive { get; set; } = true;

    // --- Fotografía del autor -------------------------------------------
    //
    // core.media_assets se replica y core.admin_users no (ADR-018), así que no
    // hay clave foránea: la fila viajaría y la referencia apuntaría a otra
    // persona en el otro nodo. Se guarda una fotografía de tres datos, todos o
    // ninguno, y no cambia después de escrita (lo impone un trigger).
    // Sin autor —subida sin sesión— los tres son nulos, y es válido.

    /// <summary>Identificador local de quien lo subió, en su nodo de pertenencia.</summary>
    public int? CreatedByAdminUserLocalId { get; set; }

    /// <summary>Nombre visible de quien lo subió, en el momento de subirlo.</summary>
    public string? CreatedByAdminUserName { get; set; }

    /// <summary>
    /// Nodo de pertenencia de la cuenta que lo subió. Se copia de
    /// <c>admin_users.home_node</c>, no de <see cref="OriginNode"/>.
    /// </summary>
    public string? CreatedByAdminUserHomeNode { get; set; }

    /// <summary>Fecha de alta.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Fecha de la última modificación. La escribe un trigger.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
