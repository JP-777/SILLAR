namespace Sillar.Modules.Sales.Dtos;

/// <summary>
/// Una línea pedida por el cliente: <b>qué variante y cuántas. Nada más.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>No hay precio aquí, y es la decisión más importante de este tipo.</b> R-09 dice
/// que un total o un precio recibido del navegador es dato no confiable; la forma más
/// fuerte de cumplirlo no es validarlo, es <b>no tener dónde ponerlo</b>. Un campo de
/// precio que se ignora sigue invitando a confiar en él el día que alguien lo lea por
/// comodidad.
/// </para>
/// <para>
/// Si el navegador envía un <c>unitPrice</c> de más, el deserializador lo descarta sin
/// que nadie tenga que acordarse de descartarlo.
/// </para>
/// </remarks>
/// <param name="ItemId">La variante, tal como M01 la identifica.</param>
/// <param name="Quantity">Cuántas unidades. Positiva.</param>
public sealed record LineaPedida(Guid ItemId, int Quantity);

/// <summary>Lo que el cliente envía para crear un pedido.</summary>
/// <remarks>
/// <b>Tampoco lleva total, ni cliente, ni dirección.</b> El cliente sale de la sesión;
/// el total lo calcula el servidor con los precios de M01; y no hay dirección porque
/// SILLAR WEB v1 es solo recojo en tienda.
/// </remarks>
/// <param name="Lines">Las líneas pedidas.</param>
public sealed record CrearPedidoPeticion(IReadOnlyList<LineaPedida> Lines);

/// <summary>Lo que se devuelve al crear un pedido.</summary>
/// <remarks>
/// <b>Solo el código visible y lo que el cliente necesita saber ahora.</b> Ningún
/// identificador interno: la ADR-016, regla 2, lo prohíbe.
/// </remarks>
/// <param name="OrderCode">El código que se dicta, con la forma <c>P-2026-0147</c>.</param>
/// <param name="Status">Estado inicial: pendiente de pago.</param>
/// <param name="TotalAmount">Lo que cuesta, calculado por el servidor.</param>
/// <param name="PaymentDueAt">Cuándo vence el plazo para pagar. <b>No es una reserva.</b></param>
public sealed record PedidoCreadoRespuesta(
    string OrderCode,
    string Status,
    decimal TotalAmount,
    DateTimeOffset PaymentDueAt);
