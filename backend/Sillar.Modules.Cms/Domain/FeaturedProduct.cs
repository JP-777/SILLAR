namespace Sillar.Modules.Cms.Domain;

public sealed class FeaturedProduct : ScheduledCmsEntity
{
    public Guid? ProductId { get; set; }
    public required string ProductName { get; set; }
    public string? ProductSlug { get; set; }
    public Guid? ImageId { get; set; }
    public int DisplayOrder { get; set; }
}
