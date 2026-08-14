namespace Sillar.Core.Media;

/// <summary>Un formato de imagen que el producto acepta.</summary>
/// <param name="MimeType">Tipo real, el que se guarda y el que se sirve.</param>
/// <param name="Extension">Extensión canónica, con punto.</param>
public sealed record ImageFormat(string MimeType, string Extension)
{
    /// <summary>PNG.</summary>
    public static readonly ImageFormat Png = new("image/png", ".png");

    /// <summary>JPEG.</summary>
    public static readonly ImageFormat Jpeg = new("image/jpeg", ".jpg");

    /// <summary>WebP.</summary>
    public static readonly ImageFormat Webp = new("image/webp", ".webp");

    /// <summary>Los tres formatos admitidos.</summary>
    public static readonly IReadOnlyList<ImageFormat> All = [Png, Jpeg, Webp];
}

/// <summary>
/// Determina el tipo real de un archivo por sus bytes iniciales.
/// </summary>
/// <remarks>
/// <b>Nunca por la extensión ni por el <c>Content-Type</c> que envía el
/// cliente</b> (SPEC §8.12). Los dos los elige quien sube el archivo; los bytes
/// iniciales, no. Un <c>.png</c> cuyo contenido es otra cosa se rechaza, no se
/// guarda «por si acaso».
///
/// <para><b>SVG no está en la lista y no debe añadirse.</b> Un SVG es XML que
/// puede contener scripts, y servido desde la ruta estática se ejecuta en el
/// mismo origen que el panel. La cookie de sesión es <c>httpOnly</c>, pero el
/// script no necesita leerla: el navegador la adjunta sola, y desde el mismo
/// origen puede pedir <c>GET /api/admin/auth/csrf</c>, que devuelve un token
/// válido y estable. Con eso, un SVG subido por cualquiera con rol
/// <c>editor</c> ejecuta escrituras autenticadas con los permisos de quien lo
/// mire.</para>
///
/// <para>Sanear SVG exige una biblioteca y una lista de permitidos que hay que
/// mantener, y es una superficie conocida por sus evasiones. Si algún día hace
/// falta vectorial, la salida es servir los medios desde otro origen, no
/// sanear.</para>
/// </remarks>
public static class ContentSniffer
{
    /// <summary>Bytes que hace falta leer para decidir.</summary>
    /// <remarks>
    /// Doce bastan para los tres formatos: WebP es el que más necesita, con su
    /// «RIFF» y su «WEBP» separados por el tamaño.
    /// </remarks>
    public const int HeaderBytes = 12;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebpSignature = "WEBP"u8.ToArray();

    /// <summary>
    /// Devuelve el formato real, o <c>null</c> si no es ninguno de los
    /// admitidos.
    /// </summary>
    /// <param name="header">Primeros bytes del archivo.</param>
    public static ImageFormat? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(PngSignature))
        {
            return ImageFormat.Png;
        }

        if (header.StartsWith(JpegSignature))
        {
            return ImageFormat.Jpeg;
        }

        // WebP: «RIFF», cuatro bytes de tamaño que aquí no interesan, y «WEBP».
        // Comprobar solo «RIFF» aceptaría un WAV o un AVI.
        if (header.Length >= HeaderBytes
            && header.StartsWith(RiffSignature)
            && header[8..12].SequenceEqual(WebpSignature))
        {
            return ImageFormat.Webp;
        }

        return null;
    }
}
