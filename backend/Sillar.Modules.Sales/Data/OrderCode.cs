using System.Globalization;

namespace Sillar.Modules.Sales.Data;

/// <summary>
/// Compone y reconoce el código visible de un pedido: <c>P-2026-0147</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Está aparte y es puro —sin base ni reloj— a propósito.</b> Una barrera que
/// solo se puede provocar levantando medio sistema es una barrera que nadie va a
/// provocar; así el formato se prueba en milisegundos y el asignador solo lo
/// consulta.
/// </para>
/// <para>
/// El formato lo rectificó JP el 27 de septiembre de 2026: <b>etiqueta del nodo,
/// año y correlativo</b>.
/// </para>
/// </remarks>
public static class OrderCode
{
    /// <summary>Cuántos dígitos lleva el correlativo, rellenando con ceros.</summary>
    /// <remarks>
    /// Cuatro, como el ejemplo ratificado. <b>No es un techo:</b> el número 10 000
    /// de un año se escribiría con cinco dígitos y seguiría siendo válido y
    /// ordenable como texto dentro de su año, porque el año va delante. Rellenar es
    /// para que se lea alineado, no para limitar.
    /// </remarks>
    public const int CorrelativeDigits = 4;

    /// <summary>
    /// Compone el código a partir de sus tres piezas.
    /// </summary>
    /// <param name="seriesLabel">
    /// La etiqueta visible del nodo, derivada de su código y fijada en la
    /// instalación. <b>Nunca un valor por defecto.</b>
    /// </param>
    /// <param name="year">El año de la serie.</param>
    /// <param name="correlative">El número dentro de la serie, desde 1.</param>
    /// <exception cref="ArgumentException">
    /// Si la etiqueta está vacía o en blanco, o si el correlativo no es positivo.
    /// <b>La guarda está aquí, en la operación que compone</b>, y no en quien
    /// llama: ponerla en el llamador protegería de ese llamador, y aquí protege de
    /// todos, incluidos los que todavía no existen.
    /// </exception>
    public static string Componer(string seriesLabel, int year, int correlative)
    {
        if (string.IsNullOrWhiteSpace(seriesLabel))
        {
            throw new ArgumentException(
                "La etiqueta de serie de pedidos no está configurada. Se fija en la " +
                $"instalación, en el ajuste '{SalesSettingsKeys.OrderSeriesLabel}', y se " +
                "deriva del código del nodo: no tiene valor por defecto.",
                nameof(seriesLabel));
        }

        if (correlative < 1)
        {
            throw new ArgumentException(
                $"El correlativo de un pedido empieza en 1; llegó {correlative}.",
                nameof(correlative));
        }

        var numero = correlative.ToString(CultureInfo.InvariantCulture)
            .PadLeft(CorrelativeDigits, '0');

        return $"{seriesLabel.Trim()}-{year.ToString(CultureInfo.InvariantCulture)}-{numero}";
    }
}
