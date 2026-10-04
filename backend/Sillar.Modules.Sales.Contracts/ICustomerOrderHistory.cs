namespace Sillar.Modules.Sales.Contracts;

/// <summary>
/// El historial de pedidos de un cliente, para quien tenga que mostrarlo sin
/// leer el schema <c>sales</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe porque M04 declaró el hueco antes de que M03 existiera.</b> Su SPEC
/// reserva en la ficha del cliente un espacio que rellena M03, y lo hace
/// «pidiendo el contrato al contenedor y comprobando si vino» — no preguntando al
/// registro de módulos. Si M03 no está instalado, este contrato no está registrado
/// y el hueco dice que no hay módulo de pedidos: <b>no falla, explica</b>.
/// </para>
/// <para>
/// <b>Nadie está obligado a consumirlo.</b> M03 muestra sus propios pedidos al
/// cliente en su pantalla «Mis pedidos», que es superficie suya y no espera a
/// nadie. Este contrato existe para los demás.
/// </para>
/// <para>
/// <b>Devuelve instantáneas, no entidades.</b> Quien lo consuma no puede alcanzar
/// una línea de pedido, un pago ni un carrito.
/// </para>
/// </remarks>
public interface ICustomerOrderHistory
{
    /// <summary>
    /// Los pedidos de un cliente, del más reciente al más antiguo.
    /// </summary>
    /// <remarks>
    /// Una lista vacía significa «este cliente no tiene pedidos», que es un caso
    /// normal y no un borde: la mayoría de las fichas nuevas están así.
    /// </remarks>
    /// <param name="customerId">Cliente, tal como lo identifica M04.</param>
    /// <param name="limit">
    /// Cuántos como mucho. <b>Se acota, no se obedece</b>: un valor que llega de
    /// fuera se recorta al tope del módulo y uno menor que 1 sube a 1. Mismo
    /// criterio que <c>ICatalogService.BuscarParaSeleccionAsync</c>, y por la misma
    /// razón: quien llama no tiene por qué conocer un número que no le importa.
    /// </param>
    /// <param name="cancellationToken">Cancelación.</param>
    Task<IReadOnlyList<CustomerOrderSummary>> ObtenerPedidosDeAsync(
        Guid customerId,
        int limit,
        CancellationToken cancellationToken);
}

/// <summary>Un pedido tal como se resume en una lista.</summary>
/// <param name="OrderCode">
/// El código que se dicta, con la forma <c>P-2026-0147</c>. <b>Es lo único que se
/// muestra</b>: la ADR-016, regla 2, prohíbe presentar el identificador interno.
/// </param>
/// <param name="Status">
/// Uno de los siete de <see cref="OrderStatus"/>. <b>Valor, no frase</b>: la
/// traducción al español la pone quien pinta.
/// </param>
/// <param name="TotalAmount">Lo que costó, congelado. Suma de las líneas y nada más.</param>
/// <param name="LineCount">Cuántas líneas tiene, para resumir sin traerlas.</param>
/// <param name="PlacedAt">Cuándo se creó.</param>
/// <param name="PaymentDueAt">
/// Cuándo vence el plazo para pagar. <b>No es una reserva</b>, y quien lo muestre
/// no puede decir que haya mercancía apartada.
/// </param>
public sealed record CustomerOrderSummary(
    string OrderCode,
    string Status,
    decimal TotalAmount,
    int LineCount,
    DateTimeOffset PlacedAt,
    DateTimeOffset PaymentDueAt);
