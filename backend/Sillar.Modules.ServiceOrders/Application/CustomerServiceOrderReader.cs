using Microsoft.EntityFrameworkCore;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Data;

namespace Sillar.Modules.ServiceOrders.Application;

/// <summary>Proyección de M05b filtrada por propietario en la propia consulta SQL.</summary>
internal sealed class CustomerServiceOrderReader(ServiceOrdersDbContext database)
    : ICustomerServiceOrderReader
{
    private const int MaximumResults = 20;

    public async Task<IReadOnlyList<CustomerServiceOrderSummary>> ListForCustomerAsync(
        Guid customerId, int limit, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
        {
            return [];
        }

        return await database.ServiceOrders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.ReceivedAt)
            .ThenByDescending(order => order.ServiceOrderId)
            .Take(Math.Clamp(limit, 1, MaximumResults))
            .Select(order => new CustomerServiceOrderSummary(
                order.VisibleCode,
                order.Status,
                order.ReceivedAt,
                order.PromisedAt,
                order.LastStatusChangedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<CustomerServiceOrderDetail?> GetForCustomerAsync(
        Guid customerId, string visibleCode, CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty || string.IsNullOrWhiteSpace(visibleCode))
        {
            return null;
        }

        // Una única consulta proyecta cabecera y líneas sin materializar
        // las notas ni los datos de personal del agregado.
        return await database.ServiceOrders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId
                            && order.VisibleCode == visibleCode)
            .Select(order => new CustomerServiceOrderDetail(
                order.VisibleCode,
                order.Status,
                order.ReceivedAt,
                order.PromisedAt,
                order.LastStatusChangedAt,
                order.Items
                    .OrderBy(item => item.SortOrder)
                    .Select(item => new CustomerServiceWorkItem(
                        item.ServiceNameSnapshot,
                        item.ServiceShortDescriptionSnapshot,
                        item.Quantity,
                        item.SaleUnitSnapshot))
                    .ToArray()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
