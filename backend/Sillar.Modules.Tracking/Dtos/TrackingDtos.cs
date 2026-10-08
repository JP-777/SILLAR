namespace Sillar.Modules.Tracking.Dtos;

/// <summary>Fija o elimina la prioridad manual de una orden y decide si queda anclada.</summary>
/// <param name="BoardPriority"><c>null</c> elimina la prioridad; cero es una prioridad válida.</param>
/// <param name="Pinned">Indica si la tarjeta aparece antes que las no fijadas.</param>
/// <param name="OrderedPeerIds">Orden completo de las tarjetas del mismo estado cuando se realiza un reordenamiento; <c>null</c> conserva el modo de actualización individual.</param>
public sealed record SetTrackingPriorityRequest(
    int? BoardPriority,
    bool Pinned,
    IReadOnlyList<Guid>? OrderedPeerIds = null);

/// <summary>Fija o limpia el plazo interno de taller.</summary>
/// <param name="InternalDueAt"><c>null</c> limpia el plazo.</param>
public sealed record SetTrackingDueRequest(DateTimeOffset? InternalDueAt);

/// <summary>Añade una nota interna de seguimiento.</summary>
/// <param name="Body">Texto interno; se recorta antes de guardarlo.</param>
public sealed record AddTrackingNoteRequest(string? Body);

/// <summary>Solicita a M05b una transición condicionada al estado que vio el cliente.</summary>
public sealed record TransitionTrackingStatusRequest(string? ExpectedStatus, string? TargetStatus);

public sealed record TrackingBoardResponse(
    IReadOnlyList<TrackingBoardColumnResponse> Columns,
    TrackingBoardPaginationResponse? Pagination = null);

public sealed record TrackingBoardPaginationResponse(
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages,
    bool HasNext);

public sealed record TrackingBoardColumnResponse(
    string Status,
    string DisplayName,
    int DisplayOrder,
    bool IsTerminal,
    IReadOnlyList<string> LegalTargetStatuses,
    IReadOnlyList<TrackingBoardCardResponse> Cards);

public sealed record TrackingBoardCardResponse(
    Guid ServiceOrderId,
    string VisibleCode,
    string CustomerName,
    string CurrentStatus,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PromisedAt,
    TrackingStaffResponse? CurrentAssignee,
    int? BoardPriority,
    bool Pinned,
    DateTimeOffset? InternalDueAt,
    DateTimeOffset OrderUpdatedAt);

public sealed record TrackingOrderDetailResponse(
    TrackingOrderReadOnlyResponse Order,
    TrackingEditableResponse Tracking);

public sealed record TrackingOrderReadOnlyResponse(
    Guid ServiceOrderId,
    string VisibleCode,
    string CustomerName,
    string CurrentStatus,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PromisedAt,
    IReadOnlyList<TrackingWorkItemResponse> Items,
    TrackingStaffResponse? CurrentAssignee,
    IReadOnlyList<TrackingStatusHistoryResponse> StatusHistory,
    DateTimeOffset UpdatedAt);

public sealed record TrackingEditableResponse(
    int? BoardPriority,
    bool Pinned,
    DateTimeOffset? InternalDueAt,
    IReadOnlyList<TrackingNoteResponse> Notes);

public sealed record TrackingWorkItemResponse(
    Guid ServiceOrderItemId,
    string ServiceName,
    string? SaleUnit,
    decimal Quantity,
    string RequestedDetails);

public sealed record TrackingStaffResponse(string DisplayName, int AdminUserId, string HomeNode);

public sealed record TrackingStatusHistoryResponse(
    Guid StatusHistoryId,
    string? FromStatus,
    string ToStatus,
    DateTimeOffset OccurredAt,
    TrackingStaffResponse? PerformedBy,
    string OriginNode);

public sealed record TrackingNoteResponse(
    Guid TrackingNoteId,
    string Body,
    TrackingStaffResponse? Author,
    DateTimeOffset CreatedAt,
    bool IsActive);

public sealed record TrackingMutationResponse(
    Guid ServiceOrderId,
    int? BoardPriority,
    bool Pinned,
    DateTimeOffset? InternalDueAt,
    DateTimeOffset UpdatedAt);

public sealed record TransitionTrackingStatusResponse(
    Guid ServiceOrderId,
    string CurrentStatus,
    TrackingStatusHistoryResponse HistoryEntry);
