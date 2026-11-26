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

    /// <summary>
    /// Si un texto tiene la forma de un código visible de pedido.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El resumen de este tipo decía «compone y reconoce» desde el principio, y
    /// reconocer no estaba.</b> Está aquí y no en quien correlaciona porque la forma
    /// es una sola, y tenerla dos veces es tenerla distinta el día que cambie.
    /// </para>
    /// <para>
    /// <b>No valida la etiqueta, y no puede.</b> La etiqueta se fija en la instalación
    /// (<see cref="SalesSettingsKeys.OrderSeriesLabel"/>) y esta función es pura: solo
    /// exige que exista. Reconocer la forma <b>no</b> afirma que el pedido exista.
    /// </para>
    /// <para>
    /// <b>Las reglas salen de <see cref="Componer"/>, no de una suposición:</b> se lee
    /// por la derecha —así una etiqueta con guiones sigue funcionando—, el correlativo
    /// lleva al menos <see cref="CorrelativeDigits"/> dígitos y nunca es cero porque
    /// la serie empieza en 1, y los dígitos son los ASCII que emite
    /// <see cref="CultureInfo.InvariantCulture"/>: <c>char.IsDigit</c> aceptaría
    /// también los dígitos índicos, que esta serie no produce jamás.
    /// </para>
    /// <para>
    /// <b>Distingue mayúsculas, a propósito.</b> La columna <c>order_code</c> se
    /// declaró sin colación —migración inicial, «ninguna colación no determinista»—,
    /// así que <c>p-2026-0001</c> y <c>P-2026-0001</c> son dos códigos distintos en la
    /// base. Plegar aquí lo que la base no pliega es cómo se correlaciona un pago con
    /// el pedido de otro.
    /// </para>
    /// </remarks>
    /// <param name="codigo">El texto a reconocer. <c>null</c> no es una forma válida.</param>
    public static bool ReconocerForma(string? codigo)
    {
        // Componer nunca deja espacio en los extremos: la etiqueta se recorta y el
        // resto son dígitos. Un código con envoltura no salió de aquí.
        if (string.IsNullOrEmpty(codigo) || codigo.Length != codigo.Trim().Length)
        {
            return false;
        }

        var ultimo = codigo.LastIndexOf('-');
        if (ultimo <= 0)
        {
            return false;
        }

        var penultimo = codigo.LastIndexOf('-', ultimo - 1);
        if (penultimo <= 0)
        {
            // <= 0 cubre las dos: no hay segundo guion, o la etiqueta está vacía.
            return false;
        }

        var correlativo = codigo.AsSpan(ultimo + 1);
        var anio = codigo.AsSpan(penultimo + 1, ultimo - penultimo - 1);
        var etiqueta = codigo.AsSpan(0, penultimo);

        return !etiqueta.IsWhiteSpace()
            && SoloDigitosAscii(anio)
            && SoloDigitosAscii(correlativo)
            && correlativo.Length >= CorrelativeDigits
            && correlativo.IndexOfAnyExcept('0') >= 0;
    }

    private static bool SoloDigitosAscii(ReadOnlySpan<char> texto)
    {
        if (texto.IsEmpty)
        {
            return false;
        }

        foreach (var c in texto)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
