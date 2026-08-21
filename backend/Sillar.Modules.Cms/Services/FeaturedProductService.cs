using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Modules.Cms.Data;
using Sillar.Modules.Cms.Domain;
using Sillar.Modules.Cms.Dtos;

namespace Sillar.Modules.Cms.Services;

/// <summary>Productos destacados almacenados como snapshots editoriales.</summary>
internal sealed class FeaturedProductService(
    CmsDbContext database,
    IMediaStorage media,
    CmsOrderService order,
    TimeProvider clock)
{
    /// <summary>
    /// Lista pública desde el snapshot. El endpoint comprobará primero que el
    /// contrato opcional de M01 exista; este método nunca consulta Catálogo.
    /// </summary>
    internal async Task<IReadOnlyList<FeaturedProductResponse>> ListPublicAsync(
        CancellationToken cancellationToken)
    {
        var featured = await database.FeaturedProducts.AsNoTracking()
            .Where(PublicationWindow.CurrentAt<FeaturedProduct>(clock.GetUtcNow()))
            .Where(FeaturedProductRules.HasPublicProduct())
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return [.. featured.Select(item => new FeaturedProductResponse(
            item.Id,
            item.ProductName,
            item.ProductSlug,
            MediaUrl(item.ImageId),
            item.ProductPrice,
            item.ProductPriceVaries,
            item.ProductCategory,
            item.ProductIsPublic,
            item.ProductIsActive))];
    }

    internal async Task<IReadOnlyList<FeaturedProductAdminResponse>> ListAsync(
        CancellationToken cancellationToken)
    {
        var featured = await database.FeaturedProducts.AsNoTracking()
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        return [.. featured.Select(item => Project(item, now))];
    }

    internal async Task<FeaturedProductAdminResponse?> GetAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var featured = await database.FeaturedProducts.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return featured is null ? null : Project(featured, clock.GetUtcNow());
    }

    internal async Task<CmsOperation<FeaturedProductAdminResponse>> CreateAsync(
        CreateFeaturedProductRequest request,
        ProductPickerItem product,
        CancellationToken cancellationToken)
    {
        var error = ValidateSelection(request.ProductId, product)
                    ?? CmsContentRules.ValidatePeriod(request.StartsAt, request.EndsAt);
        if (error is not null)
        {
            return Invalid(error);
        }

        var lastOrder = await database.FeaturedProducts
            .Select(item => (int?)item.DisplayOrder)
            .MaxAsync(cancellationToken) ?? -1;
        var featured = new FeaturedProduct
        {
            ProductId = request.ProductId!.Value,
            ProductName = product.Name.Trim(),
            DisplayOrder = lastOrder + 1,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            IsActive = true
        };
        ApplySnapshot(featured, product);

        database.FeaturedProducts.Add(featured);
        await database.SaveChangesAsync(cancellationToken);
        return new CmsOperation<FeaturedProductAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(featured, clock.GetUtcNow()));
    }

    /// <summary>Actualiza solo fechas; este comando no modifica el snapshot.</summary>
    internal async Task<CmsOperation<FeaturedProductAdminResponse>> UpdateAsync(
        int id,
        UpdateFeaturedProductRequest request,
        CancellationToken cancellationToken)
    {
        var error = CmsContentRules.ValidatePeriod(request.StartsAt, request.EndsAt);
        if (error is not null)
        {
            return Invalid(error);
        }

        var featured = await database.FeaturedProducts.FirstOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);
        if (featured is null)
        {
            return new CmsOperation<FeaturedProductAdminResponse>(CmsOutcome.NotFound);
        }

        featured.StartsAt = request.StartsAt;
        featured.EndsAt = request.EndsAt;
        await database.SaveChangesAsync(cancellationToken);
        return new CmsOperation<FeaturedProductAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(featured, clock.GetUtcNow()));
    }

    /// <summary>
    /// Reenlaza el snapshot a partir de la selección resuelta por el panel.
    /// </summary>
    internal async Task<CmsOperation<FeaturedProductAdminResponse>> RelinkAsync(
        int id,
        RelinkFeaturedProductRequest request,
        ProductPickerItem product,
        CancellationToken cancellationToken)
    {
        var error = ValidateSelection(request.ProductId, product);
        if (error is not null)
        {
            return Invalid(error);
        }

        var featured = await database.FeaturedProducts.FirstOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);
        if (featured is null)
        {
            return new CmsOperation<FeaturedProductAdminResponse>(CmsOutcome.NotFound);
        }

        ApplySnapshot(featured, product);
        await database.SaveChangesAsync(cancellationToken);

        return new CmsOperation<FeaturedProductAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(featured, clock.GetUtcNow()));
    }

    internal async Task<CmsOperation<FeaturedProductAdminResponse>> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var featured = await database.FeaturedProducts.FirstOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);
        if (featured is null)
        {
            return new CmsOperation<FeaturedProductAdminResponse>(CmsOutcome.NotFound);
        }

        if (featured.IsActive)
        {
            featured.IsActive = false;
            await database.SaveChangesAsync(cancellationToken);
        }

        return new CmsOperation<FeaturedProductAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(featured, clock.GetUtcNow()));
    }

    internal Task<CmsOperation<IReadOnlyList<int>>> ReorderAsync(
        ReorderCmsRequest request,
        CancellationToken cancellationToken)
        => order.ReorderAsync(
            database.FeaturedProducts,
            request.OrderedIds,
            (featured, position) => featured.DisplayOrder = position,
            cancellationToken);

    internal Task<bool> HasLinkedProductAsync(Guid productId, CancellationToken cancellationToken)
        => database.FeaturedProducts.AnyAsync(
            featured => featured.ProductId == productId,
            cancellationToken);

    internal async Task<IReadOnlyList<Guid>> ListLinkedProductIdsAsync(CancellationToken cancellationToken)
        => await database.FeaturedProducts.AsNoTracking()
            .Where(featured => featured.ProductId != null)
            .Select(featured => featured.ProductId!.Value)
            .Distinct()
            .OrderBy(productId => productId)
            .ToListAsync(cancellationToken);

    internal async Task<FeaturedProductRefreshResponse> RefreshLinkedProductAsync(
        Guid productId,
        ProductPickerItem? product,
        CancellationToken cancellationToken)
    {
        var featured = await database.FeaturedProducts
            .Where(item => item.ProductId == productId)
            .ToListAsync(cancellationToken);
        if (featured.Count == 0)
        {
            return new FeaturedProductRefreshResponse(0, 0);
        }

        if (product is null)
        {
            foreach (var item in featured)
            {
                item.ProductId = null;
            }

            await database.SaveChangesAsync(cancellationToken);
            return new FeaturedProductRefreshResponse(featured.Count, featured.Count);
        }

        foreach (var item in featured)
        {
            ApplySnapshot(item, product);
        }

        await database.SaveChangesAsync(cancellationToken);
        return new FeaturedProductRefreshResponse(featured.Count, 0);
    }

    private string? ValidateSelection(Guid? productId, ProductPickerItem product)
    {
        if (productId is null)
        {
            return "Elige un producto del catálogo.";
        }

        if (productId != product.ProductId)
        {
            return "El producto resuelto no coincide con el que se eligió.";
        }

        if (!product.IsActive)
        {
            return "El producto elegido está dado de baja y no se puede destacar.";
        }

        if (string.IsNullOrWhiteSpace(product.Name) || string.IsNullOrWhiteSpace(product.Slug))
        {
            return "El producto elegido no tiene nombre y dirección pública completos.";
        }

        var snapshotError = FeaturedProductRules.ValidateSnapshotValues(product.Price, product.PrimaryCategoryName);
        if (snapshotError is not null)
        {
            return snapshotError;
        }

        return product.PrimaryImageId is not null && MediaUrl(product.PrimaryImageId) is null
            ? "La imagen principal del producto ya no existe o no está activa."
            : null;
    }

    internal static void ApplySnapshot(FeaturedProduct featured, ProductPickerItem product)
    {
        featured.ProductId = product.ProductId;
        featured.ProductName = product.Name.Trim();
        featured.ProductSlug = product.Slug.Trim();
        featured.ImageId = product.PrimaryImageId;
        featured.ProductPrice = product.Price;
        featured.ProductPriceVaries = product.PriceVaries;
        featured.ProductCategory = product.PrimaryCategoryName?.Trim();
        featured.ProductIsPublic = product.IsPublic;
        featured.ProductIsActive = product.IsActive;
    }

    private string? MediaUrl(Guid? id) => id is { } value ? media.GetPublicUrl(value) : null;

    private FeaturedProductAdminResponse Project(FeaturedProduct featured, DateTimeOffset now) => new(
        featured.Id,
        featured.ProductId,
        featured.ProductName,
        featured.ProductSlug,
        featured.ImageId,
        MediaUrl(featured.ImageId),
        featured.ProductPrice,
        featured.ProductPriceVaries,
        featured.ProductCategory,
        featured.ProductIsPublic,
        featured.ProductIsActive,
        featured.DisplayOrder,
        featured.StartsAt,
        featured.EndsAt,
        featured.IsActive,
        PublicationWindow.IsCurrent(featured, now),
        FeaturedProductRules.IsPendingRelink(featured));

    private static CmsOperation<FeaturedProductAdminResponse> Invalid(string error)
        => new(CmsOutcome.Invalid, error);
}
