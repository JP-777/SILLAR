using Sillar.Modules.Catalog.Contracts.Events;
using Sillar.Modules.Cms.Services;
using Sillar.Shared.Events;

namespace Sillar.Modules.Cms.Events;

/// <summary>Relee el producto actualizado y sobrescribe todos sus snapshots.</summary>
internal sealed class ProductoActualizadoHandler(FeaturedProductSnapshotCoordinator snapshots)
    : IEventHandler<ProductoActualizado>
{
    public async Task HandleAsync(ProductoActualizado domainEvent, CancellationToken cancellationToken)
        => _ = await snapshots.RefreshProductAsync(domainEvent.ProductId, cancellationToken);
}

/// <summary>Relee la baja lógica para distinguirla de un producto inexistente.</summary>
internal sealed class ProductoDesactivadoHandler(FeaturedProductSnapshotCoordinator snapshots)
    : IEventHandler<ProductoDesactivado>
{
    public async Task HandleAsync(ProductoDesactivado domainEvent, CancellationToken cancellationToken)
        => _ = await snapshots.RefreshProductAsync(domainEvent.ProductId, cancellationToken);
}

/// <summary>
/// Relee todos los destacados cuando una categoría queda inactiva. El trabajo
/// está acotado por la portada, no por la cantidad de productos de Catálogo.
/// </summary>
internal sealed class CategoriaDesactivadaHandler(FeaturedProductSnapshotCoordinator snapshots)
    : IEventHandler<CategoriaDesactivada>
{
    public async Task HandleAsync(CategoriaDesactivada domainEvent, CancellationToken cancellationToken)
        => _ = await snapshots.RefreshAllAsync(cancellationToken);
}
