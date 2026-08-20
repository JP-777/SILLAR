namespace Sillar.Modules.Cms.Domain;

public sealed class FeaturedProject : CmsEntity
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public Guid? ImageId { get; set; }
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; }
}
