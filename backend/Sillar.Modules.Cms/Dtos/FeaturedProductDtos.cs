namespace Sillar.Modules.Cms.Dtos;

/// <summary>Un producto destacado publicado desde su snapshot editorial.</summary>
/// <param name="Id">Identificador editorial del destacado.</param>
/// <param name="ProductName">Nombre copiado del producto.</param>
/// <param name="ProductSlug">Dirección pública copiada del producto.</param>
/// <param name="ImageUrl">URL resuelta de la imagen copiada, si sigue disponible.</param>
/// <param name="ProductPrice"><c>null</c> indica precio a consultar, cero indica gratis y un valor positivo es el importe.</param>
/// <param name="ProductPriceVaries">Indica que la tarjeta debe presentar el importe como precio inicial.</param>
/// <param name="ProductCategory">Categoría efectiva; nula cuando el producto no tiene ninguna.</param>
/// <param name="ProductIsPublic">Estado público congelado en el snapshot; en esta respuesta siempre es verdadero.</param>
public sealed record FeaturedProductResponse(
    int Id,
    string ProductName,
    string? ProductSlug,
    string? ImageUrl,
    decimal? ProductPrice,
    bool ProductPriceVaries,
    string? ProductCategory,
    bool ProductIsPublic);

/// <summary>Un producto destacado visto desde administración, aunque M01 ya no lo publique.</summary>
public sealed record FeaturedProductAdminResponse(
    int Id,
    Guid? ProductId,
    string ProductName,
    string? ProductSlug,
    Guid? ImageId,
    string? ImageUrl,
    decimal? ProductPrice,
    bool ProductPriceVaries,
    string? ProductCategory,
    bool ProductIsPublic,
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

/// <summary>Modifica únicamente la vigencia; este comando no altera el snapshot.</summary>
public sealed record UpdateFeaturedProductRequest(
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

/// <summary>Vuelve a enlazar un snapshot huérfano con un producto elegido explícitamente.</summary>
public sealed record RelinkFeaturedProductRequest(Guid? ProductId);
