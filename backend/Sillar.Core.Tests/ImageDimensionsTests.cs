using Sillar.Core.Media;

namespace Sillar.Core.Tests;

/// <summary>Lectura de dimensiones desde la cabecera.</summary>
public class ImageDimensionsTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(800, 600)]
    [InlineData(4096, 2160)]
    public void Las_dimensiones_de_un_png_se_leen_del_IHDR(int ancho, int alto)
    {
        Assert.Equal(
            new Dimensions(ancho, alto),
            ImageDimensions.Read(ImageFormat.Png, Imagenes.PngCabecera(ancho, alto)));
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1920, 1080)]
    public void Las_dimensiones_de_un_jpeg_se_leen_del_inicio_de_fotograma(int ancho, int alto)
    {
        Assert.Equal(
            new Dimensions(ancho, alto),
            ImageDimensions.Read(ImageFormat.Jpeg, Imagenes.JpegCabecera(ancho, alto)));
    }

    [Fact]
    public void En_jpeg_se_saltan_los_marcadores_previos()
    {
        // El tamaño va después de cabeceras de longitud variable: EXIF,
        // miniaturas incrustadas, comentarios. Recorrerlos es el trabajo.
        var conRelleno = ImageDimensions.Read(ImageFormat.Jpeg, Imagenes.JpegCabecera(640, 480));
        var sinRelleno = ImageDimensions.Read(ImageFormat.Jpeg, Imagenes.JpegCabecera(640, 480, conRelleno: false));

        Assert.Equal(new Dimensions(640, 480), conRelleno);
        Assert.Equal(conRelleno, sinRelleno);
    }

    [Fact]
    public void En_jpeg_el_alto_va_antes_que_el_ancho()
    {
        // Es al revés que en los demás formatos y es el error fácil de cometer.
        var d = ImageDimensions.Read(ImageFormat.Jpeg, Imagenes.JpegCabecera(ancho: 100, alto: 200));

        Assert.Equal(100, d!.Value.Width);
        Assert.Equal(200, d.Value.Height);
    }

    [Theory]
    [InlineData("VP8 ", 640, 480)]
    [InlineData("VP8L", 640, 480)]
    [InlineData("VP8X", 640, 480)]
    [InlineData("VP8L", 1, 1)]
    [InlineData("VP8X", 16383, 16383)]
    public void Las_dimensiones_de_un_webp_se_leen_en_sus_tres_variantes(string variante, int ancho, int alto)
    {
        // VP8L y VP8X guardan el valor menos uno; VP8 lo guarda tal cual. Tres
        // formas distintas bajo el mismo tipo.
        Assert.Equal(
            new Dimensions(ancho, alto),
            ImageDimensions.Read(ImageFormat.Webp, Imagenes.WebpCabecera(variante, ancho, alto)));
    }

    [Fact]
    public void Una_cabecera_truncada_devuelve_nulo_sin_reventar()
    {
        // Las dimensiones son metadatos para el panel: no poder leerlas no es
        // motivo para rechazar un archivo que ya pasó la detección de tipo.
        Assert.Null(ImageDimensions.Read(ImageFormat.Png, Imagenes.PngCabecera(10, 10).AsSpan(0, 18).ToArray()));
        Assert.Null(ImageDimensions.Read(ImageFormat.Jpeg, [0xFF, 0xD8]));
        Assert.Null(ImageDimensions.Read(ImageFormat.Webp, "RIFF"u8.ToArray()));
    }

    [Fact]
    public void Un_webp_de_variante_desconocida_devuelve_nulo()
    {
        Assert.Null(ImageDimensions.Read(ImageFormat.Webp, Imagenes.WebpCabecera("VP9 ", 10, 10)));
    }

    [Fact]
    public void Un_jpeg_sin_inicio_de_fotograma_devuelve_nulo_y_no_se_queda_dando_vueltas()
    {
        // Un archivo malformado no puede colgar el hilo que lo recorre.
        var soloRelleno = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00 };

        Assert.Null(ImageDimensions.Read(ImageFormat.Jpeg, soloRelleno));
    }
}
