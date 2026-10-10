namespace Sillar.Modules.ServiceOrders.Contracts;

/// <summary>
/// Consulta de trabajos exclusivamente por la identidad de cliente suministrada
/// por un consumidor autenticado. No autoriza endpoints anónimos.
/// </summary>
/// <remarks>
/// El filtro por CustomerId lo aplica M05b en PostgreSQL, ANTES de proyectar.
/// Una orden manual sin CustomerId nunca se reclama por coincidencia de contacto.
/// Ningún DTO de esta frontera contiene notas, personal, prioridades ni IDs técnicos.
/// </remarks>
public interface ICustomerServiceOrderReader
{
    Task<IReadOnlyList<CustomerServiceOrderSummary>> ListForCustomerAsync(
        Guid customerId, int limit, CancellationToken cancellationToken);

    Task<CustomerServiceOrderDetail?> GetForCustomerAsync(
        Guid customerId, string visibleCode, CancellationToken cancellationToken);
}

public sealed record CustomerServiceOrderSummary(
    string VisibleCode,
    string CurrentStatus,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PromisedAt,
    DateTimeOffset LastStatusChangedAt);

public sealed record CustomerServiceWorkItem(
    string ServiceName,
    string? PublicDescription,
    decimal Quantity,
    string? SaleUnit);

public sealed record CustomerServiceOrderDetail(
    string VisibleCode,
    string CurrentStatus,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PromisedAt,
    DateTimeOffset LastStatusChangedAt,
    IReadOnlyList<CustomerServiceWorkItem> Items);
