namespace Sillar.Core.Media;

/// <summary>Configuración del almacenamiento de medios.</summary>
/// <remarks>
/// Están en configuración para poder ajustarlas sin recompilar, no porque sean
/// un punto de extensión: ampliar la lista de tipos admitidos no es una
/// preferencia de despliegue, es una decisión de seguridad (ver
/// <see cref="ContentSniffer"/>).
/// </remarks>
public sealed class MediaOptions
{
    /// <summary>Sección de configuración.</summary>
    public const string SectionName = "Media";

    /// <summary>Tamaño máximo por archivo. Cinco megabytes.</summary>
    public long MaxSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Carpeta donde viven los archivos.
    /// </summary>
    /// <remarks>
    /// <b>Hay que respaldarla junto con la base de datos.</b> Es la consecuencia
    /// negativa que anota el ADR-011 y el error clásico: se vuelca la base, se
    /// olvida esta carpeta, y al restaurar aparece un catálogo entero sin
    /// imágenes.
    /// </remarks>
    public string RootPath { get; set; } = "media";

    /// <summary>Prefijo público bajo el que se sirven.</summary>
    public string RequestPath { get; set; } = "/media";

    /// <summary>Subcarpeta de los archivos a medio escribir.</summary>
    /// <remarks>
    /// Dentro de la misma raíz para que mover el temporal a su destino sea un
    /// renombrado dentro del mismo volumen: entre volúmenes distintos, «mover»
    /// se convierte en copiar y borrar, y deja de ser atómico.
    /// </remarks>
    public const string TempFolder = ".tmp";
}
