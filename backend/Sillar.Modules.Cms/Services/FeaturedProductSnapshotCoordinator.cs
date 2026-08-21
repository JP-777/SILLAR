using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Modules.Cms.Dtos;

namespace Sillar.Modules.Cms.Services;

/// <summary>
/// Serializa por producto la relectura completa de snapshots y abre un ámbito
/// propio para que también pueda usarse desde manejadores singleton del bus.
/// </summary>
internal sealed class FeaturedProductSnapshotCoordinator(IServiceScopeFactory scopes)
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> gates = new();

    internal async Task<FeaturedProductRefreshResponse?> RefreshProductAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (!await HasLinkedProductAsync(productId, cancellationToken))
        {
            return new FeaturedProductRefreshResponse(0, 0);
        }

        var gate = gates.GetOrAdd(productId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var cms = scope.ServiceProvider.GetRequiredService<FeaturedProductService>();
            if (!await cms.HasLinkedProductAsync(productId, cancellationToken))
            {
                return new FeaturedProductRefreshResponse(0, 0);
            }

            var catalog = scope.ServiceProvider.GetService<ICatalogService>();
            if (catalog is null)
            {
                return null;
            }

            var product = await catalog.ObtenerParaSeleccionAsync(productId, cancellationToken);
            return await cms.RefreshLinkedProductAsync(productId, product, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    internal async Task<FeaturedProductRefreshResponse?> RefreshAllAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> productIds;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var cms = scope.ServiceProvider.GetRequiredService<FeaturedProductService>();
            productIds = await cms.ListLinkedProductIdsAsync(cancellationToken);
        }

        var refreshed = 0;
        var pending = 0;
        foreach (var productId in productIds)
        {
            var result = await RefreshProductAsync(productId, cancellationToken);
            if (result is null)
            {
                return null;
            }

            refreshed += result.RefreshedCount;
            pending += result.PendingRelinkCount;
        }

        return new FeaturedProductRefreshResponse(refreshed, pending);
    }

    private async Task<bool> HasLinkedProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FeaturedProductService>()
            .HasLinkedProductAsync(productId, cancellationToken);
    }
}
