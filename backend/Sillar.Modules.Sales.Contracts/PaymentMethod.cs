namespace Sillar.Modules.Sales.Contracts;

/// <summary>
/// Medios de pago que M03 admite en v1. <b>Cerrado por JP.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>Yape y efectivo.</b> Es lo que el registro histórico decía desde el 26 de
/// agosto de 2026 —«la tienda abre cobrando con Yape y efectivo»— y lo que JP
/// ratificó al cerrar la pregunta: el encargo del 26 de septiembre nombraba solo
/// Yape y no resolvía el efectivo, así que quedó escalado hasta ahora.
/// </para>
/// <para>
/// <b>Los dos son pago con confirmación manual por una persona</b>, y eso no cambia
/// con el medio: el hecho de pago no se marca por la pantalla de agradecimiento ni
/// por una declaración del cliente.
/// </para>
/// <para>
/// <b>El efectivo tiene una asimetría operativa que conviene tener escrita:</b> en un
/// pedido de recojo en tienda se cobra en el mostrador, así que su registro suele
/// coincidir con la entrega y no con una verificación previa. No cambia el modelo
/// —sigue siendo un pago registrado por una persona— pero sí el momento en que
/// ocurre.
/// </para>
/// <para>
/// <b>La tarjeta no está y no se adelanta nada de ella.</b> Llega con M11 en la
/// fase 4, y esa decisión —del 26 de agosto de 2026— sigue vigente.
/// </para>
/// </remarks>
public static class PaymentMethod
{
    /// <summary>Yape, con confirmación manual por una persona.</summary>
    public const string Yape = "yape";

    /// <summary>
    /// Efectivo, cobrado en el mostrador al recoger y registrado por una persona.
    /// </summary>
    public const string Cash = "efectivo";

    /// <summary>
    /// Los dos valores admitidos en v1. Alimenta el <c>CHECK</c> de la migración, así
    /// que la lista y la restricción de la base no pueden separarse.
    /// </summary>
    public static readonly string[] All = [Yape, Cash];
}
