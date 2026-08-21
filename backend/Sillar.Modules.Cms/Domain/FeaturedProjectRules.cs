namespace Sillar.Modules.Cms.Domain;

/// <summary>Reglas de publicación de un trabajo destacado.</summary>
internal static class FeaturedProjectRules
{
    /// <summary>
    /// Un trabajo puede guardarse sin imagen, pero solo está completo para la
    /// galería cuando la imagen sigue activa y tiene texto alternativo.
    /// </summary>
    internal static bool IsComplete(FeaturedProject project, string? imageUrl)
        => project.ImageId is not null
           && imageUrl is not null
           && !string.IsNullOrWhiteSpace(project.AltText);
}
