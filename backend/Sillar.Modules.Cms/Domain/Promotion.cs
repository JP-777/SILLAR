namespace Sillar.Modules.Cms.Domain;

public sealed class Promotion : CmsEntity
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public Guid? ImageId { get; set; }
    public required string AltText { get; set; }
    public string? LinkUrl { get; set; }
    public string? LinkLabel { get; set; }
    public int DisplayOrder { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string? Description { get; set; }
    public string? BadgeText { get; set; }
}
