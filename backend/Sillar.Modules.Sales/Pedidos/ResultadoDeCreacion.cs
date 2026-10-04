namespace Sillar.Modules.Sales.Pedidos;

/// <summary>
/// Por qué no se pudo crear un pedido.
/// </summary>
/// <remarks>
/// <b>Cada valor es una frase distinta en pantalla</b>, y por eso no se funden: un
/// «no se pudo crear el pedido» obligaría al cliente a adivinar si el problema es su
/// cuenta, un producto que se agotó o uno que hay que cotizar. Ninguna de las tres se
/// arregla igual.
/// </remarks>
public enum MotivoDeNoCreacion
{
    /// <summary>Ninguno: el pedido se creó.</summary>
    Ninguno = 0,

    /// <summary>La cuenta no puede comprar. M04 no dice por qué y M03 no lo inventa.</summary>
    LaCuentaNoPuedeComprar,

    /// <summary>El correo no está verificado. R-07.</summary>
    CorreoSinVerificar,

    /// <summary>El pedido llegó sin líneas. Un carrito vacío no entra en confirmación.</summary>
    SinLineas,

    /// <summary>Una cantidad no positiva. Una línea de cero no es una línea.</summary>
    CantidadNoPositiva,

    /// <summary>
    /// La variante ya no es vendible: no existe, o está de baja.
    /// </summary>
    ItemNoVendible,

    /// <summary>
    /// La variante se cotiza: su precio efectivo es <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <b>Nulo es «a consultar», no «sin precio» ni «gratis».</b> Cero sí se vende.
    /// </remarks>
    ItemAConsultar
}

/// <summary>El resultado de intentar crear un pedido.</summary>
/// <param name="OrderCode">El código visible, si se creó.</param>
/// <param name="TotalAmount">Lo que costó, si se creó.</param>
/// <param name="PaymentDueAt">Cuándo vence el plazo, si se creó.</param>
/// <param name="Motivo">Por qué no se creó.</param>
/// <param name="ItemConflictivo">
/// La variante que lo impidió, cuando el motivo es de una línea concreta. Sirve para
/// que la pantalla diga <b>cuál</b> producto, no «un producto».
/// </param>
public sealed record ResultadoDeCreacion(
    string? OrderCode,
    decimal TotalAmount,
    DateTimeOffset? PaymentDueAt,
    MotivoDeNoCreacion Motivo,
    Guid? ItemConflictivo = null)
{
    /// <summary>Si el pedido se creó.</summary>
    public bool Creado => OrderCode is not null;

    internal static ResultadoDeCreacion Rechaza(MotivoDeNoCreacion motivo, Guid? item = null)
        => new(null, 0m, null, motivo, item);
}
