using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Domain;

/// <summary>
/// Una línea de pedido: una variante comprada, con su precio congelado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Se vende contra la variante, no contra el producto.</b> El contrato de M01
/// no deja margen: «Identificador de la variante, no del producto: quien vende,
/// cuenta o factura lo hace contra ella». <see cref="ProductId"/> viaja solo para
/// agrupar en informes, y por eso <b>no lleva clave foránea</b>.
/// </para>
/// <para>
/// <b>Se replica</b>, igual que su pedido, y guarda el <c>ItemSnapshot</c> de M01
/// entero: un cambio posterior del catálogo no reescribe lo que se compró.
/// </para>
/// </remarks>
public class OrderLine : IReplicatedEntity
{
    /// <summary>Identificador interno. Nunca se muestra.</summary>
    public Guid OrderLineId { get; set; } = Guid.CreateVersion7();

    /// <summary>El pedido al que pertenece. FK interna del schema.</summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// La variante comprada. Clave foránea cruzada hacia
    /// <c>catalog.product_items</c>.
    /// </summary>
    /// <remarks>
    /// Permitida: M01 es dependencia dura y las dos tablas se replican. Y protege
    /// en la dirección correcta — intentar eliminar una variante con ventas
    /// activas falla con un error explícito, que es el comportamiento que se
    /// quiere.
    /// </remarks>
    public Guid ItemId { get; set; }

    /// <summary>
    /// El producto al que pertenece la variante. <b>Sin clave foránea.</b>
    /// </summary>
    /// <remarks>
    /// Solo para agrupar en informes: nada vende ni cuenta contra él. Ponerle una
    /// FK sugeriría que el pedido depende del producto agregado, que es
    /// justamente el error de nivel del que este proyecto se salvó una vez.
    /// </remarks>
    public Guid ProductId { get; set; }

    /// <summary>Nombre del producto en el momento de la compra. Instantánea.</summary>
    public required string ProductName { get; set; }

    /// <summary>
    /// Lo que distinguía a la variante, por ejemplo «Verde». Nulo si el producto
    /// no tenía más que su variante única.
    /// </summary>
    public string? VariantValue { get; set; }

    /// <summary>Unidad de venta en el momento de la compra, si tenía.</summary>
    public string? SaleUnit { get; set; }

    /// <summary>Cuántas unidades. <c>CHECK &gt; 0</c>: una línea de cero no es una línea.</summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Precio unitario congelado en el momento de la operación.
    /// </summary>
    /// <remarks>
    /// <b>Lo pone la operación consultando a M01, nunca el navegador.</b> Y
    /// <c>CHECK &gt;= 0</c> admite el cero porque <b>cero es gratis y se vende</b>:
    /// lo que no entra al pedido es el precio nulo, que significa «a consultar» y
    /// ni siquiera llega a ser una línea.
    /// </remarks>
    public decimal UnitPrice { get; set; }

    /// <inheritdoc />
    public string OriginNode { get; set; } = string.Empty;

    /// <inheritdoc />
    public long RowVersion { get; set; } = 1;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
