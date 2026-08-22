using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Dtos;

/// <summary>Un banner publicado en la portada.</summary>
/// <param name="Id">Identificador del contenido.</param>
/// <param name="Title">Texto principal.</param>
/// <param name="Subtitle">Texto secundario.</param>
/// <param name="ImageDesktopUrl">Imagen apaisada ya resuelta; nunca un identificador de medio.</param>
/// <param name="ImageMobileUrl">Recorte móvil. Nulo indica que la interfaz usa la imagen de escritorio.</param>
/// <param name="AltText">Descripción accesible de las imágenes.</param>
/// <param name="LinkUrl">Destino del banner.</param>
/// <param name="LinkLabel">Texto visible del enlace.</param>
public sealed record BannerResponse(
    int Id,
    string? Title,
    string? Subtitle,
    string ImageDesktopUrl,
    string? ImageMobileUrl,
    string AltText,
    string? LinkUrl,
    string? LinkLabel);

/// <summary>Un banner visto desde administración.</summary>
public sealed record BannerAdminResponse(
    int Id,
    string? Title,
    string? Subtitle,
    Guid? ImageDesktopId,
    string? ImageDesktopUrl,
    Guid? ImageMobileId,
    string? ImageMobileUrl,
    string? AltText,
    string? LinkUrl,
    string? LinkLabel,
    int DisplayOrder,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive,
    bool IsCurrent,
    PublicationState PublicationState,
    bool IsComplete);

/// <summary>Crea un banner. El orden se asigna al final de la sección.</summary>
public sealed record CreateBannerRequest(
    string? Title,
    string? Subtitle,
    Guid? ImageDesktopId,
    Guid? ImageMobileId,
    string? AltText,
    string? LinkUrl,
    string? LinkLabel,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

/// <summary>Modifica un banner sin cambiar su orden ni desactivarlo.</summary>
public sealed record UpdateBannerRequest(
    string? Title,
    string? Subtitle,
    Guid? ImageDesktopId,
    Guid? ImageMobileId,
    string? AltText,
    string? LinkUrl,
    string? LinkLabel,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);
