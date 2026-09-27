namespace Sillar.Modules.Sales.Domain;

/// <summary>
/// Una línea del carrito: qué variante y cuántas.
/// </summary>
/// <remarks>
/// <para>
/// <b>No se replica</b>, como su carrito. Y puede referenciar
/// <c>catalog.product_items</c>, que sí se replica: la dirección <b>local →
/// replicada</b> es el segundo renglón de la tabla de la ADR-018 y está permitida
/// — la fila de origen se queda en su nodo y su destino está ahí.
/// </para>
/// <para>
/// <b>Aquí no hay precio, y es deliberado.</b> Un precio guardado en el carrito se
/// leería como precio contractual, y no lo es: la operación que crea el pedido
/// vuelve a consultar la autoridad de M01 y congela lo que ella diga. Guardarlo
/// aquí invitaría a confiar en él, que es exactamente lo que la guarda de la
/// operación existe para impedir.
/// </para>
/// </remarks>
public class CartItem
{
    /// <summary>Identidad de la fila. Local del nodo, nunca se muestra.</summary>
    public int CartItemId { get; set; }

    /// <summary>El carrito al que pertenece. FK interna del schema.</summary>
    public int CartId { get; set; }

    /// <summary>La variante elegida, tal como la identifica M01.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Cuántas unidades. <c>CHECK &gt; 0</c>.</summary>
    public int Quantity { get; set; }

    /// <summary>Cuándo se añadió.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Cuándo cambió la cantidad. La escribe el trigger.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
