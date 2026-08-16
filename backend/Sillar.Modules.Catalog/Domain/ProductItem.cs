namespace Sillar.Modules.Catalog.Domain;

/// <summary>
/// La variante. <b>Es la unidad que se cuenta, se cobra y se factura.</b>
/// </summary>
/// <remarks>
/// M09 cuenta contra esta tabla; M13 y M03 venden contra ella. El contrato de
/// M01 habla de <c>ItemId</c>, no de <c>ProductId</c>, por esta razón.
///
/// <b>La tabla no aporta «variante»: aporta «más de una»</b> (SPEC §4.2). Todo
/// producto tiene exactamente una, creada sola y con <see cref="VariantValue"/>
/// nulo. Mientras solo haya una, la interfaz no menciona la palabra: el código y
/// el código de barras se editan en el formulario del producto, como si
/// vivieran ahí.
///
/// La pregunta que decide si algo es variante o característica de venta:
/// <i>¿puedo quedarme sin esta teniendo las otras?</i> Si sí, es variante y va
/// aquí. Si no —el color se elige al momento y no hay nada que se acabe— es un
/// dato de la línea de venta y vive en M13.
/// </remarks>
public class ProductItem : CatalogEntity
{
    /// <summary>Producto al que pertenece.</summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Lo que la distingue: <c>Verde</c>, <c>A4</c>, <c>200 hojas</c>.
    /// </summary>
    /// <remarks>
    /// Nulo solo en la variante única del producto. Colación <c>es_search</c>.
    /// </remarks>
    public string? VariantValue { get; set; }

    /// <summary>
    /// Código visible del negocio. Es lo que se teclea en caja.
    /// </summary>
    /// <remarks>
    /// Opcional: un plato no tiene ninguno y un cuaderno tiene los dos. Único
    /// entre los no nulos en toda la instalación, con colación <c>es_ci</c>.
    ///
    /// <b>La búsqueda por fragmento no puede usar <c>LIKE</c> sobre esta
    /// columna</b>: PostgreSQL no lo admite en colaciones no deterministas. Hay
    /// un índice de trigramas sobre <c>(code COLLATE "C")</c> y la consulta debe
    /// escribirse igual — ver la migración y <c>DATOS.md</c>.
    /// </remarks>
    public string? Code { get; set; }

    /// <summary>Código de barras. Opcional y único entre los no nulos.</summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// Precio propio, si esta variante no vale lo mismo que las demás.
    /// </summary>
    /// <remarks>
    /// Nulo usa el <c>list_price</c> del producto, que es el caso común: mismo
    /// precio, solo cambia el código. El campo existe para el otro caso, que
    /// también es real: el cuaderno de 100 y el de 200 hojas.
    /// </remarks>
    public decimal? PriceOverride { get; set; }

    /// <summary>Imagen propia, cuando el color se ve.</summary>
    /// <remarks>
    /// La variante no lleva galería: seis fotos por color multiplican el trabajo
    /// de quien carga el catálogo por nada.
    /// </remarks>
    public Guid? ImageId { get; set; }

    /// <summary>Orden de presentación.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Baja lógica.
    /// </summary>
    /// <remarks>
    /// No se puede desactivar la última variante activa de un producto activo:
    /// se desactiva el producto (regla 8). Lo comprueba la aplicación, porque
    /// depende del estado del producto y de sus hermanas.
    /// </remarks>
    public bool IsActive { get; set; } = true;

    /// <summary>Producto al que pertenece.</summary>
    public Product? Product { get; set; }
}
