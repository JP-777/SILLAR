using System.Linq.Expressions;

namespace Sillar.Modules.Cms.Domain;

/// <summary>Definición única de contenido vigente para las tres tablas programables.</summary>
internal static class PublicationWindow
{
    /// <summary>
    /// Expresión única, traducible por EF Core y compilable en memoria.
    /// </summary>
    internal static Expression<Func<TEntity, bool>> CurrentAt<TEntity>(DateTimeOffset now)
        where TEntity : ScheduledCmsEntity
        => content =>
            content.IsActive &&
            (content.StartsAt == null || content.StartsAt <= now) &&
            (content.EndsAt == null || content.EndsAt > now);

    internal static bool IsCurrent<TEntity>(TEntity content, DateTimeOffset now)
        where TEntity : ScheduledCmsEntity
        => CurrentAt<TEntity>(now).Compile()(content);
}
