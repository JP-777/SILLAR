using Sillar.Shared.Replication;

namespace Sillar.Modules.Tracking.Domain;

public sealed class OrderTracking : IReplicatedEntity
{
    public Guid OrderTrackingId { get; set; } = Guid.CreateVersion7();
    public Guid ServiceOrderId { get; set; }

    public int? BoardPriority { get; set; }
    public DateTimeOffset? InternalDueAt { get; set; }
    public bool Pinned { get; set; }

    public string? LastTouchedByName { get; set; }
    public int? LastTouchedByAdminUserId { get; set; }
    public string? LastTouchedByAdminUserHomeNode { get; set; }
    public DateTimeOffset? LastTouchedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public string OriginNode { get; set; } = string.Empty;
    public long RowVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<TrackingNote> Notes { get; set; } = [];
}
