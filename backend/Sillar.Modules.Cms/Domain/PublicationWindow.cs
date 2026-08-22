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

    /// <summary>
    /// Clasifica el contenido para administración usando la misma evaluación
    /// que decide si está vigente. El inicio es inclusivo y el final exclusivo.
    /// </summary>
    internal static PublicationState StateAt<TEntity>(TEntity content, DateTimeOffset now)
        where TEntity : ScheduledCmsEntity
    {
        if (!content.IsActive)
        {
            return PublicationState.Inactive;
        }

        if (IsCurrent(content, now))
        {
            return PublicationState.Current;
        }

        return content.StartsAt is { } startsAt && startsAt > now
            ? PublicationState.Scheduled
            : PublicationState.Expired;
    }
}
