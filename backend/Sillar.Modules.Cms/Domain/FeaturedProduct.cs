namespace Sillar.Modules.Cms.Domain;

public sealed class FeaturedProduct : ScheduledCmsEntity
{
    public Guid? ProductId { get; set; }
    public required string ProductName { get; set; }
    public string? ProductSlug { get; set; }
    public Guid? ImageId { get; set; }

    /// <summary>
    /// Precio efectivo del snapshot: <c>null</c> significa a consultar,
    /// <c>0</c> significa gratis y un valor positivo es el importe publicado.
    /// </summary>
    public decimal? ProductPrice { get; set; }

    public bool ProductPriceVaries { get; set; }
    public string? ProductCategory { get; set; }
    public bool ProductIsPublic { get; set; }
    public int DisplayOrder { get; set; }
}
