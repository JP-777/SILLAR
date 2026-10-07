using Sillar.Shared.Replication;

namespace Sillar.Modules.Tracking.Domain;

public sealed class TrackingNote : IReplicatedEntity
{
    public Guid TrackingNoteId { get; set; } = Guid.CreateVersion7();

    public Guid OrderTrackingId { get; set; }
    public OrderTracking? OrderTracking { get; set; }

    public required string Body { get; set; }

    public string? AuthorName { get; set; }
    public int? AuthorAdminUserId { get; set; }
    public string? AuthorAdminUserHomeNode { get; set; }

    public bool IsActive { get; set; } = true;

    public string OriginNode { get; set; } = string.Empty;
    public long RowVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
