using System.Globalization;
using System.Text.RegularExpressions;

namespace Sillar.Modules.Reports.Orders;

/// <summary>
/// Estado de cada indicador, con el vocabulario que consume la pantalla. Ninguno
/// se deduce de otro: un 0 solo existe en <see cref="Empty"/>.
/// </summary>
public static class IndicatorStatus
{
    /// <summary>El módulo de ventas no está instalado o activo.</summary>
    public const string SourceUnavailable = "source_unavailable";

    /// <summary>Ventas está, pero no acredita la capacidad de medirlo (D-M10-11).</summary>
    public const string NotMeasurable = "not_measurable";

    /// <summary>Capacidad acreditada y ningún hecho en el período: el único 0.</summary>
    public const string Empty = "empty";

    /// <summary>Hay hechos, pero ninguno con moneda: se cuentan, no se suman (AC-M10-03).</summary>
    public const string CurrencyNotAccredited = "currency_not_accredited";

    /// <summary>Hay hechos que se pueden publicar.</summary>
    public const string WithData = "with_data";
}

/// <summary>
/// Cuánto de un importe quedó acreditado: todo, una parte (hay hechos sin moneda
/// que no se suman) o nada.
/// </summary>
public static class CurrencyCoverage
{
    /// <summary>Todos los hechos tienen moneda.</summary>
    public const string Complete = "complete";

    /// <summary>Algunos hechos no tienen moneda; se cuentan aparte y no se suman.</summary>
    public const string Partial = "partial";

    /// <summary>Ningún hecho tiene moneda.</summary>
    public const string None = "none";
}

/// <summary>Importe de una sola moneda. El importe viaja como texto decimal exacto, nunca como número de coma flotante.</summary>
public sealed record CurrencyAmountView(string CurrencyCode, long Count, string Amount);

/// <summary>
/// Ventas respondió, pero con cifras que no cuadran: no se publica ninguna. Es
/// un fallo de la fuente, distinto de «no disponible» y de «no medible».
/// </summary>
internal sealed class SourceIncoherentException(string reason) : Exception(reason);

/// <summary>Reglas comunes a todo importe que M10 recibe de ventas.</summary>
internal static partial class MonetaryFacts
{
    /// <summary>
    /// Un grupo de hechos con o sin moneda. Sin moneda no hay importe; con moneda,
    /// código ISO 4217 en mayúsculas e importe no negativo. Ningún reverso se
    /// presume (P-M10-08): un importe negativo es incoherente.
    /// </summary>
    public static void CheckGroup(string? currencyCode, long count, decimal? amount)
    {
        if (count < 0) throw new SourceIncoherentException("Recuento negativo.");
        if (currencyCode is null)
        {
            if (amount is not null) throw new SourceIncoherentException("Importe sin moneda.");
            return;
        }
        if (!IsoCurrency().IsMatch(currencyCode)) throw new SourceIncoherentException("Moneda que no es ISO 4217.");
        if (amount is not { } value || value < 0) throw new SourceIncoherentException("Moneda sin importe o importe negativo.");
        if (count == 0 && value != 0) throw new SourceIncoherentException("Importe sin hechos.");
    }

    /// <summary>El período respondido tiene que ser el pedido.</summary>
    public static void CheckPeriod(DateTimeOffset answeredStart, DateTimeOffset answeredEnd,
        DateTimeOffset requestedStart, DateTimeOffset requestedEnd)
    {
        if (answeredStart != requestedStart || answeredEnd != requestedEnd)
        {
            throw new SourceIncoherentException("El período respondido no es el pedido.");
        }
    }

    /// <summary>Suma de recuentos que no puede desbordar en silencio.</summary>
    public static long Total(IEnumerable<long> counts)
    {
        try
        {
            return counts.Aggregate(0L, (sum, count) => checked(sum + count));
        }
        catch (OverflowException)
        {
            throw new SourceIncoherentException("Los recuentos desbordan.");
        }
    }

    /// <summary>Texto decimal invariable; conserva la escala que dio ventas.</summary>
    public static string Format(decimal amount) => amount.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoCurrency();
}
