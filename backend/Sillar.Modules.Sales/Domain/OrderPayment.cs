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
    /// <b>El nombre</b> de quien lo registró. Jamás una clave foránea.
    /// </summary>
    /// <remarks>
    /// <c>core.admin_users</c> no se replica y esta tabla sí: la ADR-018 prohíbe
    /// que una fila que viaja referencie a una que se queda, y no avisa cuando se
    /// incumple. Es además el precedente ya escrito de <c>b2b.quotes</c>, y por la
    /// misma razón de fondo: el dato que hace falta dentro de un año es
    /// <b>quién cobró</b>, y eso sobrevive a que la cuenta se dé de baja o se
    /// renombre.
    /// </remarks>
    public required string RegisteredBy { get; set; }

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
