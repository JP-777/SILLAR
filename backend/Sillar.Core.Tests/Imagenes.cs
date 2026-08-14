using System.Buffers.Binary;

namespace Sillar.Core.Tests;

/// <summary>
/// Cabeceras de imagen construidas a mano, byte a byte.
/// </summary>
/// <remarks>
/// No son imágenes completas ni pretenden serlo: el detector y el lector de
/// dimensiones solo miran la cabecera. Construirlas aquí, en vez de guardar
/// archivos de ejemplo en el repositorio, deja a la vista qué byte significa
/// qué — que es justo lo que hay que poder revisar cuando se toca un analizador
/// binario.
/// </remarks>
internal static class Imagenes
{
    /// <summary>PNG: firma, longitud del trozo, «IHDR», ancho y alto en big-endian.</summary>
    public static byte[] PngCabecera(int ancho, int alto)
    {
        var bytes = new byte[24];

        ReadOnlySpan<byte> firma = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        firma.CopyTo(bytes);

        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), ancho);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), alto);

        return bytes;
    }

    /// <summary>
    /// JPEG: inicio de imagen, un marcador de relleno con carga —para que el
    /// recorrido tenga que saltárselo— y luego el inicio de fotograma con el
    /// alto antes que el ancho.
    /// </summary>
    public static byte[] JpegCabecera(int ancho, int alto, bool conRelleno = true)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };

        if (conRelleno)
        {
            // APP0: marcador, longitud (2 + 4 de carga) y la carga.
            bytes.AddRange([0xFF, 0xE0, 0x00, 0x06, 0x4A, 0x46, 0x49, 0x46]);
        }

        // SOF0: marcador, longitud, precisión, alto, ancho, componentes.
        bytes.AddRange([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        bytes.AddRange([(byte)(alto >> 8), (byte)(alto & 0xFF)]);
        bytes.AddRange([(byte)(ancho >> 8), (byte)(ancho & 0xFF)]);
        bytes.Add(0x03);

        return [.. bytes];
    }

    /// <summary>WebP en cualquiera de sus tres variantes.</summary>
    public static byte[] WebpCabecera(string variante, int ancho, int alto)
    {
        var bytes = new byte[32];

        "RIFF"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), bytes.Length - 8);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        System.Text.Encoding.ASCII.GetBytes(variante).CopyTo(bytes.AsSpan(12));

        switch (variante)
        {
            case "VP8 ":
                // Etiqueta de fotograma, código de sincronización y las
                // dimensiones en 14 bits cada una.
                ReadOnlySpan<byte> sync = [0x9D, 0x01, 0x2A];
                sync.CopyTo(bytes.AsSpan(23));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), (ushort)ancho);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28), (ushort)alto);
                break;

            case "VP8L":
                // Firma y los dos valores menos uno, empaquetados en 28 bits.
                bytes[20] = 0x2F;
                var packed = (uint)((ancho - 1) & 0x3FFF) | ((uint)((alto - 1) & 0x3FFF) << 14);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(21), packed);
                break;

            case "VP8X":
                // Cuatro bytes de banderas y el lienzo en tres bytes por
                // dimensión, también menos uno.
                EscribirUInt24(bytes.AsSpan(24), ancho - 1);
                EscribirUInt24(bytes.AsSpan(27), alto - 1);
                break;
        }

        return bytes;
    }

    private static void EscribirUInt24(Span<byte> destino, int valor)
    {
        destino[0] = (byte)(valor & 0xFF);
        destino[1] = (byte)((valor >> 8) & 0xFF);
        destino[2] = (byte)((valor >> 16) & 0xFF);
    }
}
