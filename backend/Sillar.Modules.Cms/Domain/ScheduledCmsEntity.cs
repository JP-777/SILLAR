namespace Sillar.Modules.Cms.Domain;

/// <summary>Contenido cuya publicación puede empezar y terminar automáticamente.</summary>
public abstract class ScheduledCmsEntity : CmsEntity
{
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
}
