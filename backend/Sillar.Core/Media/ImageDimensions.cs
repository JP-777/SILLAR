using System.Buffers.Binary;

namespace Sillar.Core.Media;

/// <summary>Ancho y alto de una imagen, en píxeles.</summary>
public readonly record struct Dimensions(int Width, int Height);

/// <summary>
/// Lee las dimensiones de la cabecera, sin decodificar la imagen.
/// </summary>
/// <remarks>
/// Solo se recorren los primeros bytes: nunca se descomprimen los píxeles. Eso
/// evita de paso la clase de ataque en que una imagen diminuta se expande a
/// varios gigabytes al abrirla.
///
/// Si el formato no se reconoce o la cabecera está truncada, devuelve
/// <c>null</c>. No es motivo para rechazar el archivo: las dimensiones son
/// metadatos para el panel, mientras que la decisión de aceptarlo ya la tomó
/// <see cref="ContentSniffer"/>.
/// </remarks>
public static class ImageDimensions
{
    /// <summary>Bytes que conviene tener a mano para leer cualquiera de los tres formatos.</summary>
    /// <remarks>
    /// PNG y WebP resuelven en los primeros treinta. JPEG puede necesitar mucho
    /// más, porque el marcador con el tamaño va después de cabeceras de longitud
    /// variable —EXIF, miniaturas incrustadas—, así que se recorre lo que haya.
    /// </remarks>
    public const int RecommendedBytes = 64 * 1024;

    /// <summary>Lee las dimensiones según el formato indicado.</summary>
    public static Dimensions? Read(ImageFormat format, ReadOnlySpan<byte> content)
    {
        if (format == ImageFormat.Png)
        {
            return ReadPng(content);
        }

        return format == ImageFormat.Jpeg ? ReadJpeg(content) : ReadWebp(content);
    }

    /// <summary>
    /// PNG: el primer trozo es siempre IHDR, y empieza con el ancho y el alto en
    /// big-endian.
    /// </summary>
    private static Dimensions? ReadPng(ReadOnlySpan<byte> content)
    {
        // 8 de firma + 4 de longitud + 4 de «IHDR» = 16, y luego 4 + 4.
        if (content.Length < 24 || !content[12..16].SequenceEqual("IHDR"u8))
        {
            return null;
        }

        return new Dimensions(
            BinaryPrimitives.ReadInt32BigEndian(content[16..20]),
            BinaryPrimitives.ReadInt32BigEndian(content[20..24]));
    }

    /// <summary>
    /// JPEG: hay que recorrer los marcadores hasta dar con uno de inicio de
    /// fotograma, que es el que lleva el tamaño.
    /// </summary>
    private static Dimensions? ReadJpeg(ReadOnlySpan<byte> content)
    {
        var position = 2; // Tras el marcador de inicio de imagen.

        while (position + 3 < content.Length)
        {
            // Los marcadores empiezan por 0xFF; puede haber relleno de 0xFF.
            if (content[position] != 0xFF)
            {
                position++;
                continue;
            }

            var marker = content[position + 1];
            position += 2;

            // Marcadores sin carga: reinicio, inicio y fin de imagen.
            if (marker is 0x01 or >= 0xD0 and <= 0xD9)
            {
                continue;
            }

            if (position + 1 >= content.Length)
            {
                return null;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(content[position..]);

            // Inicio de fotograma: 0xC0 a 0xCF salvo 0xC4, 0xC8 y 0xCC, que son
            // tablas de Huffman y extensiones. Su carga es precisión (1 byte),
            // alto (2) y ancho (2).
            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
            {
                return position + 7 <= content.Length
                    ? new Dimensions(
                        BinaryPrimitives.ReadUInt16BigEndian(content[(position + 5)..]),
                        BinaryPrimitives.ReadUInt16BigEndian(content[(position + 3)..]))
                    : null;
            }

            if (length < 2)
            {
                return null;
            }

            position += length;
        }

        return null;
    }

    /// <summary>
    /// WebP: tres variantes con tres formas distintas de guardar el tamaño.
    /// </summary>
    private static Dimensions? ReadWebp(ReadOnlySpan<byte> content)
    {
        if (content.Length < 16)
        {
            return null;
        }

        var variant = content[12..16];

        // Con pérdida: tras la cabecera del trozo van tres bytes de etiqueta y
        // el código de sincronización 9D 01 2A; después, ancho y alto en 14 bits
        // cada uno, en little-endian.
        if (variant.SequenceEqual("VP8 "u8))
        {
            return content.Length < 30
                ? null
                : new Dimensions(
                    BinaryPrimitives.ReadUInt16LittleEndian(content[26..]) & 0x3FFF,
                    BinaryPrimitives.ReadUInt16LittleEndian(content[28..]) & 0x3FFF);
        }

        // Sin pérdida: un byte de firma (0x2F) y luego 14 bits de ancho menos
        // uno y 14 de alto menos uno, empaquetados.
        if (variant.SequenceEqual("VP8L"u8))
        {
            if (content.Length < 25 || content[20] != 0x2F)
            {
                return null;
            }

            var packed = BinaryPrimitives.ReadUInt32LittleEndian(content[21..]);

            return new Dimensions(
                (int)(packed & 0x3FFF) + 1,
                (int)((packed >> 14) & 0x3FFF) + 1);
        }

        // Extendido: cuatro bytes de banderas y luego el lienzo, tres bytes por
        // dimensión y también menos uno.
        if (variant.SequenceEqual("VP8X"u8))
        {
            return content.Length < 30
                ? null
                : new Dimensions(
                    ReadUInt24LittleEndian(content[24..]) + 1,
                    ReadUInt24LittleEndian(content[27..]) + 1);
        }

        return null;
    }

    private static int ReadUInt24LittleEndian(ReadOnlySpan<byte> value)
        => value[0] | (value[1] << 8) | (value[2] << 16);
}
