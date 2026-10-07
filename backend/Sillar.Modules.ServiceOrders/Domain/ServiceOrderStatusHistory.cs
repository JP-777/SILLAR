using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Domain;

/// <summary>Entrada durable del historial autoritativo de estados.</summary>
public sealed class ServiceOrderStatusHistory : IReplicatedEntity
{
    public Guid StatusHistoryId { get; set; } = Guid.CreateVersion7();
    public Guid ServiceOrderId { get; set; }
    public ServiceOrder? ServiceOrder { get; set; }
    public string? FromStatus { get; set; }
    public required string ToStatus { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? PerformedByName { get; set; }
    public int? PerformedByAdminUserId { get; set; }
    public string? PerformedByAdminUserHomeNode { get; set; }
    public string OriginNode { get; set; } = string.Empty;
    public long RowVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
