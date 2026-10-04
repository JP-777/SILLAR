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
    /// Nombre del trabajador que registró el pago, congelado al actuar.
    /// </summary>
    /// <remarks>
    /// Primero de los <b>tres datos</b> de la atribución (<see cref="StaffAttribution"/>).
    /// En un pago los tres son obligatorios: un pago lo registra siempre una persona.
    /// </remarks>
    public required string RegisteredBy { get; set; }

    /// <summary>
    /// Identificador del trabajador <b>dentro de su nodo de pertenencia</b>.
    /// </summary>
    /// <remarks>
    /// <b>Dato de bitácora, no puntero.</b> No es clave foránea hacia
    /// <c>core.admin_users</c> y no se resuelve con un <c>JOIN</c>: esa tabla no se
    /// replica y esta sí, así que el 7 de un nodo no es el 7 de otro (ADR-018).
    /// Solo es único dentro del <c>admin_users</c> que lo emitió, y cuál es ese lo
    /// dice <see cref="RegisteredByAdminUserHomeNode"/>.
    /// </remarks>
    public int RegisteredByAdminUserLocalId { get; set; }

    /// <summary>
    /// Nodo al que pertenece la <b>cuenta</b> del trabajador.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tercero de los tres datos, y el que la formulación anterior no tenía. Es el
    /// universo contra el que se interpreta
    /// <see cref="RegisteredByAdminUserLocalId"/>.
    /// </para>
    /// <para>
    /// <b>NO es <see cref="OrderPayment.OriginNode"/>.</b> Aquella dice <b>dónde
    /// ocurrió la actuación</b> y la sella el <c>DbContext</c> al nacer la fila; esta
    /// dice <b>de qué nodo es la cuenta</b>. Pueden diferir —una cuenta del nodo A
    /// puede registrar un pago desde el nodo B— y el esquema lo admite: <b>no hay
    /// ningún <c>CHECK</c> que exija que coincidan</b>, porque exigirlo prohibiría un
    /// hecho real del negocio.
    /// </para>
    /// <para>
    /// <b>Y no se deriva de aquella.</b> Rellenarla con el nodo actual sería correcto
    /// solo mientras nadie atienda desde otra sucursal, y fallaría en silencio el día
    /// que ocurra.
    /// </para>
    /// </remarks>
    public required string RegisteredByAdminUserHomeNode { get; set; }

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
