using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Data;
using Sillar.Modules.Catalog.Domain;
using Sillar.Modules.Catalog.Dtos;

namespace Sillar.Modules.Catalog.Services;

/// <summary>Cómo terminó una operación sobre la galería de un producto.</summary>
internal enum ProductImageOutcome
{
    Ok,
    NotFound,
    Invalid,
    Conflict
}

/// <summary>Resultado de asociar o reordenar imágenes.</summary>
internal sealed record ProductImageOperation(
    ProductImageOutcome Outcome,
    string? Error = null,
    ProductImageAdminResponse? Image = null,
    IReadOnlyList<ProductImageAdminResponse>? Images = null);

/// <summary>
/// Galería de un producto: asociar, quitar y reordenar imágenes de CORE.
/// </summary>
/// <remarks>
/// <c>product_images</c> no lleva <c>is_active</c> a propósito: es una
/// asociación, no un dato de negocio por sí sola. Quitarla borra la fila,
/// nunca el archivo (regla 12 y criterios del SPEC).
/// </remarks>
internal sealed class ProductImageService(
    CatalogDbContext database,
    IMediaStorage media,
    IAuditWriter audit)
{
    /// <summary>Asocia un archivo de la galería de CORE a un producto.</summary>
    public async Task<ProductImageOperation> AssociateAsync(
        Guid productId,
        AssociateProductImageRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        if (request.MediaAssetId is not { } mediaAssetId)
        {
            return Invalid("Selecciona un archivo de la galería de CORE.");
        }

        if (media.GetPublicUrl(mediaAssetId) is null)
        {
            return Invalid("El archivo indicado no existe o no está activo.");
        }

        var product = await database.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            return new ProductImageOperation(ProductImageOutcome.NotFound);
        }

        if (product.Images.Any(image => image.MediaAssetId == mediaAssetId))
        {
            return new ProductImageOperation(ProductImageOutcome.Conflict, "Ese archivo ya está asociado a este producto.");
        }

        if (request.IsPrimary)
        {
            foreach (var current in product.Images.Where(image => image.IsPrimary))
            {
                current.IsPrimary = false;
            }
        }

        var image = new ProductImage
        {
            ProductId = productId,
            MediaAssetId = mediaAssetId,
            AltText = string.IsNullOrWhiteSpace(request.AltText) ? null : request.AltText.Trim(),
            SortOrder = product.Images.Count == 0 ? 0 : product.Images.Max(existing => existing.SortOrder) + 1,
            IsPrimary = request.IsPrimary
        };

        database.ProductImages.Add(image);
        await database.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditAction.Create, actingUserId, actingEmail, productId,
            $"Imagen asociada al producto.", cancellationToken);

        return new ProductImageOperation(ProductImageOutcome.Ok, Image: Project(image));
    }

    /// <summary>Quita una imagen de la galería. Nunca borra el archivo.</summary>
    public async Task<bool> RemoveAsync(
        Guid productId,
        Guid imageId,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var affected = await database.ProductImages
            .Where(image => image.Id == imageId && image.ProductId == productId)
            .ExecuteDeleteAsync(cancellationToken);

        if (affected == 0)
        {
            return false;
        }

        await AuditAsync(AuditAction.Delete, actingUserId, actingEmail, productId,
            "Imagen quitada de la galería del producto. El archivo se conserva.", cancellationToken);

        return true;
    }

    /// <summary>
    /// Reordena la galería y decide cuál es la principal (regla 11: máximo una).
    /// </summary>
    public async Task<ProductImageOperation> ReorderAsync(
        Guid productId,
        ReorderProductImagesRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var product = await database.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            return new ProductImageOperation(ProductImageOutcome.NotFound);
        }

        var orderedIds = request.OrderedImageIds ?? [];
        var currentIds = product.Images.Select(image => image.Id).ToHashSet();

        if (orderedIds.Count != currentIds.Count
            || orderedIds.Distinct().Count() != orderedIds.Count
            || orderedIds.Any(id => !currentIds.Contains(id)))
        {
            return Invalid("La lista debe incluir cada imagen del producto exactamente una vez.");
        }

        if (request.PrimaryImageId is { } primaryId && !currentIds.Contains(primaryId))
        {
            return Invalid("La imagen principal indicada no pertenece a este producto.");
        }

        for (var index = 0; index < orderedIds.Count; index++)
        {
            product.Images.First(image => image.Id == orderedIds[index]).SortOrder = index;
        }

        if (request.PrimaryImageId is { } chosenPrimary)
        {
            foreach (var image in product.Images)
            {
                image.IsPrimary = image.Id == chosenPrimary;
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditAction.Update, actingUserId, actingEmail, productId,
            "Galería reordenada.", cancellationToken);

        return new ProductImageOperation(
            ProductImageOutcome.Ok,
            Images: [.. product.Images.OrderBy(image => image.SortOrder).Select(Project)]);
    }

    private Task AuditAsync(
        string action,
        int actingUserId,
        string actingEmail,
        Guid productId,
        string summary,
        CancellationToken cancellationToken)
        => audit.WriteAsync(
            new AuditEntry(action)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CatalogModule.ModuleCode,
                EntityType = "product_image",
                EntityId = productId.ToString(),
                Summary = summary
            },
            cancellationToken);

    private static ProductImageOperation Invalid(string error) => new(ProductImageOutcome.Invalid, error);

    private ProductImageAdminResponse Project(ProductImage image) => new(
        image.Id,
        image.MediaAssetId,
        media.GetPublicUrl(image.MediaAssetId) ?? string.Empty,
        image.AltText,
        image.SortOrder,
        image.IsPrimary);
}
