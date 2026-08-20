namespace Sillar.Modules.Cms.Domain;

public abstract class CmsEntity
{
    public int Id { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
