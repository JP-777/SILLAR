namespace Sillar.Modules.Tracking.Contracts;

/// <summary>
/// Vista pública de avance de trabajos para un consumidor que ya autenticó
/// al cliente mediante M04. La propiedad se revalida en M05b en cada lectura.
/// </summary>
public interface ICustomerTrackingProgress
{
    Task<IReadOnlyList<CustomerTrackingSummary>> ListForCustomerAsync(
        Guid customerId, int limit, CancellationToken cancellationToken);

    Task<CustomerTrackingDetail?> GetForCustomerAsync(
        Guid customerId, string visibleCode, CancellationToken cancellationToken);
}

/// <summary>El estado es el autoritativo de M05b; nada proviene de notas internas.</summary>
public sealed record CustomerTrackingSummary(
    string VisibleCode,
    string CurrentStatus,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PromisedAt,
    DateTimeOffset LastStatusChangedAt);

public sealed record CustomerTrackingWorkItem(
    string ServiceName,
    string? PublicDescription,
    decimal Quantity,
    string? SaleUnit);

public sealed record CustomerTrackingDetail(
    string VisibleCode,
    string CurrentStatus,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PromisedAt,
    DateTimeOffset LastStatusChangedAt,
    IReadOnlyList<CustomerTrackingWorkItem> Items);
