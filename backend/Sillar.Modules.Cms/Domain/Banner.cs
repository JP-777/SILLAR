namespace Sillar.Modules.Cms.Domain;

public sealed class Banner : ScheduledCmsEntity
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public Guid? ImageDesktopId { get; set; }
    public Guid? ImageMobileId { get; set; }
    public string? AltText { get; set; }
    public string? LinkUrl { get; set; }
    public string? LinkLabel { get; set; }
    public int DisplayOrder { get; set; }
}
