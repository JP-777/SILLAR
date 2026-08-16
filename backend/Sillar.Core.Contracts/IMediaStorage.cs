namespace Sillar.Core.Contracts;

/// <summary>Un archivo guardado, visto por los demás módulos.</summary>
/// <param name="MediaAssetId">Identificador.</param>
/// <param name="Url">
/// Ruta pública bajo <c>/media/</c>. <b>Nunca una ruta del sistema de
/// archivos</b>: es lo que permite que una implementación futura sobre
/// almacenamiento externo devuelva otra cosa sin que ningún módulo se entere.
/// </param>
/// <param name="OriginalName">Nombre que traía el archivo, solo para mostrarlo.</param>
/// <param name="MimeType">Tipo real, verificado por el contenido.</param>
/// <param name="SizeBytes">Tamaño en bytes.</param>
/// <param name="Width">Ancho en píxeles, si se pudo leer.</param>
/// <param name="Height">Alto en píxeles, si se pudo leer.</param>
public sealed record MediaAsset(
    Guid MediaAssetId,
    string Url,
    string? OriginalName,
    string MimeType,
    long SizeBytes,
    int? Width,
    int? Height);

/// <summary>
/// Guarda y recupera archivos. Lo implementa CORE y lo usan los demás módulos.
/// </summary>
/// <remarks>
/// CORE es el único que toca el disco (ADR-011). Ningún módulo implementa su
/// propia subida: si cada uno inventara su validación, cada uno tendría su
/// propio agujero.
///
/// El acceso al disco vive detrás de esta interfaz para que añadir una
/// implementación sobre almacenamiento externo sea escribir una clase y cambiar
/// un registro, sin tocar ningún módulo.
/// </remarks>
public interface IMediaStorage
{
    /// <summary>
    /// Guarda un archivo y devuelve su ficha.
    /// </summary>
    /// <param name="content">Contenido. Se lee una sola vez y hacia delante.</param>
    /// <param name="originalName">Nombre que envió quien sube. Solo se conserva para mostrarlo.</param>
    /// <param name="ownerModuleCode">Módulo que lo sube.</param>
    /// <param name="ct">Cancelación.</param>
    /// <exception cref="MediaRejectedException">
    /// El contenido no es de un tipo admitido o pasa del tamaño permitido.
    /// </exception>
    Task<MediaAsset> SaveAsync(Stream content, string originalName, string ownerModuleCode, CancellationToken ct);

    /// <summary>
    /// Da de baja un archivo. La baja es <b>lógica</b>: el binario se conserva.
    /// </summary>
    /// <returns><c>true</c> si existía y estaba activo.</returns>
    Task<bool> DeleteAsync(Guid mediaAssetId, CancellationToken ct);

    /// <summary>Devuelve la ruta pública de un archivo, o <c>null</c> si no existe.</summary>
    string? GetPublicUrl(Guid mediaAssetId);
}

/// <summary>El archivo no se puede aceptar.</summary>
/// <param name="reason">Motivo, en español, para mostrar a quien lo subió.</param>
/// <param name="tooLarge">
/// Verdadero si el problema es el tamaño, que se responde con 413; falso si es
/// el tipo, que se responde con 415.
/// </param>
public sealed class MediaRejectedException(string reason, bool tooLarge = false) : Exception(reason)
{
    /// <summary>Motivo del rechazo.</summary>
    public string Reason { get; } = reason;

    /// <summary>El rechazo es por tamaño, no por tipo.</summary>
    public bool TooLarge { get; } = tooLarge;
}
