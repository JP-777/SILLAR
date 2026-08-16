namespace Sillar.Modules.Catalog.Domain;

/// <summary>
/// Producto: lo que <b>comparten</b> todas sus variantes.
/// </summary>
/// <remarks>
/// Identidad, presentación y precio se escriben una vez aquí. Lo que varía entre
/// presentaciones —color, tamaño, código— vive en <see cref="ProductItem"/>.
///
/// El producto <b>no es</b> el nivel que se cuenta ni el que se cobra. M09
/// cuenta contra las variantes y M13 vende contra ellas (SPEC §4.2). Si el verde
/// y el azul fueran un solo número, la tienda diría que hay tres cuando quedan
/// tres verdes y ningún azul, que es prometer de más.
/// </remarks>
public class Product : CatalogEntity
{
    /// <summary>
    /// Producto + característica + marca + presentación, según el diccionario.
    /// </summary>
    /// <remarks>
    /// Colación <c>es_search</c>: la búsqueda pública ignora mayúsculas y
    /// tildes, así que <i>lapiz</i> encuentra <i>LÁPIZ</i>.
    /// </remarks>
    public required string Name { get; set; }

    /// <summary>
    /// URL pública.
    /// </summary>
    /// <remarks>
    /// Se genera del nombre al crear y se puede corregir a mano, pero
    /// <b>nunca cambia solo</b> al editar el nombre (regla 3): cambiarlo rompe
    /// los enlaces que ya se compartieron por WhatsApp, que es como se comparte
    /// un producto aquí.
    /// </remarks>
    public required string Slug { get; set; }

    /// <summary>Una o dos líneas, para la tarjeta.</summary>
    public string? ShortDescription { get; set; }

    /// <summary>Ficha completa.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// La categoría que da la ruta pública y la miga de pan.
    /// </summary>
    /// <remarks>
    /// Tiene que estar también en <c>product_categories</c>. Se garantiza en la
    /// aplicación y no con un <c>CHECK</c>: una restricción que mira otra tabla
    /// exige un disparador, y un disparador que se salta en una carga masiva
    /// miente (SPEC §6.5).
    /// </remarks>
    public Guid? PrimaryCategoryId { get; set; }

    /// <summary>Marca.</summary>
    public Guid? BrandId { get; set; }

    /// <summary>
    /// Precio de lista compartido por las variantes.
    /// </summary>
    /// <remarks>
    /// Nulo significa «consultar precio», que es real en pedidos B2B y en el
    /// catálogo de exhibición sin venta. <b>Cero no es lo mismo que nulo: cero
    /// es gratis</b>, y la interfaz no los confunde (regla 5).
    /// </remarks>
    public decimal? ListPrice { get; set; }

    /// <summary>
    /// Unidad de venta: <c>unidad</c>, <c>plato</c>, <c>porción</c>, <c>millar</c>.
    /// </summary>
    /// <remarks>
    /// Texto libre y no una lista cerrada. Un plato no se vende «por unidad», y
    /// una lista cerrada dejaría fuera a los restaurantes y a los servicios.
    /// </remarks>
    public string? SaleUnit { get; set; }

    /// <summary>
    /// Cómo se llama lo que varía: <c>Color</c>, <c>Tamaño</c>, <c>Sabor</c>.
    /// </summary>
    /// <remarks>
    /// Solo se usa cuando hay más de una variante. Mientras haya una sola, la
    /// interfaz no muestra la palabra «variante» en ninguna parte.
    /// </remarks>
    public string? VariantLabel { get; set; }

    /// <summary>Si aparece en la web pública.</summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>Baja lógica.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Categoría principal.</summary>
    public Category? PrimaryCategory { get; set; }

    /// <summary>Marca.</summary>
    public Brand? Brand { get; set; }

    /// <summary>
    /// Variantes. <b>Siempre al menos una</b>, creada con el producto y con
    /// <c>variant_value</c> nulo (regla 2).
    /// </summary>
    public ICollection<ProductItem> Items { get; set; } = [];

    /// <summary>Categorías a las que pertenece.</summary>
    public ICollection<ProductCategory> Categories { get; set; } = [];

    /// <summary>Galería.</summary>
    public ICollection<ProductImage> Images { get; set; } = [];
}
