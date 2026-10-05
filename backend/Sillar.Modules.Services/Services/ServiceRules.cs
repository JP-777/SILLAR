using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sillar.Modules.Services.Domain;
namespace Sillar.Modules.Services.Services;
internal static partial class ServiceRules
{
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugSeparators();
    internal static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var plain = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return SlugSeparators().Replace(plain.ToLowerInvariant(), "-").Trim('-');
    }
    internal static string? Validate(string? name, string? shortDescription, string? description,
        decimal? price, Guid? imageId, string? imageAltText)
    {
        if (string.IsNullOrWhiteSpace(name)) return "El nombre del servicio es obligatorio.";
        if (price is < 0) return "El precio no puede ser negativo.";
        if (string.IsNullOrWhiteSpace(shortDescription) && string.IsNullOrWhiteSpace(description))
            return "Escribe una descripción breve o una descripción completa.";
        if (imageId is not null && string.IsNullOrWhiteSpace(imageAltText))
            return "Describe la imagen para quienes no pueden verla.";
        if (imageId is null && imageAltText is not null && string.IsNullOrWhiteSpace(imageAltText))
            return "El texto alternativo no puede contener solo espacios.";
        return null;
    }
    internal static bool CanTransition(PublicationState from, PublicationState to) =>
        (from, to) is (PublicationState.Draft, PublicationState.Published)
            or (PublicationState.Published, PublicationState.Draft)
            or (PublicationState.Draft, PublicationState.Archived)
            or (PublicationState.Published, PublicationState.Archived);
}
