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
/// <param name="ProductIsActive">Estado de alta del producto de M01; en esta respuesta siempre es verdadero.</param>
public sealed record FeaturedProductResponse(
    int Id,
    string ProductName,
    string? ProductSlug,
    string? ImageUrl,
    decimal? ProductPrice,
    bool ProductPriceVaries,
    string? ProductCategory,
    bool ProductIsPublic,
    bool ProductIsActive);

/// <summary>Un producto destacado visto desde administración, aunque M01 ya no lo publique.</summary>
/// <param name="Id">Identificador editorial.</param>
/// <param name="ProductId">Producto enlazado; nulo significa pendiente de reenlace.</param>
/// <param name="ProductName">Nombre congelado del producto.</param>
/// <param name="ProductSlug">Dirección pública congelada.</param>
/// <param name="ImageId">Medio congelado para el selector administrativo.</param>
/// <param name="ImageUrl">URL del medio si continúa disponible.</param>
/// <param name="ProductPrice">Precio efectivo congelado.</param>
/// <param name="ProductPriceVaries">Indica si el precio debe presentarse como inicial.</param>
/// <param name="ProductCategory">Categoría efectiva congelada.</param>
/// <param name="ProductIsPublic">Indica si el producto está publicado en M01.</param>
/// <param name="ProductIsActive">Indica si el producto enlazado sigue de alta en M01.</param>
/// <param name="DisplayOrder">Posición editorial.</param>
/// <param name="StartsAt">Inicio opcional de vigencia.</param>
/// <param name="EndsAt">Fin opcional de vigencia.</param>
/// <param name="IsActive">Indica si el destacado editorial sigue de alta en CMS.</param>
/// <param name="IsCurrent">Indica si su vigencia editorial está abierta ahora.</param>
/// <param name="PendingRelink">Indica que el producto ya no existe y hay que elegir otro.</param>
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
    bool ProductIsActive,
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

/// <summary>Producto activo de M01 disponible para elegir como destacado.</summary>
public sealed record FeaturedProductPickerResponse(
    Guid ProductId,
    string Name,
    string Slug,
    string? ImageUrl,
    string? PrimaryCategoryName,
    decimal? Price,
    bool PriceVaries,
    bool IsPublic,
    bool IsActive);

/// <summary>Resultado observable de una reconciliación de snapshots.</summary>
public sealed record FeaturedProductRefreshResponse(int RefreshedCount, int PendingRelinkCount);
