namespace Sillar.Modules.Sales.Domain;

/// <summary>
/// El carrito de la tienda: lo que un cliente ha elegido y todavía no ha
/// confirmado.
/// </summary>
/// <remarks>
/// <para>
/// <b>No se replica.</b> La ADR-017 coloca «carrito y sesiones de compra» en el
/// lado exclusivo de SILLAR WEB: un carrito a medias no tiene nada que hacer en el
/// mostrador de otro nodo. Clave <c>integer GENERATED ALWAYS AS IDENTITY</c>.
/// </para>
/// <para>
/// <b>Un carrito no es un pedido, y esa distinción tiene una consecuencia
/// concreta:</b> el carrito <b>no consume número de la serie</b>. El código visible
/// se pide al confirmar, justo antes de persistir el pedido — nunca aquí. Pedirlo
/// al abrir el carrito gastaría un número en cada carrito abandonado, y los
/// abandonados son la mayoría.
/// </para>
/// <para>
/// <b>Referencia a <c>crm.customers</c> sin clave foránea cruzada.</b> Esta tabla
/// no se replica y <c>crm.customers</c> sí; la dirección local → replicada está
/// permitida por la ADR-018, pero el carrito es efímero y no necesita que la base
/// le garantice nada sobre un cliente: quien lo lee ya tiene la sesión delante.
/// </para>
/// </remarks>
public class Cart
{
    /// <summary>Identidad de la fila. Local del nodo, nunca se muestra.</summary>
    public int CartId { get; set; }

    /// <summary>De quién es el carrito.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Cuándo se creó.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Cuándo se tocó por última vez. La escribe el trigger.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
