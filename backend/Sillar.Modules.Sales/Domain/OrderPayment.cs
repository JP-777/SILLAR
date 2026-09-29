using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Domain;

/// <summary>
/// Un pago registrado sobre un pedido. <b>Un hecho consumado, no un estado.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>El hecho de pago y el estado del pedido son dos cosas distintas</b>, y esta
/// tabla existe para que lo sigan siendo. Fundirlos obligaría a rehacer la máquina
/// de estados <b>con pedidos reales dentro</b> el día que entre la pasarela — y es
/// el mismo defecto que M04 corrigió con «de baja» y «bloqueada»: un estado que
/// carga dos significados obliga a elegir el peor comportamiento para los dos.
/// </para>
/// <para>
/// <b>El personal siempre puede registrar un pago, aunque el pedido esté
/// vencido.</b> Un pago tardío nunca se rechaza por el estado del pedido; lo que
/// no puede ocurrir es que se acepte sin dejar un resultado operativo o un
/// pendiente visible.
/// </para>
/// <para>
/// <b>Se replica</b>, como el pedido: quién cobró y cuánto es parte del registro
/// comercial y tiene que viajar con él.
/// </para>
/// </remarks>
public class OrderPayment : IReplicatedEntity
{
    /// <summary>Identificador interno. Nunca se muestra.</summary>
    public Guid OrderPaymentId { get; set; } = Guid.CreateVersion7();

    /// <summary>El pedido cobrado. FK interna del schema.</summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Cómo se cobró. Hoy solo <c>yape</c>.
    /// </summary>
    /// <remarks>
    /// El <c>CHECK</c> contiene solo lo ratificado. El efectivo que el registro
    /// histórico menciona <b>no está autorizado ni eliminado</b>: está escalado, y
    /// añadir un valor después es una migración barata mientras quitar uno con
    /// filas dentro no lo es.
    /// </remarks>
    public required string Method { get; set; }

    /// <summary>Importe cobrado. <c>CHECK &gt;= 0</c>.</summary>
    public decimal Amount { get; set; }

    /// <summary>Código de operación que el cliente dio, si lo hay.</summary>
    public string? Reference { get; set; }

    /// <summary>
    /// <b>El nombre</b> de quien lo registró, congelado en el momento de actuar.
    /// </summary>
    /// <remarks>
    /// Es la mitad legible de la atribución. Sobrevive a que la cuenta se dé de
    /// baja o se renombre, que es justo el dato que hace falta dentro de un año:
    /// <b>quién cobró</b>.
    /// </remarks>
    public required string RegisteredBy { get; set; }

    /// <summary>
    /// Identificador del trabajador <b>en el nodo donde actuó</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dato de bitácora, no puntero.</b> No es una clave foránea hacia
    /// <c>core.admin_users</c> y no se resuelve con un <c>JOIN</c>: esa tabla no se
    /// replica y esta sí, así que el 7 de este nodo no es el 7 de otro. La ADR-018
    /// prohíbe cruzar esa línea, y no avisa cuando se incumple.
    /// </para>
    /// <para>
    /// <b>Solo significa algo junto a la identidad del nodo donde se actuó</b>, y
    /// ese nodo es el <c>OriginNode</c> <b>de esta misma fila</b> —no el del
    /// pedido—: esta tabla se replica, así que el sello se pone donde la fila nace,
    /// que es donde estaba la persona. Un pago de un pedido nacido en otro nodo
    /// lleva su propio origen, y por eso el par es interpretable sin columna nueva.
    /// </para>
    /// <para>
    /// El sufijo <c>Origin</c> está en el nombre a propósito: sin él, el primero que
    /// lo vea escribirá el <c>JOIN</c> y funcionará —en un solo nodo, que es lo peor
    /// que puede pasar.
    /// </para>
    /// </remarks>
    public int RegisteredByAdminUserIdOrigin { get; set; }

    /// <summary>Cuándo se registró el pago.</summary>
    public DateTimeOffset RegisteredAt { get; set; }

    /// <summary>
    /// Si se registró después de vencer el plazo.
    /// </summary>
    /// <remarks>
    /// Se guarda como hecho, no se deduce comparando fechas más tarde: el plazo
    /// del pedido puede haber sido otro cuando esto ocurrió. Es lo que permite
    /// encontrar los pagos tardíos sin recalcular nada.
    /// </remarks>
    public bool WasLate { get; set; }

    /// <inheritdoc />
    public string OriginNode { get; set; } = string.Empty;

    /// <inheritdoc />
    public long RowVersion { get; set; } = 1;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
