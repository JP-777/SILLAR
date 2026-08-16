using Microsoft.EntityFrameworkCore;
using Sillar.Core.Auditing;
using Sillar.Core.Data;
using Sillar.Core.Domain.Values;
using Sillar.Core.Dtos;
using Sillar.Core.Media;
using Sillar.Core.Modularity;
using Sillar.Shared.Paging;

namespace Sillar.Core.Services;

/// <summary>Filtros del listado de medios.</summary>
/// <param name="OwnerModuleCode">Módulo que lo subió.</param>
/// <param name="IsOrphan">Solo huérfanos, o solo los que tienen dueño.</param>
/// <param name="MimeType">Tipo real.</param>
/// <param name="From">Desde esta fecha.</param>
/// <param name="To">Hasta esta fecha.</param>
/// <param name="IncludeInactive">Incluir los dados de baja.</param>
internal sealed record MediaFilter(
    string? OwnerModuleCode,
    bool? IsOrphan,
    string? MimeType,
    DateTimeOffset? From,
    DateTimeOffset? To,
    bool IncludeInactive);

/// <summary>Subida, listado y baja de archivos.</summary>
internal sealed class MediaService(
    CoreDbContext database,
    MediaStorage storage,
    DeclaredModules declared,
    IAuditWriter audit)
{
    /// <summary>
    /// Comprueba que el módulo indicado exista en esta versión del producto.
    /// </summary>
    /// <remarks>
    /// Se valida contra el código, no contra la base: <c>owner_module_code</c> se
    /// guarda como texto y sin clave foránea a propósito (SPEC §4.8), porque el
    /// módulo puede desinstalarse y el archivo tiene que sobrevivir. La
    /// comprobación evita que se escriban códigos inventados.
    /// </remarks>
    public bool IsKnownModule(string? ownerModuleCode)
        => !string.IsNullOrWhiteSpace(ownerModuleCode)
            && declared.Modules.Any(module =>
                string.Equals(module.Code, ownerModuleCode, StringComparison.OrdinalIgnoreCase));

    /// <summary>Guarda un archivo y devuelve su ficha.</summary>
    public async Task<MediaUploadResponse> UploadAsync(
        Stream content,
        string originalName,
        string ownerModuleCode,
        string? altText,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var asset = await storage.SaveAsync(content, originalName, ownerModuleCode, cancellationToken);

        // El autor y el texto alternativo se completan después: no forman parte
        // del contrato de IMediaStorage, que los demás módulos usan sin sesión.
        var row = await database.MediaAssets
            .FirstAsync(candidate => candidate.MediaAssetId == asset.MediaAssetId, cancellationToken);

        row.CreatedBy = actingUserId;
        row.AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
        await database.SaveChangesAsync(cancellationToken);

        var duplicateOf = await FindDuplicateAsync(row.Checksum, row.MediaAssetId, cancellationToken);

        await audit.WriteAsync(
            new AuditEntry(AuditAction.Create)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CoreModule.ModuleCode,
                EntityType = "media_asset",
                EntityId = row.MediaAssetId.ToString(),
                Summary = $"Archivo subido para el módulo '{ownerModuleCode}': {row.StoredName} ({row.MimeType})."
            },
            cancellationToken);

        return new MediaUploadResponse(
            asset.MediaAssetId,
            asset.Url,
            asset.OriginalName,
            asset.MimeType,
            asset.SizeBytes,
            asset.Width,
            asset.Height,
            duplicateOf);
    }

    /// <summary>Lista los archivos, con filtros y paginación.</summary>
    public async Task<PagedResult<MediaResponse>> ListAsync(
        MediaFilter filter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = Filter(database.MediaAssets.AsNoTracking(), filter);
        var total = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(asset => asset.CreatedAt)
            .ThenByDescending(asset => asset.MediaAssetId)
            .Skip(page.Skip)
            .Take(page.Size)
            .ToListAsync(cancellationToken);

        return PagedResult<MediaResponse>.From(
            page,
            [.. items.Select(asset => new MediaResponse(
                asset.MediaAssetId,
                storage.PublicUrl(asset.RelativePath),
                asset.OriginalName,
                asset.MimeType,
                asset.SizeBytes,
                asset.Width,
                asset.Height,
                asset.AltText,
                asset.OwnerModuleCode,
                asset.IsOrphan,
                asset.IsActive,
                asset.CreatedAt))],
            total);
    }

    /// <summary>Da de baja un archivo. Baja lógica: el binario se conserva.</summary>
    public async Task<bool> DeleteAsync(
        Guid mediaAssetId,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        if (!await storage.DeleteAsync(mediaAssetId, cancellationToken))
        {
            return false;
        }

        await audit.WriteAsync(
            new AuditEntry(AuditAction.Delete)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CoreModule.ModuleCode,
                EntityType = "media_asset",
                EntityId = mediaAssetId.ToString(),
                Summary = "Archivo dado de baja. El binario se conserva en disco."
            },
            cancellationToken);

        return true;
    }

    /// <summary>
    /// Busca otro archivo activo con el mismo contenido.
    /// </summary>
    /// <remarks>
    /// Detectar y avisar, no reutilizar. Reutilizar la fila existente haría que
    /// dos módulos apuntaran al mismo archivo y que dar de baja uno rompiera al
    /// otro; evitarlo exige un recuento de referencias que nadie ha pedido.
    /// </remarks>
    private async Task<Guid?> FindDuplicateAsync(string? checksum, Guid excludeId, CancellationToken cancellationToken)
        => checksum is null
            ? null
            : await database.MediaAssets
                .AsNoTracking()
                .Where(asset => asset.Checksum == checksum && asset.IsActive && asset.MediaAssetId != excludeId)
                .OrderBy(asset => asset.MediaAssetId)
                .Select(asset => (Guid?)asset.MediaAssetId)
                .FirstOrDefaultAsync(cancellationToken);

    private static IQueryable<Domain.MediaAsset> Filter(
        IQueryable<Domain.MediaAsset> query,
        MediaFilter filter)
    {
        if (!filter.IncludeInactive)
        {
            query = query.Where(asset => asset.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(filter.OwnerModuleCode))
        {
            query = query.Where(asset => asset.OwnerModuleCode == filter.OwnerModuleCode);
        }

        if (filter.IsOrphan is { } isOrphan)
        {
            query = query.Where(asset => asset.IsOrphan == isOrphan);
        }

        if (!string.IsNullOrWhiteSpace(filter.MimeType))
        {
            query = query.Where(asset => asset.MimeType == filter.MimeType);
        }

        if (filter.From is { } from)
        {
            query = query.Where(asset => asset.CreatedAt >= from);
        }

        return filter.To is { } to ? query.Where(asset => asset.CreatedAt <= to) : query;
    }
}
