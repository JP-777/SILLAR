using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Dtos;

/// <summary>Una promoción publicada.</summary>
public sealed record PromotionResponse(
    int Id,
    string? Title,
    string? Subtitle,
    string? Description,
    string? BadgeText,
    string? ImageUrl,
    string? AltText,
    string? LinkUrl,
    string? LinkLabel);

/// <summary>Una promoción vista desde administración.</summary>
public sealed record PromotionAdminResponse(
    int Id,
    string? Title,
    string? Subtitle,
    string? Description,
    string? BadgeText,
    Guid? ImageId,
    string? ImageUrl,
    string? AltText,
    string? LinkUrl,
    string? LinkLabel,
    int DisplayOrder,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive,
    bool IsCurrent,
    PublicationState PublicationState);

/// <summary>Crea una promoción. Puede ser solo texto.</summary>
public sealed record CreatePromotionRequest(
    string? Title,
    string? Subtitle,
    string? Description,
    string? BadgeText,
    Guid? ImageId,
    string? AltText,
    string? LinkUrl,
    string? LinkLabel,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

/// <summary>Modifica una promoción sin cambiar su orden ni desactivarla.</summary>
public sealed record UpdatePromotionRequest(
    string? Title,
    string? Subtitle,
    string? Description,
    string? BadgeText,
    Guid? ImageId,
    string? AltText,
    string? LinkUrl,
    string? LinkLabel,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);
