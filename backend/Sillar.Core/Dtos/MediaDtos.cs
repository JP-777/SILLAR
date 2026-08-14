namespace Sillar.Core.Dtos;

/// <summary>Archivo recién subido.</summary>
/// <param name="MediaAssetId">Identificador.</param>
/// <param name="Url">Ruta pública bajo <c>/media/</c>.</param>
/// <param name="OriginalName">Nombre que traía, solo para mostrarlo.</param>
/// <param name="MimeType">Tipo real, verificado por el contenido.</param>
/// <param name="SizeBytes">Tamaño en bytes.</param>
/// <param name="Width">Ancho en píxeles, si se pudo leer.</param>
/// <param name="Height">Alto en píxeles, si se pudo leer.</param>
/// <param name="DuplicateOf">
/// Identificador de un archivo activo con el mismo contenido, si lo hay. La
/// subida se acepta igualmente: es un aviso para el panel, no un rechazo.
/// </param>
public sealed record MediaUploadResponse(
    int MediaAssetId,
    string Url,
    string? OriginalName,
    string MimeType,
    long SizeBytes,
    int? Width,
    int? Height,
    int? DuplicateOf);

/// <summary>Archivo tal como se lista en el panel.</summary>
/// <param name="MediaAssetId">Identificador.</param>
/// <param name="Url">Ruta pública.</param>
/// <param name="OriginalName">Nombre original.</param>
/// <param name="MimeType">Tipo real.</param>
/// <param name="SizeBytes">Tamaño en bytes.</param>
/// <param name="Width">Ancho en píxeles.</param>
/// <param name="Height">Alto en píxeles.</param>
/// <param name="AltText">Texto alternativo, para accesibilidad.</param>
/// <param name="OwnerModuleCode">Módulo que lo subió.</param>
/// <param name="IsOrphan">Su módulo ya no existe en esta versión del producto.</param>
/// <param name="IsActive">Si sigue en uso y se sirve.</param>
/// <param name="CreatedAt">Cuándo se subió.</param>
public sealed record MediaResponse(
    int MediaAssetId,
    string Url,
    string? OriginalName,
    string MimeType,
    long SizeBytes,
    int? Width,
    int? Height,
    string? AltText,
    string? OwnerModuleCode,
    bool IsOrphan,
    bool IsActive,
    DateTimeOffset CreatedAt);
