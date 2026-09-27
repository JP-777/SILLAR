namespace Sillar.Modules.Sales.Data;

/// <summary>
/// Las claves de <c>core.site_settings</c> que M03 lee.
/// </summary>
/// <remarks>
/// Se leen siempre por <c>ISettingsReader</c>: nadie toca <c>core.site_settings</c>
/// por su cuenta.
/// </remarks>
public static class SalesSettingsKeys
{
    /// <summary>
    /// La etiqueta visible de la serie de pedidos de este nodo — la <c>P</c> de
    /// <c>P-2026-0147</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No tiene valor por defecto, y eso es la decisión.</b> «<c>P</c>» es el
    /// ejemplo que corresponde al nodo cuyo <c>NodeIdentity.Code</c> es
    /// <c>principal</c>, <b>no una letra universal que pueda grabarse en todos los
    /// nodos</b>. Se deriva del código real del nodo y se fija en la instalación.
    /// </para>
    /// <para>
    /// Sembrar aquí una letra por defecto es exactamente el error del que viene
    /// esta clave: la primera propuesta fue una letra fija, y era una circunstancia
    /// de hoy —hay un solo nodo— a punto de quedar grabada en un dato que viajará a
    /// comprobantes.
    /// </para>
    /// </remarks>
    public const string OrderSeriesLabel = "sales.order_series_label";

    /// <summary>
    /// Cuántas horas naturales tiene el cliente para pagar. Por defecto 48.
    /// </summary>
    /// <remarks>
    /// <b>Es plazo para pagar, no reserva de existencias.</b> Configurable por
    /// instalación; cambiarla no reescribe el plazo de los pedidos ya creados,
    /// porque cada pedido guarda su propio vencimiento.
    /// </remarks>
    public const string PaymentDueHours = "sales.payment_due_hours";

    /// <summary>Valor por defecto del plazo para pagar, en horas naturales.</summary>
    public const int PaymentDueHoursDefault = 48;
}
