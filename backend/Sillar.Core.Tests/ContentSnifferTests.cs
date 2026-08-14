using Sillar.Core.Media;

namespace Sillar.Core.Tests;

/// <summary>
/// Detección del tipo real por los bytes iniciales.
/// </summary>
/// <remarks>
/// Es la regla 12 del SPEC y la defensa de la que cuelga todo lo demás: la
/// extensión y el <c>Content-Type</c> los elige quien sube el archivo.
/// </remarks>
public class ContentSnifferTests
{
    [Fact]
    public void Un_png_se_reconoce_por_su_firma()
    {
        Assert.Equal(ImageFormat.Png, ContentSniffer.Detect(Imagenes.PngCabecera(1, 1)));
    }

    [Fact]
    public void Un_jpeg_se_reconoce_por_su_firma()
    {
        Assert.Equal(ImageFormat.Jpeg, ContentSniffer.Detect(Imagenes.JpegCabecera(1, 1)));
    }

    [Theory]
    [InlineData("VP8 ")]
    [InlineData("VP8L")]
    [InlineData("VP8X")]
    public void Un_webp_se_reconoce_en_sus_tres_variantes(string variante)
    {
        Assert.Equal(ImageFormat.Webp, ContentSniffer.Detect(Imagenes.WebpCabecera(variante, 1, 1)));
    }

    [Fact]
    public void Un_svg_valido_se_rechaza()
    {
        // No es un descuido: un SVG servido desde la ruta estática se ejecuta en
        // el mismo origen que el panel, y desde ahí puede pedir el token CSRF y
        // escribir con los permisos de quien lo mire. Si esta prueba empieza a
        // fallar, alguien ha añadido SVG a la lista.
        var svg = System.Text.Encoding.UTF8.GetBytes(
            "<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>");

        Assert.Null(ContentSniffer.Detect(svg));
    }

    [Fact]
    public void Un_svg_sin_declaracion_xml_tambien_se_rechaza()
    {
        Assert.Null(ContentSniffer.Detect("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8));
    }

    [Fact]
    public void Un_archivo_con_extension_png_pero_otro_contenido_se_rechaza()
    {
        // El caso que da sentido a todo esto: el nombre dice .png y dentro hay
        // un ejecutable de Windows.
        Assert.Null(ContentSniffer.Detect("MZ\0\0\0\0\0\0\0"u8));
    }

    [Theory]
    [InlineData("GIF89a")]
    [InlineData("%PDF-1.7")]
    [InlineData("PK")]
    [InlineData("")]
    public void Otros_formatos_conocidos_se_rechazan(string firma)
    {
        Assert.Null(ContentSniffer.Detect(System.Text.Encoding.Latin1.GetBytes(firma)));
    }

    [Fact]
    public void Un_riff_que_no_es_webp_se_rechaza()
    {
        // Un WAV y un AVI también empiezan por «RIFF»: comprobar solo eso los
        // dejaría pasar.
        var wav = new byte[12];
        "RIFF"u8.CopyTo(wav);
        "WAVE"u8.CopyTo(wav.AsSpan(8));

        Assert.Null(ContentSniffer.Detect(wav));
    }

    [Fact]
    public void Un_archivo_vacio_o_truncado_se_rechaza()
    {
        Assert.Null(ContentSniffer.Detect([]));
        Assert.Null(ContentSniffer.Detect([0x89, 0x50]));
        Assert.Null(ContentSniffer.Detect("RIFF"u8));
    }
}
