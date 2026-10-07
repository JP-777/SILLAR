using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Domain;

/// <summary>Línea de trabajo con fotografía congelada del servicio de M05a.</summary>
public sealed class ServiceOrderItem : IReplicatedEntity
{
    public Guid ServiceOrderItemId { get; set; } = Guid.CreateVersion7();
    public Guid ServiceOrderId { get; set; }
    public ServiceOrder? ServiceOrder { get; set; }
    public int ServiceSourceId { get; set; }
    public required string ServiceSourceNode { get; set; }
    public required string ServiceNameSnapshot { get; set; }
    public required string ServiceSlugSnapshot { get; set; }
    public string? ServiceShortDescriptionSnapshot { get; set; }
    public string? ServiceDescriptionSnapshot { get; set; }
    public decimal? ShowcasePriceSnapshot { get; set; }
    public string? SaleUnitSnapshot { get; set; }
    public Guid? MediaAssetIdSnapshot { get; set; }
    public string? ImageUrlSnapshot { get; set; }
    public string? ImageAltTextSnapshot { get; set; }
    public required string RequestedDetails { get; set; }
    public decimal Quantity { get; set; }
    public decimal? AgreedUnitPrice { get; set; }
    public int SortOrder { get; set; }
    public string OriginNode { get; set; } = string.Empty;
    public long RowVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
