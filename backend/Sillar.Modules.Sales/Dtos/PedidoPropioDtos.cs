namespace Sillar.Modules.Sales.Dtos;

/// <summary>
/// Una línea de un pedido, tal como el cliente la ve.
/// </summary>
/// <remarks>
/// <b>Todo congelado.</b> No se recompone con el catálogo de hoy: el pedido conserva
/// qué se compró y a qué precio, y un cambio posterior en M01 no lo reescribe.
/// </remarks>
/// <param name="ProductName">Nombre del producto en el momento de la compra.</param>
/// <param name="VariantValue">La variante, o nulo si el producto tenía una sola.</param>
/// <param name="SaleUnit">Unidad de venta, si tenía.</param>
/// <param name="Quantity">Cuántas unidades.</param>
/// <param name="UnitPrice">Precio unitario congelado.</param>
/// <param name="Subtotal">Cantidad por precio. Derivado, no guardado.</param>
public sealed record LineaDePedidoPropio(
    string ProductName,
    string? VariantValue,
    string? SaleUnit,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

/// <summary>
/// El detalle de un pedido propio.
/// </summary>
/// <remarks>
/// <b>No lleva ningún identificador interno.</b> La ADR-016, regla 2, lo prohíbe: lo
/// que el cliente ve es <c>OrderCode</c>, y ni <c>order_id</c> ni <c>customer_id</c>
/// ni <c>item_id</c> salen por aquí.
/// </remarks>
/// <param name="OrderCode">El código que se dicta, con la forma <c>P-2026-0147</c>.</param>
/// <param name="Status">Uno de los siete. <b>Valor, no frase</b>: la traducción la pone quien pinta.</param>
/// <param name="TotalAmount">Lo que costó, congelado.</param>
/// <param name="PlacedAt">Cuándo se hizo.</param>
/// <param name="PaymentDueAt">Cuándo vence el plazo para pagar. <b>No es una reserva.</b></param>
/// <param name="Lines">Las líneas, congeladas.</param>
public sealed record PedidoPropioDetalle(
    string OrderCode,
    string Status,
    decimal TotalAmount,
    DateTimeOffset PlacedAt,
    DateTimeOffset PaymentDueAt,
    IReadOnlyList<LineaDePedidoPropio> Lines);
