namespace Sillar.Modules.Sales.Contracts;

/// <summary>
/// De dónde viene un pago: quién lo registró en el libro de M03.
/// </summary>
/// <remarks>
/// <para>
/// <b>No es el medio.</b> <see cref="PaymentMethod"/> dice <b>cómo</b> pagó el cliente
/// —Yape, efectivo—; esto dice <b>quién</b> asienta el hecho: una persona del personal
/// o, algún día, un proveedor de pagos verificado por M11. Son dos ejes, y fundirlos
/// obligaría a inventar un «medio proveedor» que no describe ningún medio.
/// </para>
/// <para>
/// <b>El <c>CHECK</c> contiene solo lo ratificado</b>, igual que el de los medios:
/// hoy únicamente <see cref="Staff"/>. <see cref="Provider"/> está nombrado para que
/// los lectores puedan decir «esto no se puede medir» en vez de «cero», pero ningún
/// escritor lo produce y la base lo rechaza. Añadir un valor después es una migración
/// barata; quitar uno con filas dentro no lo es.
/// </para>
/// </remarks>
public static class PaymentSource
{
    /// <summary>Registrado por una persona, con su atribución completa de tres datos.</summary>
    public const string Staff = "staff";

    /// <summary>
    /// Reservado: liquidación de un proveedor externo. <b>Sin escritor y fuera del
    /// <c>CHECK</c>.</b> Llega cuando se ratifiquen moneda, identidad durable y ciclo
    /// de vida del pago externo.
    /// </summary>
    public const string Provider = "provider";

    /// <summary>
    /// Los orígenes que la base admite hoy. Alimenta el <c>CHECK</c>, así que la lista
    /// y la restricción no pueden separarse.
    /// </summary>
    public static readonly string[] Ratified = [Staff];
}
