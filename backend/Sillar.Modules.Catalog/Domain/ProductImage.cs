namespace Sillar.Modules.Catalog.Domain;

/// <summary>
/// Una imagen del producto.
/// </summary>
/// <remarks>
/// Las imágenes son <b>del producto</b>, no de la variante. La variante puede
/// tener una propia en <c>product_items.image_id</c> cuando el color se ve, pero
/// no lleva galería.
///
/// El archivo lo gestiona CORE: aquí solo vive la asociación. Quitar la
/// asociación no borra el archivo.
/// </remarks>
public class ProductImage : CatalogEntity
{
    /// <summary>Producto.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Archivo, en <c>core.media_assets</c>.</summary>
    public Guid MediaAssetId { get; set; }

    /// <summary>Texto alternativo, para accesibilidad.</summary>
    public string? AltText { get; set; }

    /// <summary>Orden en la galería.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Imagen de la tarjeta. <b>Máximo una por producto.</b>
    /// </summary>
    /// <remarks>
    /// Lo garantiza un índice único parcial. Si no hay ninguna marcada, se usa
    /// la de menor <c>sort_order</c> (regla 11).
    /// </remarks>
    public bool IsPrimary { get; set; }

    /// <summary>Producto.</summary>
    public Product? Product { get; set; }
}
