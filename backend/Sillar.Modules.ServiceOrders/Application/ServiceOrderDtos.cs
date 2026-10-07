using Sillar.Modules.ServiceOrders.Contracts;

namespace Sillar.Modules.ServiceOrders.Application;

public sealed record CreateServiceOrderLineRequest(
    int ServiceId,
    string RequestedDetails,
    decimal Quantity,
    decimal? AgreedUnitPrice);

public sealed record CreateServiceOrderRequest(
    Guid IdempotencyKey,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string? ReceivedNotes,
    DateTimeOffset? ReceivedAt,
    DateTimeOffset? PromisedAt,
    bool AssignToMe,
    IReadOnlyList<CreateServiceOrderLineRequest> Items);

public sealed record UpdateServiceOrderLineRequest(
    Guid? ServiceOrderItemId,
    int? ServiceId,
    string RequestedDetails,
    decimal Quantity,
    decimal? AgreedUnitPrice);

public sealed record UpdateServiceOrderRequest(
    DateTimeOffset ExpectedUpdatedAt,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string? ReceivedNotes,
    DateTimeOffset? PromisedAt,
    IReadOnlyList<UpdateServiceOrderLineRequest> Items);

public sealed record ServiceOrderConcurrencyRequest(DateTimeOffset ExpectedUpdatedAt);

public sealed record ServiceOrderTransitionRequest(
    string ExpectedStatus,
    string TargetStatus);

public sealed record ServiceOrderAdminSummary(
    Guid ServiceOrderId,
    string VisibleCode,
    string CustomerName,
    string CurrentStatus,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PromisedAt,
    StaffSnapshot? CurrentAssignee,
    bool HasPendingPrice,
    decimal? TotalAmount,
    DateTimeOffset UpdatedAt);

public sealed record ServiceOrderAdminItem(
    Guid ServiceOrderItemId,
    int ServiceSourceId,
    string ServiceSourceNode,
    string ServiceName,
    string ServiceSlug,
    string? ServiceShortDescription,
    string? ServiceDescription,
    decimal? ShowcasePrice,
    string? SaleUnit,
    Guid? MediaAssetId,
    string? ImageUrl,
    string? ImageAltText,
    string RequestedDetails,
    decimal Quantity,
    decimal? AgreedUnitPrice,
    int SortOrder);

public sealed record ServiceOrderAssignmentSnapshot(
    Guid AssignmentEventId,
    string Action,
    StaffSnapshot Assignee,
    StaffSnapshot PerformedBy,
    DateTimeOffset OccurredAt,
    string OriginNode);

public sealed record ServiceOrderAdminDetail(
    Guid ServiceOrderId,
    string VisibleCode,
    string CurrentStatus,
    Guid? CustomerId,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string? ReceivedNotes,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PromisedAt,
    StaffSnapshot CreatedBy,
    StaffSnapshot? CurrentAssignee,
    IReadOnlyList<ServiceOrderAdminItem> Items,
    IReadOnlyList<ServiceOrderStatusHistorySnapshot> StatusHistory,
    IReadOnlyList<ServiceOrderAssignmentSnapshot> AssignmentHistory,
    bool HasPendingPrice,
    decimal? TotalAmount,
    DateTimeOffset UpdatedAt);

internal enum ServiceOrderAdminOutcome
{
    Ok,
    NotFound,
    Invalid,
    Conflict
}

internal sealed record ServiceOrderAdminOperation<T>(
    ServiceOrderAdminOutcome Outcome,
    string? Error = null,
    T? Value = default,
    bool IsReplay = false);
