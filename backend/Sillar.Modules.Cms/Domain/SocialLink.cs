namespace Sillar.Modules.Cms.Domain;

public sealed class SocialLink : CmsEntity
{
    public required string Platform { get; set; }
    public required string Url { get; set; }
    public int DisplayOrder { get; set; }
}
