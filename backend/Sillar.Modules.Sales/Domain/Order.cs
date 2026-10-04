using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Domain;

/// <summary>
/// Un pedido: el documento comercial de una compra en línea.
/// </summary>
/// <remarks>
/// <para>
/// <b>Se replica.</b> La ADR-016 enumera «Ventas y sus líneas» entre las filas
/// replicables, así que la clave es <c>uuid</c> v7 generada por la aplicación
/// (regla 1): un nodo sin conexión tiene que poder crear la fila entera antes de
/// hablar con nadie.
/// </para>
/// <para>
/// <b>Lo que guarda son instantáneas, no referencias vivas.</b> El nombre y el
/// documento del cliente se copian en el momento de la compra; una edición
/// posterior de su ficha no reescribe lo que ocurrió. Es lo que permite que el
/// pedido siga siendo legible después de <c>DROP SCHEMA crm CASCADE</c>.
/// </para>
/// <para>
/// <b>No lleva dirección.</b> SILLAR WEB v1 es solo recojo en tienda: no hay
/// entrega a domicilio ni tarifa de envío, así que no hay a dónde enviar que
/// congelar.
/// </para>
/// <para>
/// <b>No lleva ninguna clave foránea a <c>core.admin_users</c>.</b> Quién cobró y
/// quién cambió el estado se guardan como <b>nombre</b> en sus propias tablas:
/// esa tabla no se replica y esta sí, y la ADR-018 prohíbe cruzar esa línea. El
/// dato que hace falta dentro de un año es quién actuó, y sobrevive a que la
/// cuenta se dé de baja o se renombre.
/// </para>
/// </remarks>
public class Order : IReplicatedEntity
{
    /// <summary>Identificador interno. <b>Nunca se muestra</b> (ADR-016, regla 2).</summary>
    public Guid OrderId { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// El código que se dicta por teléfono, con la forma <c>P-2026-0147</c>:
    /// etiqueta del nodo, año y correlativo.
    /// </summary>
    /// <remarks>
    /// Columna aparte de la clave y con <c>UNIQUE</c>: son dos columnas con dos
    /// oficios, y esa separación es lo que permite que la clave sea fea y el
    /// número del pedido sea legible. Lo asigna <c>OrderCodeAllocator</c> al
    /// confirmar, nunca al crear el carrito.
    /// </remarks>
    public required string OrderCode { get; set; }

    /// <summary>
    /// Quién compró. Clave foránea cruzada hacia <c>crm.customers</c>.
    /// </summary>
    /// <remarks>
    /// Permitida porque M04 es <b>dependencia dura</b> desde el 21 de agosto de
    /// 2026 —la cuenta obligatoria para comprar la convirtió en dura—, así que la
    /// FK va en la migración de M03 y no en un script de integración. Y las dos
    /// tablas se replican, que es lo que la ADR-018 exige comprobar.
    /// </remarks>
    public Guid CustomerId { get; set; }

    /// <summary>Nombre del cliente en el momento de la compra. Instantánea.</summary>
    public required string CustomerFullName { get; set; }

    /// <summary>Correo del cliente en el momento de la compra. Instantánea.</summary>
    public required string CustomerEmail { get; set; }

    /// <summary>Teléfono del cliente en el momento de la compra, si lo tenía.</summary>
    public string? CustomerPhone { get; set; }

    /// <summary>
    /// Tipo de documento en el momento de la compra: hoy <c>dni</c> o <c>ruc</c>.
    /// </summary>
    /// <remarks>
    /// <b>Sin <c>CHECK</c> que replique la lista de M04.</b> Esa restricción es
    /// suya (<c>ck_customers_document_type</c>), y copiarla aquí ataría el
    /// historial de pedidos a una regla ajena: el día que M04 admita un tercer
    /// tipo, un pedido válido no se podría guardar. Una instantánea guarda lo que
    /// había, no lo que hoy es válido.
    /// </remarks>
    public string? CustomerDocumentType { get; set; }

    /// <summary>Número de documento en el momento de la compra. Instantánea.</summary>
    public string? CustomerDocumentNumber { get; set; }

    /// <summary>
    /// Uno de los siete de <c>OrderStatus</c>. Columna <c>text</c> con <c>CHECK</c>.
    /// </summary>
    /// <remarks>
    /// <b>No una clave foránea a una tabla de catálogo de estados.</b> Una tabla
    /// así llevaría clave entera y no se replicaría, y este pedido sí: es el
    /// tercer renglón de la ADR-018 —la fila viaja y la referencia se queda— y
    /// <b>no da ningún error</b>, porque cada base queda coherente por dentro.
    /// Con siete estados fijos, un <c>CHECK</c> da lo mismo sin cruzar la línea.
    /// </remarks>
    public required string Status { get; set; }

    /// <summary>
    /// Cuándo vence el plazo para pagar.
    /// </summary>
    /// <remarks>
    /// <b>No es una reserva y ninguna pantalla puede llamarla así.</b> M09
    /// Inventario pasó a SILLAR ERP, así que no hay existencia que apartar: son
    /// 48 horas naturales por defecto —configurables por instalación— para que el
    /// cliente pague. Al vencer, el pedido deja de estar garantizado y <b>no se
    /// cancela por ese hecho</b>.
    /// </remarks>
    public DateTimeOffset PaymentDueAt { get; set; }

    /// <summary>Lo que cuesta el pedido: la suma de sus líneas y nada más.</summary>
    /// <remarks>
    /// Sin costo de entrega ni tarifas: solo recojo en tienda. Se congela al
    /// confirmar y no se recalcula con el catálogo de hoy.
    /// </remarks>
    public decimal TotalAmount { get; set; }

    /// <summary>Baja lógica. Nunca <c>DELETE</c> físico en una tabla de negocio.</summary>
    public bool IsActive { get; set; } = true;

    /// <inheritdoc />
    public string OriginNode { get; set; } = string.Empty;

    /// <inheritdoc />
    public long RowVersion { get; set; } = 1;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
