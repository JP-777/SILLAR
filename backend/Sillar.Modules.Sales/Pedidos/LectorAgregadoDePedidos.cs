using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>
/// Agrega exclusivamente sales.orders. Una sola sentencia SQL agrupa por
/// estado actual y actividad; en memoria solo se reduce el número acotado
/// de grupos, no se materializan entidades Order ni datos personales.
/// </summary>
internal sealed class LectorAgregadoDePedidos(
    SalesDbContext database,
    TimeProvider clock) : IOrderAggregateReader
{
    /// <inheritdoc />
    public async Task<OrderAggregateSnapshot> ReadCreatedBetweenAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        if (endExclusive <= startInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endExclusive), "El fin debe ser posterior al inicio.");
        }

        // Npgsql requiere marcas de tiempo UTC para timestamptz.
        var startUtc = startInclusive.ToUniversalTime();
        var endUtc = endExclusive.ToUniversalTime();

        // Es una referencia temporal tomada justo antes de la lectura, no un
        // historial de estados ni una promesa de instantánea transnodo.
        var observedAtUtc = clock.GetUtcNow();

        // PostgreSQL produce a lo sumo 7 x 2 grupos. En particular NO heredar
        // HistorialDePedidos.Consulta, que excluye IsActive == false.
        var groups = await database.Orders
            .AsNoTracking()
            .Where(order => order.CreatedAt >= startUtc && order.CreatedAt < endUtc)
            .GroupBy(order => new { order.Status, order.IsActive })
            .Select(group => new
            {
                group.Key.Status,
                group.Key.IsActive,
                Count = group.LongCount()
            })
            .ToListAsync(cancellationToken);

        var byStatus = OrderStatus.All
            .Select(status => new OrderStatusCount(
                status,
                groups.Where(group => group.Status == status)
                    .Sum(group => group.Count)))
            .ToArray();

        var active = groups.Where(group => group.IsActive)
            .Sum(group => group.Count);
        var inactive = groups.Where(group => !group.IsActive)
            .Sum(group => group.Count);

        // I04 identifica el estado actual 'expired'. No se infiere un nuevo
        // vencimiento a partir de payment_due_at ni de la edad del pedido.
        var expired = byStatus.Single(group => group.Status == OrderStatus.Expired).Count;

        return new OrderAggregateSnapshot(
            startUtc,
            endUtc,
            observedAtUtc,
            active + inactive,
            byStatus,
            expired,
            new OrderActivityCounts(active, inactive));
    }
}
