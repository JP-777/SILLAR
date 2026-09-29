using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Domain;

/// <summary>
/// Un cambio de estado de un pedido: de cuál a cuál, cuándo y por quién.
/// </summary>
/// <remarks>
/// <para>
/// <b>Se replica</b>, como el pedido. Y guarda <b>el nombre</b> de quien actuó,
/// jamás una clave foránea a <c>core.admin_users</c>: es la misma regla que el
/// pago, extendida aquí por analogía directa y no como decisión nueva — la ADR-018
/// obliga igual en los dos casos.
/// </para>
/// <para>
/// <b><see cref="ChangedBy"/> admite nulo, y no es un descuido.</b> El
/// vencimiento del plazo lo provoca el tiempo, no una persona: una fila con
/// nombre vacío diría que alguien lo hizo, y una con nombre inventado sería peor.
/// Nulo significa exactamente «lo hizo el sistema».
/// </para>
/// </remarks>
public class OrderStatusChange : IReplicatedEntity
{
    /// <summary>Identificador interno. Nunca se muestra.</summary>
    public Guid OrderStatusChangeId { get; set; } = Guid.CreateVersion7();

    /// <summary>El pedido que cambió. FK interna del schema.</summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Estado anterior, o nulo en el primer asiento — cuando el pedido nace.
    /// </summary>
    public string? FromStatus { get; set; }

    /// <summary>Estado nuevo. Uno de los siete.</summary>
    public required string ToStatus { get; set; }

    /// <summary>Cuándo ocurrió.</summary>
    public DateTimeOffset ChangedAt { get; set; }

    /// <summary>
    /// <b>El nombre</b> de quien lo cambió, congelado, o nulo si lo hizo el sistema.
    /// </summary>
    public string? ChangedBy { get; set; }

    /// <summary>
    /// Identificador del trabajador <b>en el nodo donde actuó</b>, o nulo si lo hizo
    /// el sistema.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dato de bitácora, no puntero.</b> No es clave foránea hacia
    /// <c>core.admin_users</c> y no se resuelve con un <c>JOIN</c>. Se interpreta
    /// junto al <c>OriginNode</c> <b>de esta misma fila</b>, que es donde estaba la
    /// persona — no junto al del pedido, que puede ser otro.
    /// </para>
    /// <para>
    /// <b>Va con <see cref="ChangedBy"/> o no va.</b> Las dos nulas significan «lo
    /// hizo el sistema»; las dos presentes, una persona. Media atribución es peor
    /// que ninguna, porque parece completa — y lo impone un <c>CHECK</c>, no una
    /// convención.
    /// </para>
    /// </remarks>
    public int? ChangedByAdminUserIdOrigin { get; set; }

    /// <inheritdoc />
    public string OriginNode { get; set; } = string.Empty;

    /// <inheritdoc />
    public long RowVersion { get; set; } = 1;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
