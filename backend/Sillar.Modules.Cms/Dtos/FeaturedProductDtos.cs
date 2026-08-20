namespace Sillar.Modules.Cms.Dtos;

/// <summary>Un producto destacado publicado desde su snapshot editorial.</summary>
public sealed record FeaturedProductResponse(
    int Id,
    string ProductName,
    string? ProductSlug,
    string? ImageUrl);

/// <summary>Un producto destacado visto desde administración.</summary>
public sealed record FeaturedProductAdminResponse(
    int Id,
    Guid? ProductId,
    string ProductName,
    string? ProductSlug,
    Guid? ImageId,
    string? ImageUrl,
    int DisplayOrder,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive,
    bool IsCurrent,
    bool PendingRelink);

/// <summary>Destaca un producto elegido mediante el contrato de selección de M01.</summary>
/// <param name="ProductId">Producto que el adaptador de Catálogo debe resolver antes de llamar al servicio.</param>
/// <param name="StartsAt">Inicio opcional de publicación.</param>
/// <param name="EndsAt">Fin opcional de publicación.</param>
public sealed record CreateFeaturedProductRequest(
    Guid? ProductId,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

/// <summary>Modifica únicamente la vigencia; nunca refresca el snapshot en silencio.</summary>
public sealed record UpdateFeaturedProductRequest(
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

/// <summary>Vuelve a enlazar un snapshot huérfano con un producto elegido explícitamente.</summary>
public sealed record RelinkFeaturedProductRequest(Guid? ProductId);
