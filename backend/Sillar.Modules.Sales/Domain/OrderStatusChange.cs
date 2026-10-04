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
    /// Nombre del trabajador que lo cambió, congelado, o nulo si lo hizo el sistema.
    /// </summary>
    public string? ChangedBy { get; set; }

    /// <summary>
    /// Identificador del trabajador dentro de su nodo de pertenencia, o nulo si lo
    /// hizo el sistema.
    /// </summary>
    /// <remarks>
    /// <b>Dato de bitácora, no puntero.</b> Sin FK a <c>core.admin_users</c> y sin
    /// <c>JOIN</c>. Se interpreta contra
    /// <see cref="ChangedByAdminUserHomeNode"/>, no contra ningún otro nodo.
    /// </remarks>
    public int? ChangedByAdminUserLocalId { get; set; }

    /// <summary>
    /// Nodo al que pertenece la <b>cuenta</b> del trabajador, o nulo si lo hizo el
    /// sistema.
    /// </summary>
    /// <remarks>
    /// <b>NO es <see cref="OrderStatusChange.OriginNode"/></b>, que dice dónde
    /// ocurrió la actuación. Pueden diferir y el esquema lo admite. Tampoco se
    /// deriva de él, ni del <c>origin_node</c> del pedido, que no interviene en
    /// absoluto.
    /// </remarks>
    public string? ChangedByAdminUserHomeNode { get; set; }

    /// <summary>
    /// La atribución como unidad, o <c>null</c> si la actuación fue del sistema.
    /// </summary>
    /// <remarks>
    /// Los tres datos van juntos o no van. Esta propiedad los lee como lo que son
    /// —una sola cosa— y falla si están unos y faltan otros: media atribución es
    /// peor que ninguna, porque parece completa.
    /// </remarks>
    public StaffAttribution? Atribucion()
        => StaffAttribution.DeOpcional(
            ChangedBy, ChangedByAdminUserLocalId, ChangedByAdminUserHomeNode);

    /// <inheritdoc />
    public string OriginNode { get; set; } = string.Empty;

    /// <inheritdoc />
    public long RowVersion { get; set; } = 1;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
