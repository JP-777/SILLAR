using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.Tracking.Contracts;

namespace Sillar.Modules.Tracking.Application;

/// <summary>
/// Frontera de M06 para el portal. Deliberadamente no accede a TrackingDbContext:
/// prioridad, plazos internos, notas y autoría NO son datos de la clientela.
/// </summary>
internal sealed class CustomerTrackingProgressService(ICustomerServiceOrderReader orders)
    : ICustomerTrackingProgress
{
    public async Task<IReadOnlyList<CustomerTrackingSummary>> ListForCustomerAsync(
        Guid customerId, int limit, CancellationToken cancellationToken)
    {
        var authorized = await orders.ListForCustomerAsync(
            customerId, limit, cancellationToken);

        return authorized
            .Select(order => new CustomerTrackingSummary(
                order.VisibleCode,
                order.CurrentStatus,
                order.ReceivedAt,
                order.PromisedAt,
                order.LastStatusChangedAt))
            .ToArray();
    }

    public async Task<CustomerTrackingDetail?> GetForCustomerAsync(
        Guid customerId, string visibleCode, CancellationToken cancellationToken)
    {
        // No confiar en que el consumidor listó antes: M05b revalida propiedad.
        var authorized = await orders.GetForCustomerAsync(
            customerId, visibleCode, cancellationToken);

        return authorized is null
            ? null
            : new CustomerTrackingDetail(
                authorized.VisibleCode,
                authorized.CurrentStatus,
                authorized.ReceivedAt,
                authorized.PromisedAt,
                authorized.LastStatusChangedAt,
                authorized.Items
                    .Select(item => new CustomerTrackingWorkItem(
                        item.ServiceName, item.PublicDescription, item.Quantity, item.SaleUnit))
                    .ToArray());
    }
}
