namespace Sillar.Modules.Sales.Contracts.Events;

/// <summary>Se creó un pedido.</summary>
/// <param name="OrderId">Pedido creado.</param>
/// <param name="OrderCode">Su código visible, para que un consumidor no tenga que resolverlo.</param>
/// <param name="CustomerId">Quién lo hizo.</param>
/// <param name="OccurredAt">Cuándo ocurrió.</param>
public sealed record PedidoCreado(
    Guid OrderId,
    string OrderCode,
    Guid CustomerId,
    DateTimeOffset OccurredAt);

/// <summary>
/// Una persona confirmó manualmente un pago.
/// </summary>
/// <remarks>
/// <b>Es el hecho de pago, no el estado del pedido.</b> Son dos cosas distintas y
/// este evento anuncia solo la primera: fundirlas obligaría a rehacer la máquina
/// de estados con pedidos reales dentro el día que entre la pasarela.
/// </remarks>
/// <param name="OrderId">Pedido cobrado.</param>
/// <param name="OrderCode">Su código visible.</param>
/// <param name="OccurredAt">Cuándo se registró la confirmación.</param>
public sealed record PagoConfirmado(
    Guid OrderId,
    string OrderCode,
    DateTimeOffset OccurredAt);

/// <summary>
/// Venció el plazo para pagar de un pedido.
/// </summary>
/// <remarks>
/// <b>No se libera nada, porque nada se apartó.</b> M09 Inventario pasó a SILLAR
/// ERP y SILLAR WEB v1 no reserva existencias: el plazo es para pagar. El pedido
/// pasa a <c>expired</c>, deja de estar garantizado y <b>no se cancela por este
/// hecho</b>.
/// </remarks>
/// <param name="OrderId">Pedido cuyo plazo venció.</param>
/// <param name="OrderCode">Su código visible.</param>
/// <param name="OccurredAt">Cuándo se registró el vencimiento.</param>
public sealed record PlazoDePagoVencido(
    Guid OrderId,
    string OrderCode,
    DateTimeOffset OccurredAt);
