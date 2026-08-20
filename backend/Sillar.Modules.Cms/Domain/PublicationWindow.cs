namespace Sillar.Modules.Cms.Domain;

/// <summary>Definición única de contenido vigente para las tres tablas programables.</summary>
internal static class PublicationWindow
{
    internal static bool IsCurrent(
        bool isActive,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        DateTimeOffset now) =>
        isActive &&
        (startsAt is null || startsAt <= now) &&
        (endsAt is null || endsAt > now);
}
