namespace Sillar.Modules.Cms.Dtos;

/// <summary>Un enlace social publicado.</summary>
public sealed record SocialLinkResponse(int Id, string Platform, string Url);

/// <summary>Un enlace social visto desde administración.</summary>
public sealed record SocialLinkAdminResponse(
    int Id,
    string Platform,
    string Url,
    int DisplayOrder,
    bool IsActive);

/// <summary>Crea un enlace a una red admitida por CMS.</summary>
public sealed record CreateSocialLinkRequest(string? Platform, string? Url);

/// <summary>Modifica un enlace social sin cambiar su orden ni desactivarlo.</summary>
public sealed record UpdateSocialLinkRequest(string? Platform, string? Url);
