using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
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
            .Where(item => item.ProductId != null)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return [.. featured.Select(item => new FeaturedProductResponse(
            item.Id,
            item.ProductName,
            item.ProductSlug,
            MediaUrl(item.ImageId)))];
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
        string productName,
        string productSlug,
        Guid? primaryImageId,
        CancellationToken cancellationToken)
    {
        var error = ValidateSelection(request.ProductId, productName, productSlug, primaryImageId)
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
            ProductName = productName.Trim(),
            ProductSlug = productSlug.Trim(),
            ImageId = primaryImageId,
            DisplayOrder = lastOrder + 1,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            IsActive = true
        };

        database.FeaturedProducts.Add(featured);
        await database.SaveChangesAsync(cancellationToken);
        return new CmsOperation<FeaturedProductAdminResponse>(
            CmsOutcome.Ok,
            Value: Project(featured, clock.GetUtcNow()));
    }

    /// <summary>Actualiza solo fechas; el snapshot no cambia por observar M01.</summary>
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
    /// Reenlaza o refresca el snapshot únicamente por una acción explícita del panel.
    /// </summary>
    internal async Task<CmsOperation<FeaturedProductAdminResponse>> RelinkAsync(
        int id,
        RelinkFeaturedProductRequest request,
        string productName,
        string productSlug,
        Guid? primaryImageId,
        CancellationToken cancellationToken)
    {
        var error = ValidateSelection(request.ProductId, productName, productSlug, primaryImageId);
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

        featured.ProductId = request.ProductId!.Value;
        featured.ProductName = productName.Trim();
        featured.ProductSlug = productSlug.Trim();
        featured.ImageId = primaryImageId;
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

    private string? ValidateSelection(
        Guid? productId,
        string productName,
        string productSlug,
        Guid? primaryImageId)
    {
        if (productId is null)
        {
            return "Elige un producto del catálogo.";
        }

        if (string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(productSlug))
        {
            return "El producto elegido no tiene nombre y dirección pública completos.";
        }

        return primaryImageId is not null && MediaUrl(primaryImageId) is null
            ? "La imagen principal del producto ya no existe o no está activa."
            : null;
    }

    private string? MediaUrl(Guid? id) => id is { } value ? media.GetPublicUrl(value) : null;

    private FeaturedProductAdminResponse Project(FeaturedProduct featured, DateTimeOffset now) => new(
        featured.Id,
        featured.ProductId,
        featured.ProductName,
        featured.ProductSlug,
        featured.ImageId,
        MediaUrl(featured.ImageId),
        featured.DisplayOrder,
        featured.StartsAt,
        featured.EndsAt,
        featured.IsActive,
        PublicationWindow.IsCurrent(featured, now),
        FeaturedProductRules.IsPendingRelink(featured));

    private static CmsOperation<FeaturedProductAdminResponse> Invalid(string error)
        => new(CmsOutcome.Invalid, error);
}
