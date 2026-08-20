namespace Sillar.Modules.Catalog.Dtos;

/// <summary>Un producto en un listado público.</summary>
/// <param name="Slug">Para la URL pública.</param>
/// <param name="Name">Producto + característica + marca + presentación, según el diccionario.</param>
/// <param name="ShortDescription">Una o dos líneas, para la tarjeta.</param>
/// <param name="PrimaryImageUrl">La imagen marcada como principal, o la de menor orden si ninguna lo está (regla 11).</param>
/// <param name="Price">
/// Lo que cuesta, ya resuelto para la tarjeta: el <b>mínimo efectivo</b> de sus
/// presentaciones activas. Nulo es «a consultar» — y basta con que una lo sea.
/// No es el <c>list_price</c>: enseñar ése cuando una presentación tiene precio
/// propio es enseñar un número que no se cobra.
/// </param>
/// <param name="PriceVaries">
/// Si las presentaciones no cuestan lo mismo. La tarjeta no tiene selector, así
/// que el precio es una cota y hay que decirlo: «Desde S/ 5,50».
/// <b>El servidor dice el hecho; la frase la pone la pantalla.</b>
/// </param>
public sealed record ProductCardResponse(
    string Slug,
    string Name,
    string? ShortDescription,
    string? PrimaryImageUrl,
    decimal? Price,
    bool PriceVaries);

/// <summary>Una imagen de la galería, en la ficha pública.</summary>
/// <param name="Url">Ruta pública del archivo.</param>
/// <param name="AltText">Texto alternativo, para accesibilidad.</param>
/// <param name="IsPrimary">Si es la imagen de la tarjeta.</param>
public sealed record ProductImageResponse(string Url, string? AltText, bool IsPrimary);

/// <summary>Una variante disponible, en la ficha pública.</summary>
/// <param name="VariantValue">Nulo si el producto no tiene más que su variante única.</param>
/// <param name="Code">Código visible del negocio, si tiene.</param>
/// <param name="Barcode">Código de barras, si tiene.</param>
/// <param name="Price">Ya resuelto (regla 5).</param>
/// <param name="ImageUrl">Imagen propia de la variante, si tiene.</param>
public sealed record ProductVariantResponse(
    string? VariantValue,
    string? Code,
    string? Barcode,
    decimal? Price,
    string? ImageUrl);

/// <summary>Ficha completa de un producto, para la web pública.</summary>
/// <param name="Slug">Para la URL pública.</param>
/// <param name="Name">Nombre del producto.</param>
/// <param name="ShortDescription">Una o dos líneas, para la tarjeta.</param>
/// <param name="Description">Ficha completa.</param>
/// <param name="BrandName">Marca, si tiene.</param>
/// <param name="BrandSlug">Slug de la marca, si tiene.</param>
/// <param name="Breadcrumb">
/// Vacía si ninguna de las categorías del producto está activa: nunca un
/// enlace a algo invisible.
/// </param>
/// <param name="Images">Galería, en el orden de presentación.</param>
/// <param name="Variants">Sus variantes activas, con el precio ya resuelto.</param>
/// <param name="SaleUnit">Unidad de venta, texto libre.</param>
/// <param name="VariantLabel">Cómo se llama lo que varía, solo relevante con más de una variante.</param>
public sealed record ProductDetailResponse(
    string Slug,
    string Name,
    string? ShortDescription,
    string? Description,
    string? BrandName,
    string? BrandSlug,
    IReadOnlyList<BreadcrumbItemResponse> Breadcrumb,
    IReadOnlyList<ProductImageResponse> Images,
    IReadOnlyList<ProductVariantResponse> Variants,
    string? SaleUnit,
    string? VariantLabel);

/// <summary>Un producto en el listado de administración.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Name">Nombre del producto.</param>
/// <param name="Slug">Para la URL pública.</param>
/// <param name="BrandName">Marca, si tiene.</param>
/// <param name="ListPrice">Precio de lista.</param>
/// <param name="IsPublic">Si aparece en la web pública.</param>
/// <param name="IsActive">Baja lógica.</param>
public sealed record ProductAdminListItemResponse(
    Guid Id,
    string Name,
    string Slug,
    string? BrandName,
    decimal? ListPrice,
    bool IsPublic,
    bool IsActive);

/// <summary>Una imagen de la galería, vista desde la administración.</summary>
/// <param name="Id">Identificador de la asociación.</param>
/// <param name="MediaAssetId">Archivo, en <c>core.media_assets</c>.</param>
/// <param name="Url">Ruta pública del archivo.</param>
/// <param name="AltText">Texto alternativo, para accesibilidad.</param>
/// <param name="SortOrder">Orden en la galería.</param>
/// <param name="IsPrimary">Si es la imagen de la tarjeta.</param>
public sealed record ProductImageAdminResponse(
    Guid Id,
    Guid MediaAssetId,
    string Url,
    string? AltText,
    int SortOrder,
    bool IsPrimary);

/// <summary>Ficha completa de un producto, para la administración.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Name">Nombre del producto.</param>
/// <param name="Slug">Para la URL pública.</param>
/// <param name="ShortDescription">Una o dos líneas, para la tarjeta.</param>
/// <param name="Description">Ficha completa.</param>
/// <param name="PrimaryCategoryId">La que da la ruta y la miga de pan.</param>
/// <param name="BrandId">Marca, si tiene.</param>
/// <param name="ListPrice">Precio de lista compartido.</param>
/// <param name="SaleUnit">Unidad de venta, texto libre.</param>
/// <param name="VariantLabel">Cómo se llama lo que varía.</param>
/// <param name="IsPublic">Si aparece en la web pública.</param>
/// <param name="IsActive">Baja lógica.</param>
/// <param name="CategoryIds">Todas las categorías a las que pertenece.</param>
/// <param name="Items">
/// Sus variantes. Siempre al menos una: la única, con <c>variantValue</c>
/// nulo, cuando todavía no tiene más. La interfaz decide cuándo mostrar la
/// palabra «variante» a partir de <c>Items.Count</c>; el API no la esconde.
/// </param>
/// <param name="Images">Galería, en el orden de presentación.</param>
public sealed record ProductAdminResponse(
    Guid Id,
    string Name,
    string Slug,
    string? ShortDescription,
    string? Description,
    Guid? PrimaryCategoryId,
    Guid? BrandId,
    decimal? ListPrice,
    string? SaleUnit,
    string? VariantLabel,
    bool IsPublic,
    bool IsActive,
    IReadOnlyList<Guid> CategoryIds,
    IReadOnlyList<ProductItemResponse> Items,
    IReadOnlyList<ProductImageAdminResponse> Images);

/// <summary>
/// Da de alta un producto, con su variante única (regla 2). Quien llama nunca
/// menciona una variante: si el producto necesita código o código de barras,
/// van aquí y el servicio los coloca en la variante que crea solo.
/// </summary>
/// <param name="Name">Obligatorio.</param>
/// <param name="Slug">Opcional: si falta, se genera del nombre.</param>
/// <param name="ShortDescription">Opcional.</param>
/// <param name="Description">Opcional.</param>
/// <param name="PrimaryCategoryId">
/// Opcional. Si se indica, tiene que estar también en <paramref name="CategoryIds"/> (regla 6).
/// </param>
/// <param name="CategoryIds">Categorías del producto.</param>
/// <param name="BrandId">Opcional.</param>
/// <param name="ListPrice">Opcional: nulo es «consultar precio».</param>
/// <param name="SaleUnit">Texto libre, opcional.</param>
/// <param name="VariantLabel">Cómo se llama lo que varía. Solo tiene efecto visible cuando haya una segunda variante.</param>
/// <param name="Code">Código del negocio, para la variante única que se crea con el producto.</param>
/// <param name="Barcode">Código de barras, para la variante única.</param>
public sealed record CreateProductRequest(
    string? Name,
    string? Slug,
    string? ShortDescription,
    string? Description,
    Guid? PrimaryCategoryId,
    IReadOnlyList<Guid>? CategoryIds,
    Guid? BrandId,
    decimal? ListPrice,
    string? SaleUnit,
    string? VariantLabel,
    string? Code,
    string? Barcode);

/// <summary>
/// Modifica los datos del producto. El slug no se recalcula del nombre
/// (regla 3); categorías e imágenes tienen sus propios endpoints, y el
/// código/código de barras se editan a través del ítem correspondiente.
/// </summary>
/// <param name="Name">Obligatorio.</param>
/// <param name="Slug">Obligatorio, tal cual se envía.</param>
/// <param name="ShortDescription">Opcional.</param>
/// <param name="Description">Opcional.</param>
/// <param name="BrandId">Opcional.</param>
/// <param name="ListPrice">Opcional: nulo es «consultar precio».</param>
/// <param name="SaleUnit">Texto libre, opcional.</param>
/// <param name="VariantLabel">Cómo se llama lo que varía.</param>
/// <param name="IsPublic">Si aparece en la web pública.</param>
/// <param name="IsActive">Baja lógica.</param>
/// <param name="Code">
/// Código de la **variante única**, cuando el producto tiene exactamente una.
/// <para>
/// Existe para que editar un producto corriente sea **una sola petición
/// atómica**, igual que darlo de alta. Antes el alta aceptaba estos campos y
/// la edición no, así que la interfaz tenía que hacer dos peticiones y un
/// choque de código dejaba media edición aplicada.
/// </para>
/// <para>
/// **Con más de una variante se rechaza**, no se ignora: entonces estos
/// campos ya no son del producto sino de cada presentación, y aplicarlos a
/// una al azar —o descartarlos en silencio— es cómo se pierde una edición sin
/// que nadie se entere.
/// </para>
/// </param>
/// <param name="Barcode">Código de barras de la variante única. Mismas reglas que <paramref name="Code"/>.</param>
/// <param name="SingleVariantFieldsPresent">
/// Si quien llama pretende editar los campos de la variante única. Distingue
/// «no los mando» de «los mando vacíos para borrarlos», que son cosas
/// distintas y con `null` a secas se confundirían.
/// </param>
public sealed record UpdateProductRequest(
    string? Name,
    string? Slug,
    string? ShortDescription,
    string? Description,
    Guid? BrandId,
    decimal? ListPrice,
    string? SaleUnit,
    string? VariantLabel,
    bool IsPublic,
    bool IsActive,
    string? Code = null,
    string? Barcode = null,
    bool SingleVariantFieldsPresent = false);

/// <summary>Fija el conjunto de categorías de un producto y cuál es la principal (regla 6).</summary>
/// <param name="CategoryIds">El conjunto completo; sustituye al anterior.</param>
/// <param name="PrimaryCategoryId">Tiene que estar entre <paramref name="CategoryIds"/>.</param>
public sealed record SetProductCategoriesRequest(IReadOnlyList<Guid>? CategoryIds, Guid? PrimaryCategoryId);

/// <summary>Asocia una imagen de la galería de CORE a un producto.</summary>
/// <param name="MediaAssetId">Debe existir en <c>core.media_assets</c> y estar activo.</param>
/// <param name="AltText">Opcional.</param>
/// <param name="IsPrimary">Si se marca, deja de serlo la que lo era antes (regla 11).</param>
public sealed record AssociateProductImageRequest(Guid? MediaAssetId, string? AltText, bool IsPrimary);

/// <summary>Reordena la galería y decide cuál es la principal.</summary>
/// <param name="OrderedImageIds">Todas las imágenes del producto, en el orden final.</param>
/// <param name="PrimaryImageId">Cuál pasa a ser la principal. Nulo deja la que había.</param>
public sealed record ReorderProductImagesRequest(IReadOnlyList<Guid>? OrderedImageIds, Guid? PrimaryImageId);
