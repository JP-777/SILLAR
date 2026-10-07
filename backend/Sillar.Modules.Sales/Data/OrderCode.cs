using System.Globalization;
using Sillar.Shared.Data.Numbering;

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
    /// <summary>
    /// La zona del negocio. El año visible de un pedido es el de Lima, no el de UTC.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No es un detalle de presentación: decide a qué serie pertenece el pedido.</b>
    /// El 31 de diciembre a las 20:00 de Lima ya es el 1 de enero en UTC, así que un
    /// pedido hecho esa tarde llevaría el año siguiente y abriría una serie que
    /// todavía no le corresponde. Y el cliente que lo dicte por teléfono estaría
    /// leyendo un año que no es el de su calendario.
    /// </para>
    /// <para>
    /// Es además la zona en la que ya numera M07 sus cotizaciones, y dos módulos del
    /// mismo producto discrepando sobre qué año es sería un defecto que solo se ve
    /// una tarde al año.
    /// </para>
    /// <para>
    /// Se resuelve por identificador IANA, que .NET traduce también en Windows —el
    /// desarrollo alterna entre Windows y Arch Linux—.
    /// </para>
    /// </remarks>
    public const string ZonaDelNegocio = TransactionalSeriesAllocator.BusinessTimeZone;

    /// <summary>
    /// El año al que pertenece la serie de un instante dado, en hora de Lima.
    /// </summary>
    /// <remarks>
    /// Está aquí, en la parte pura y sin base ni reloj inyectado, para que su
    /// frontera se pueda provocar en milisegundos: la del 31 de diciembre es la única
    /// que importa y no hace falta una base de datos para verla.
    /// </remarks>
    /// <param name="instante">El momento, con su desplazamiento.</param>
    public static int AnioDe(DateTimeOffset instante)
        => TransactionalSeriesAllocator.YearInLima(instante);

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
