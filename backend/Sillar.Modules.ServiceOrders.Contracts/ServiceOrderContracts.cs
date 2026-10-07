using Sillar.Shared.Paging;

namespace Sillar.Modules.ServiceOrders.Contracts;

/// <summary>Vocabulario público de estados, propiedad exclusiva de M05b.</summary>
public static class ServiceOrderStatuses
{
    public const string Received = "received";
    public const string InProgress = "in_progress";
    public const string Ready = "ready";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    /// <summary>Estados vigentes, en el orden en que se presentan en un tablero.</summary>
    public static IReadOnlyList<ServiceOrderStateDefinition> All { get; } =
    [
        new(Received, "Recibida", 0, IsTerminal: false),
        new(InProgress, "En proceso", 1, IsTerminal: false),
        new(Ready, "Lista", 2, IsTerminal: false),
        new(Completed, "Completada", 3, IsTerminal: true),
        new(Cancelled, "Cancelada", 4, IsTerminal: true),
    ];

    /// <summary>Transiciones legales como dato de representación; la operación las revalida.</summary>
    public static IReadOnlyList<ServiceOrderTransitionDefinition> LegalTransitions { get; } =
    [
        new(Received, InProgress),
        new(Received, Cancelled),
        new(InProgress, Ready),
        new(InProgress, Cancelled),
        new(Ready, Completed),
        new(Ready, InProgress),
        new(Ready, Cancelled),
    ];
}

public sealed record ServiceOrderStateDefinition(
    string Code,
    string DisplayName,
    int DisplayOrder,
    bool IsTerminal);

public sealed record ServiceOrderTransitionDefinition(string FromStatus, string ToStatus);

public enum ServiceOrderScope { Open, Closed, All }
public enum ServiceOrderSort { ReceivedAt, PromisedAt, UpdatedAt, VisibleCode }
public enum ServiceOrderSortDirection { Ascending, Descending }

/// <summary>Consulta cerrada y tipada; no expone IQueryable, EF ni SQL.</summary>
public sealed record ServiceOrderQuery(
    ServiceOrderScope Scope,
    string? Status,
    ServiceOrderSort Sort,
    ServiceOrderSortDirection Direction,
    PageRequest Page);

public enum ServiceOrderOutcome { Ok, NotFound, Invalid, Conflict }

/// <summary>Resultado público cuya semántica vive en Outcome, no en Error.</summary>
public sealed record ServiceOrderOperation<T>(
    ServiceOrderOutcome Outcome,
    string? Error = null,
    T? Value = default);

/// <summary>Lectura durable que el futuro M06 consume sin tocar entidades ni schema.</summary>
public interface IServiceOrderTrackingSource
{
    Task<ServiceOrderTrackingSnapshot?> GetAsync(
        Guid serviceOrderId,
        CancellationToken cancellationToken);

    Task<PagedResult<ServiceOrderTrackingSummary>> ListAsync(
        ServiceOrderQuery query,
        CancellationToken cancellationToken);
}

/// <summary>Único contrato por el que un consumidor futuro provoca una transición.</summary>
public interface IServiceOrderTransitions
{
    Task<ServiceOrderOperation<ServiceOrderTransitionResult>> TransitionAsync(
        Guid serviceOrderId,
        string expectedStatus,
        string targetStatus,
        CancellationToken cancellationToken);
}

public sealed record ServiceOrderTrackingSummary(
    Guid ServiceOrderId,
    string VisibleCode,
    string CustomerName,
    string CurrentStatus,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PromisedAt,
    StaffSnapshot? CurrentAssignee,
    DateTimeOffset UpdatedAt);

public sealed record ServiceOrderTrackingSnapshot(
    Guid ServiceOrderId,
    string VisibleCode,
    string CustomerName,
    string CurrentStatus,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PromisedAt,
    IReadOnlyList<ServiceOrderWorkItemSnapshot> Items,
    StaffSnapshot? CurrentAssignee,
    IReadOnlyList<ServiceOrderStatusHistorySnapshot> StatusHistory,
    DateTimeOffset UpdatedAt);

public sealed record ServiceOrderWorkItemSnapshot(
    Guid ServiceOrderItemId,
    string ServiceName,
    string? SaleUnit,
    decimal Quantity,
    string RequestedDetails);

public sealed record StaffSnapshot(
    string DisplayName,
    int AdminUserId,
    string HomeNode);

public sealed record ServiceOrderStatusHistorySnapshot(
    Guid StatusHistoryId,
    string? FromStatus,
    string ToStatus,
    DateTimeOffset OccurredAt,
    StaffSnapshot? PerformedBy,
    string OriginNode);

public sealed record ServiceOrderTransitionResult(
    Guid ServiceOrderId,
    string CurrentStatus,
    ServiceOrderStatusHistorySnapshot HistoryEntry);
