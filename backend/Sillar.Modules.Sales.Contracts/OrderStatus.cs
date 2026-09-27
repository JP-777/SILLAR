namespace Sillar.Modules.Sales.Contracts;

/// <summary>
/// Los siete estados de un pedido. <b>Idénticos para cliente y personal.</b>
/// </summary>
/// <remarks>
/// <para>
/// Decisión de producto cerrada el 26 de septiembre de 2026 (SPEC §9.0). No son
/// siete valores elegidos por conveniencia técnica: son los siete que el cliente
/// ve, y el personal ve los mismos. Un estado que el personal viera y el cliente
/// no obligaría a mantener dos vocabularios y a decidir cuál manda.
/// </para>
/// <para>
/// <b>Los valores son inglés técnico y la frase visible es española.</b> La
/// traducción vive en el frontend, no aquí: congelar la frase en la base fijaría
/// el idioma para siempre, y es el mismo criterio que <c>ProductPickerItem</c>
/// aplica al precio — «el contrato da el número y el hecho; la frase la pone
/// quien pinta».
/// </para>
/// <para>
/// <b><see cref="Expired"/> no es <see cref="Cancelled"/>.</b> Un pedido cuyo
/// plazo de pago vence deja de estar garantizado, pero <b>no se cancela por ese
/// hecho</b> y el personal puede registrarle un pago después. Fundirlos obligaría
/// a elegir el peor comportamiento para los dos.
/// </para>
/// </remarks>
public static class OrderStatus
{
    /// <summary>Creado, esperando que el cliente pague. Estado inicial.</summary>
    public const string PendingPayment = "pending_payment";

    /// <summary>El cliente dice que pagó; una persona tiene que comprobarlo.</summary>
    public const string PaymentToVerify = "payment_to_verify";

    /// <summary>Pago confirmado por una persona; se está preparando.</summary>
    public const string Preparing = "preparing";

    /// <summary>Preparado y esperando que el cliente lo recoja.</summary>
    public const string ReadyForPickup = "ready_for_pickup";

    /// <summary>Entregado al cliente. Fin de la secuencia principal.</summary>
    public const string Delivered = "delivered";

    /// <summary>
    /// Venció el plazo para pagar. <b>Sigue existiendo y no está cancelado.</b>
    /// </summary>
    public const string Expired = "expired";

    /// <summary>Cancelado. Nunca por vencimiento del plazo.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>La secuencia principal, en orden.</summary>
    public static readonly string[] MainSequence =
        [PendingPayment, PaymentToVerify, Preparing, ReadyForPickup, Delivered];

    /// <summary>Los siete valores admitidos. Alimenta el <c>CHECK</c> de la migración.</summary>
    public static readonly string[] All =
        [PendingPayment, PaymentToVerify, Preparing, ReadyForPickup, Delivered, Expired, Cancelled];
}
