namespace Sillar.Core.Domain;

/// <summary>
/// Metadatos de un archivo subido. El binario vive en el volumen de disco
/// (ADR-011); aquí solo está su ficha.
/// </summary>
public class MediaAsset
{
    /// <summary>Identificador.</summary>
    public int MediaAssetId { get; set; }

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

    /// <summary>Quién lo subió. Nulo si ese usuario fue eliminado.</summary>
    public int? CreatedBy { get; set; }

    /// <summary>Fecha de alta.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Fecha de la última modificación. La escribe un trigger.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Usuario que subió el archivo.</summary>
    public AdminUser? CreatedByUser { get; set; }
}
