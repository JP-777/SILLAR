using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Domain;

/// <summary>Hecho inmutable de toma o liberación de una orden.</summary>
public sealed class ServiceOrderAssignmentEvent : IReplicatedEntity
{
    public Guid AssignmentEventId { get; set; } = Guid.CreateVersion7();
    public Guid ServiceOrderId { get; set; }
    public ServiceOrder? ServiceOrder { get; set; }
    public required string Action { get; set; }
    public required string AssigneeName { get; set; }
    public int AssigneeAdminUserId { get; set; }
    public required string AssigneeAdminUserHomeNode { get; set; }
    public required string PerformedByName { get; set; }
    public int PerformedByAdminUserId { get; set; }
    public required string PerformedByAdminUserHomeNode { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string OriginNode { get; set; } = string.Empty;
    public long RowVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
