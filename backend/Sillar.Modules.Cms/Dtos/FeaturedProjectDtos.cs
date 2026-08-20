namespace Sillar.Modules.Cms.Dtos;

/// <summary>Un trabajo publicado en la galería.</summary>
public sealed record FeaturedProjectResponse(
    int Id,
    string Title,
    string? Description,
    string? ImageUrl,
    string? AltText);

/// <summary>Un trabajo visto desde administración.</summary>
public sealed record FeaturedProjectAdminResponse(
    int Id,
    string Title,
    string? Description,
    Guid? ImageId,
    string? ImageUrl,
    string? AltText,
    int DisplayOrder,
    bool IsActive);

/// <summary>Crea un trabajo destacado.</summary>
public sealed record CreateFeaturedProjectRequest(
    string? Title,
    string? Description,
    Guid? ImageId,
    string? AltText);

/// <summary>Modifica un trabajo sin cambiar su orden ni desactivarlo.</summary>
public sealed record UpdateFeaturedProjectRequest(
    string? Title,
    string? Description,
    Guid? ImageId,
    string? AltText);
