namespace Sillar.Modules.Cms.Domain;

public sealed class FeaturedProduct : CmsEntity
{
    public Guid? ProductId { get; set; }
    public required string ProductName { get; set; }
    public string? ProductSlug { get; set; }
    public Guid? ImageId { get; set; }
    public int DisplayOrder { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
}
