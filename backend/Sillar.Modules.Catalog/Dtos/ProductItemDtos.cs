namespace Sillar.Modules.Catalog.Dtos;

/// <summary>Una variante, vista desde la administración.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="ProductId">Producto al que pertenece.</param>
/// <param name="VariantValue">Lo que la distingue. Nulo si es la única del producto.</param>
/// <param name="Code">Código visible del negocio.</param>
/// <param name="Barcode">Código de barras.</param>
/// <param name="PriceOverride">Precio propio, si no usa el de lista.</param>
/// <param name="EffectivePrice">Ya resuelto: <c>priceOverride ?? listPrice</c> del producto (regla 5).</param>
/// <param name="ImageId">Imagen propia, si tiene.</param>
/// <param name="ImageUrl">Imagen propia, ya resuelta.</param>
/// <param name="SortOrder">Orden de presentación.</param>
/// <param name="IsActive">Baja lógica.</param>
public sealed record ProductItemResponse(
    Guid Id,
    Guid ProductId,
    string? VariantValue,
    string? Code,
    string? Barcode,
    decimal? PriceOverride,
    decimal? EffectivePrice,
    Guid? ImageId,
    string? ImageUrl,
    int SortOrder,
    bool IsActive);

/// <summary>
/// Crea una variante. Solo para la segunda y siguientes: la primera nace sola
/// con el producto (regla 2).
/// </summary>
/// <param name="VariantValue">Obligatorio: es lo que la distingue de las demás.</param>
/// <param name="Code">Opcional.</param>
/// <param name="Barcode">Opcional.</param>
/// <param name="PriceOverride">Opcional.</param>
/// <param name="ImageId">Opcional. Debe existir en <c>core.media_assets</c> y estar activo.</param>
public sealed record CreateProductItemRequest(
    string? VariantValue,
    string? Code,
    string? Barcode,
    decimal? PriceOverride,
    Guid? ImageId);

/// <summary>Modifica una variante.</summary>
public sealed record UpdateProductItemRequest(
    string? VariantValue,
    string? Code,
    string? Barcode,
    decimal? PriceOverride,
    Guid? ImageId,
    int? SortOrder,
    bool IsActive);

/// <summary>Resolución rápida por código, para la caja.</summary>
/// <param name="Item">La variante.</param>
/// <param name="ProductName">Nombre del producto al que pertenece, para mostrarlo sin otra consulta.</param>
/// <param name="ProductSlug">Su slug.</param>
public sealed record ItemLookupResponse(ProductItemResponse Item, string ProductName, string ProductSlug);
