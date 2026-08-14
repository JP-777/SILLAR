using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sillar.Core.Contracts;
using Sillar.Core.Data;

namespace Sillar.Core.Media;

/// <summary>Lo que se averigua de un archivo mientras se recibe.</summary>
/// <param name="Format">Tipo real detectado.</param>
/// <param name="SizeBytes">Tamaño en bytes.</param>
/// <param name="Checksum">SHA-256 en hexadecimal.</param>
/// <param name="Dimensions">Ancho y alto, si se pudieron leer.</param>
internal sealed record StoredContent(
    ImageFormat Format,
    long SizeBytes,
    string Checksum,
    Dimensions? Dimensions);

/// <summary>
/// Guarda los archivos en disco y sus fichas en <c>core.media_assets</c>.
/// </summary>
/// <remarks>
/// El binario va al volumen; en la base solo queda su ficha (ADR-011).
/// </remarks>
internal sealed class MediaStorage(
    CoreDbContext database,
    IOptions<MediaOptions> options,
    TimeProvider clock,
    ILogger<MediaStorage> logger) : IMediaStorage
{
    private readonly MediaOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<MediaAsset> SaveAsync(
        Stream content,
        string originalName,
        string ownerModuleCode,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        // Se escribe primero en un temporal y se mueve al final. Un fallo a
        // mitad no puede dejar un archivo incompleto en la carpeta que se sirve.
        var tempPath = Path.Combine(EnsureFolder(MediaOptions.TempFolder), $"{Guid.CreateVersion7()}.part");

        try
        {
            var stored = await ReceiveAsync(content, tempPath, ct);

            var storedName = $"{Guid.CreateVersion7()}{stored.Format.Extension}";
            var relativeFolder = $"{now:yyyy}/{now:MM}";
            var relativePath = $"{relativeFolder}/{storedName}";

            // El movimiento es lo último que toca el disco: hasta aquí, nada
            // visible desde la ruta pública.
            File.Move(tempPath, Path.Combine(EnsureFolder(relativeFolder), storedName));

            var asset = new Domain.MediaAsset
            {
                StoredName = storedName,
                OriginalName = Truncate(originalName, 255),
                RelativePath = relativePath,
                MimeType = stored.Format.MimeType,
                SizeBytes = stored.SizeBytes,
                Width = stored.Dimensions?.Width,
                Height = stored.Dimensions?.Height,
                OwnerModuleCode = ownerModuleCode,
                Checksum = stored.Checksum,
                IsActive = true
            };

            database.MediaAssets.Add(asset);
            await database.SaveChangesAsync(ct);

            return Project(asset);
        }
        finally
        {
            // Todos los caminos: rechazo, excepción y cancelación. Si el
            // movimiento salió bien, el temporal ya no está y esto no hace nada.
            DeleteIfExists(tempPath);
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int mediaAssetId, CancellationToken ct)
    {
        // Baja lógica, como en el resto del proyecto: el binario se conserva.
        // Borrarlo dejaría un hueco en cualquier banner o producto que lo
        // referencie, y eso no se puede deshacer.
        var affected = await database.MediaAssets
            .Where(asset => asset.MediaAssetId == mediaAssetId && asset.IsActive)
            .ExecuteUpdateAsync(update => update.SetProperty(asset => asset.IsActive, false), ct);

        return affected > 0;
    }

    /// <inheritdoc />
    public string? GetPublicUrl(int mediaAssetId)
    {
        var relativePath = database.MediaAssets
            .AsNoTracking()
            .Where(asset => asset.MediaAssetId == mediaAssetId && asset.IsActive)
            .Select(asset => asset.RelativePath)
            .FirstOrDefault();

        return relativePath is null ? null : PublicUrl(relativePath);
    }

    /// <summary>Ruta pública de un archivo. Nunca una ruta del sistema de archivos.</summary>
    public string PublicUrl(string relativePath) => $"{_options.RequestPath}/{relativePath}";

    /// <summary>Ruta en disco de un archivo, a partir de su ruta relativa.</summary>
    public string PhysicalPath(string relativePath)
        => Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private string Root => Path.GetFullPath(_options.RootPath);

    /// <summary>
    /// Recibe el contenido en el temporal, comprobando tamaño y tipo.
    /// </summary>
    /// <remarks>
    /// El orden es deliberado: lo barato antes de gastar disco. El tope de bytes
    /// se aplica mientras se copia, no al terminar, para que un cuerpo que
    /// miente sobre su tamaño no llegue a escribirse entero.
    /// </remarks>
    private async Task<StoredContent> ReceiveAsync(Stream content, string tempPath, CancellationToken ct)
    {
        // Suficiente para detectar el tipo y leer las dimensiones de los tres
        // formatos, incluido un JPEG con EXIF por delante.
        var head = new byte[ImageDimensions.RecommendedBytes];
        var headLength = 0;
        long total = 0;

        using var digest = SHA256.Create();

        await using (var destination = new FileStream(
            tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            int read;

            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                total += read;

                if (total > _options.MaxSizeBytes)
                {
                    throw new MediaRejectedException(
                        $"El archivo pasa del máximo permitido de {_options.MaxSizeBytes / (1024 * 1024)} MB.",
                        tooLarge: true);
                }

                if (headLength < head.Length)
                {
                    var copy = Math.Min(read, head.Length - headLength);
                    buffer.AsSpan(0, copy).CopyTo(head.AsSpan(headLength));
                    headLength += copy;
                }

                digest.TransformBlock(buffer, 0, read, null, 0);
                await destination.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }

        if (total == 0)
        {
            throw new MediaRejectedException("El archivo está vacío.");
        }

        digest.TransformFinalBlock([], 0, 0);

        var header = head.AsSpan(0, headLength);
        var format = ContentSniffer.Detect(header);

        // Por los bytes iniciales, nunca por la extensión ni por el Content-Type
        // que envió el cliente: esos los elige quien sube el archivo.
        if (format is null)
        {
            throw new MediaRejectedException(
                "El contenido del archivo no es una imagen JPEG, PNG o WebP. " +
                "Se comprueba el contenido, no la extensión.");
        }

        return new StoredContent(
            format,
            total,
            Convert.ToHexString(digest.Hash!).ToLowerInvariant(),
            ImageDimensions.Read(format, header));
    }

    /// <summary>Crea la carpeta si falta y devuelve su ruta absoluta.</summary>
    private string EnsureFolder(string relativeFolder)
    {
        var path = Path.Combine(Root, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(path);
        return path;
    }

    private void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException exception)
        {
            // Que no se pueda borrar un temporal no puede convertir una subida
            // correcta en un error: se anota y sigue.
            logger.LogWarning(exception, "No se pudo borrar el archivo temporal {Path}.", path);
        }
    }

    /// <summary>Convierte la entidad en lo que ven los demás módulos.</summary>
    public MediaAsset Project(Domain.MediaAsset asset) => new(
        asset.MediaAssetId,
        PublicUrl(asset.RelativePath),
        asset.OriginalName,
        asset.MimeType,
        asset.SizeBytes,
        asset.Width,
        asset.Height);

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];
}
