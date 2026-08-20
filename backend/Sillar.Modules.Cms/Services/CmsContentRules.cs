namespace Sillar.Modules.Cms.Services;

/// <summary>Validaciones compartidas por los cinco servicios de CMS.</summary>
internal static class CmsContentRules
{
    internal static readonly IReadOnlySet<string> SocialPlatforms = new HashSet<string>(StringComparer.Ordinal)
    {
        "facebook",
        "instagram",
        "tiktok",
        "whatsapp",
        "youtube"
    };

    internal static string? ValidatePeriod(DateTimeOffset? startsAt, DateTimeOffset? endsAt)
        => startsAt is not null && endsAt is not null && endsAt <= startsAt
            ? "La fecha de fin debe ser posterior a la fecha de inicio."
            : null;

    internal static string? ValidateLink(string? linkUrl, string? linkLabel)
    {
        if (string.IsNullOrWhiteSpace(linkUrl))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(linkLabel))
        {
            return "Escribe el texto que se mostrará en el enlace.";
        }

        return IsInternalOrHttpUrl(linkUrl)
            ? null
            : "El enlace debe ser una ruta interna que empiece por / o una URL HTTP o HTTPS.";
    }

    internal static string? ValidateAltText(bool hasImage, string? altText)
    {
        if (hasImage && string.IsNullOrWhiteSpace(altText))
        {
            return "Escribe el texto alternativo de la imagen.";
        }

        return altText is not null && string.IsNullOrWhiteSpace(altText)
            ? "El texto alternativo no puede quedar vacío."
            : null;
    }

    internal static string? ValidateOptionalText(string? value, string field)
        => value is not null && string.IsNullOrWhiteSpace(value)
            ? $"{field} no puede quedar vacío."
            : null;

    internal static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static string? NormalizePlatform(string? platform)
        => string.IsNullOrWhiteSpace(platform) ? null : platform.Trim().ToLowerInvariant();

    internal static string? ValidateSocialLink(string? platform, string? url)
    {
        var normalized = NormalizePlatform(platform);
        if (normalized is null || !SocialPlatforms.Contains(normalized))
        {
            return "Elige una red admitida: Facebook, Instagram, TikTok, WhatsApp o YouTube.";
        }

        return IsAbsoluteHttpUrl(url)
            ? null
            : "La dirección de la red debe ser una URL completa HTTP o HTTPS.";
    }

    private static bool IsInternalOrHttpUrl(string value)
        => value.TrimStart().StartsWith('/') || IsAbsoluteHttpUrl(value);

    private static bool IsAbsoluteHttpUrl(string? value)
        => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
