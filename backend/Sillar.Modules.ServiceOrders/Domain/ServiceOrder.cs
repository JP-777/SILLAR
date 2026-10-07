using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Domain;

/// <summary>Recepción de uno o más servicios, apta para replicación.</summary>
public sealed class ServiceOrder : IReplicatedEntity
{
    public Guid ServiceOrderId { get; set; } = Guid.CreateVersion7();
    public required string VisibleCode { get; set; }
    public string Status { get; set; } = ServiceOrderStatuses.Received;
    public Guid? CustomerId { get; set; }
    public required string CustomerNameSnapshot { get; set; }
    public string? CustomerPhoneSnapshot { get; set; }
    public string? CustomerEmailSnapshot { get; set; }
    public string? ReceivedNotes { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? PromisedAt { get; set; }
    public required string CreatedByAdminName { get; set; }
    public int CreatedByAdminUserId { get; set; }
    public required string CreatedByAdminUserHomeNode { get; set; }
    public string? CurrentAssigneeName { get; set; }
    public int? CurrentAssigneeAdminUserId { get; set; }
    public string? CurrentAssigneeAdminUserHomeNode { get; set; }
    public DateTimeOffset LastStatusChangedAt { get; set; }
    public string? LastStatusChangedByName { get; set; }
    public int? LastStatusChangedByAdminUserId { get; set; }
    public string? LastStatusChangedByAdminUserHomeNode { get; set; }
    public string OriginNode { get; set; } = string.Empty;
    public long RowVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ServiceOrderItem> Items { get; set; } = [];
    public List<ServiceOrderAssignmentEvent> AssignmentEvents { get; set; } = [];
    public List<ServiceOrderStatusHistory> StatusHistory { get; set; } = [];
}
