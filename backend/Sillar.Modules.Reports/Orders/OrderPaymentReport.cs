using System.Text.RegularExpressions;
using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Reports.Orders;

/// <summary>
/// Puerto consumidor de I05–I07. Refleja campo a campo <c>IOrderPaymentAggregateReader</c>
/// de M03 (candidato <c>30cb8c3</c>).
/// </summary>
/// <remarks>
/// Interno por la misma razón que <see cref="IOrderValueAggregatePort"/>. El
/// adaptador explícito está en <see cref="MonetarySources"/>.
/// </remarks>
internal interface IOrderPaymentAggregatePort
{
    /// <summary>Pagos por <c>registered_at</c> en [inicio, fin).</summary>
    Task<PaymentWindow> ReadRegisteredBetweenAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken);
}

/// <summary>Una ventana de pagos, como <c>OrderPaymentAggregateSnapshot</c>.</summary>
/// <param name="StartInclusive">Límite incluido, en UTC; tiene que ser el pedido.</param>
/// <param name="EndExclusive">Límite excluido, en UTC; tiene que ser el pedido.</param>
/// <param name="ObservedAtUtc">Instante en que ventas leyó, según su reloj.</param>
/// <param name="Groups">Filas observadas. Observadas no es completas: lo dice la cobertura.</param>
/// <param name="Coverage">Una entrada por origen e indicador.</param>
internal sealed record PaymentWindow(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<PaymentGroup> Groups,
    IReadOnlyList<PaymentCoverageEntry> Coverage);

/// <summary>Pagos con el mismo origen, medio, moneda y tardanza, como <c>PaymentAggregateGroup</c>.</summary>
/// <param name="Source">
/// Origen: <c>staff</c>, <c>provider</c> o null, que es un pago histórico sin origen
/// registrado (decisión JP-B): no se atribuye a ningún origen.
/// </param>
/// <param name="Method">Código de medio de M03.</param>
/// <param name="CurrencyCode">ISO 4217, o null si la moneda es desconocida.</param>
/// <param name="WasLate">El hecho guardado; no se recalcula.</param>
/// <param name="Count">Pagos del grupo.</param>
/// <param name="Amount">Suma de importes; null si y solo si no hay moneda.</param>
internal sealed record PaymentGroup(string? Source, string Method, string? CurrencyCode, bool WasLate, long Count, decimal? Amount);

/// <summary>Cobertura de un indicador para un origen en la ventana, como <c>PaymentCoverage</c>.</summary>
/// <param name="Source">Origen.</param>
/// <param name="Indicator">Uno de <see cref="PaymentIndicators"/>.</param>
/// <param name="Measured">Medido: lo observado es todo lo que ocurrió.</param>
/// <param name="Reason">Uno de <see cref="NotMeasurableReasons"/>; nulo si y solo si es medido.</param>
/// <param name="CoveredSinceUtc">Desde cuándo está acreditado el escritor del origen; nulo si no lo está.</param>
internal sealed record PaymentCoverageEntry(
    string Source, string Indicator, bool Measured, string? Reason, DateTimeOffset? CoveredSinceUtc);

/// <summary>Qué se mide sobre los pagos de un origen, como <c>PaymentIndicator</c>.</summary>
internal static class PaymentIndicators
{
    public const string Count = "count";
    public const string LateCount = "late_count";
    public const string AmountByCurrency = "amount_by_currency";

    public static readonly string[] All = [Count, LateCount, AmountByCurrency];
}

/// <summary>Por qué un valor no se puede medir, con el vocabulario de la pantalla.</summary>
public static class NotMeasurableReasons
{
    /// <summary>Ventas está, pero no publica el lector de cobros.</summary>
    public const string ReaderAbsent = "reader_absent";

    /// <summary>No hay escritor acreditado para el origen: ni todo consta ni la ausencia es cero.</summary>
    public const string WriterNotAccredited = "writer_not_accredited";

    /// <summary>El escritor está acreditado, pero desde después del inicio de la ventana.</summary>
    public const string WindowStartsBeforeCoverage = "window_starts_before_coverage";

    /// <summary>Hay pagos del origen sin moneda: el importe no se puede afirmar completo.</summary>
    public const string CurrencyNotRecorded = "currency_not_recorded";

    /// <summary>Hay pagos históricos sin origen: cualquiera podría ser de este, así que nada se afirma completo.</summary>
    public const string SourceNotRecorded = "source_not_recorded";
}

/// <summary>
/// I05 cobros registrados, I06 importe por medio y moneda, I07 cobros tardíos
/// (D-M10-04: «cobros registrados», no ingresos).
/// </summary>
/// <remarks>
/// <para>
/// <b>Solo cuenta lo que M03 declara medido.</b> La cobertura es por origen e
/// indicador: un origen no medido no suma nada, ni siquiera sus filas observadas,
/// y su ausencia nunca es un cero. Con algún origen sin medir, la cifra es la de
/// los medidos y la cobertura es <c>partial</c>.
/// </para>
/// <para>
/// Sin ningún origen con escritor acreditado, los tres son «no medible» aunque
/// haya filas.
/// </para>
/// </remarks>
internal sealed partial class OrderPaymentReport(IServiceProvider services)
{
    /// <summary>Los orígenes sobre los que M03 declara cobertura (<c>PaymentSource.All</c>), en orden.</summary>
    internal static readonly string[] Sources = ["staff", "provider"];

    /// <summary>Lee los cobros del período. Un fallo o una respuesta que no cuadra se propagan.</summary>
    public async Task<PaymentIndicatorsView> ReadAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
    {
        var startUtc = startInclusive.ToUniversalTime();
        var endUtc = endExclusive.ToUniversalTime();

        if (services.GetService(typeof(IOrderAggregateReader)) is null)
        {
            return PaymentIndicatorsView.Unmeasured(IndicatorStatus.SourceUnavailable, null, startUtc, endUtc);
        }
        if (MonetarySources.Payments(services) is not { } port)
        {
            return PaymentIndicatorsView.Unmeasured(IndicatorStatus.NotMeasurable, NotMeasurableReasons.ReaderAbsent, startUtc, endUtc);
        }

        var window = await port.ReadRegisteredBetweenAsync(startUtc, endUtc, cancellationToken);
        return Project(window, startUtc, endUtc);
    }

    /// <summary>Comprueba la respuesta y la reparte en los tres indicadores.</summary>
    internal static PaymentIndicatorsView Project(PaymentWindow window, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        MonetaryFacts.CheckPeriod(window.StartInclusive, window.EndExclusive, startUtc, endUtc);
        var groups = CheckedGroups(window.Groups);
        var coverage = CheckedCoverage(window.Coverage, groups, startUtc);
        PaymentCoverageEntry Of(string source, string indicator) => coverage[(source, indicator)];

        // Los históricos sin origen no se descartan ni se atribuyen: se cuentan aparte.
        var historical = MonetaryFacts.Total(groups.Where(g => g.Source is null).Select(g => g.Count)) is > 0 and var n
            ? new HistoricalWithoutSourceView(n, window.ObservedAtUtc)
            : null;

        // Ningún origen con el recuento medido: ninguna cifra por origen, haya filas o no.
        if (!Sources.Any(s => Of(s, PaymentIndicators.Count).Measured))
        {
            var reasons = Sources.Select(s => Of(s, PaymentIndicators.Count).Reason).ToArray();
            var reason = reasons.Contains(NotMeasurableReasons.SourceNotRecorded) ? NotMeasurableReasons.SourceNotRecorded
                : reasons.Contains(NotMeasurableReasons.WindowStartsBeforeCoverage) ? NotMeasurableReasons.WindowStartsBeforeCoverage
                : NotMeasurableReasons.WriterNotAccredited;
            return PaymentIndicatorsView.Unmeasured(IndicatorStatus.NotMeasurable, reason, startUtc, endUtc) with
            {
                HistoricalWithoutSource = historical,
            };
        }

        long CountOf(string source, Func<PaymentGroup, bool>? filter = null)
            => MonetaryFacts.Total(groups.Where(g => g.Source == source && (filter?.Invoke(g) ?? true)).Select(g => g.Count));

        return new(startUtc, endUtc, window.ObservedAtUtc, null,
            I05(Of, CountOf),
            I06(Of, CountOf, groups),
            I07(Of, CountOf, groups))
        {
            HistoricalWithoutSource = historical,
        };
    }

    private static PaymentGroup[] CheckedGroups(IReadOnlyList<PaymentGroup> groups)
    {
        if (groups is null) throw new SourceIncoherentException("Sin grupos.");
        foreach (var group in groups)
        {
            // Null es el histórico sin origen; cualquier otro valor tiene que ser un origen conocido.
            if (group is null || (group.Source is not null && !Sources.Contains(group.Source)))
            {
                throw new SourceIncoherentException("Origen desconocido.");
            }
            if (group.Method is null || !MethodCode().IsMatch(group.Method)) throw new SourceIncoherentException("Medio sin código.");
            MonetaryFacts.CheckGroup(group.CurrencyCode, group.Count, group.Amount);
            if (group.Count == 0) throw new SourceIncoherentException("Grupo sin pagos.");
        }
        if (groups.Select(g => (g.Source, g.Method, g.CurrencyCode, g.WasLate)).Distinct().Count() != groups.Count)
        {
            throw new SourceIncoherentException("Grupo repetido.");
        }
        return [.. groups];
    }

    /// <summary>
    /// Una entrada por origen e indicador, cada una coherente consigo misma y con
    /// las filas de su origen.
    /// </summary>
    private static Dictionary<(string, string), PaymentCoverageEntry> CheckedCoverage(
        IReadOnlyList<PaymentCoverageEntry> entries, PaymentGroup[] groups, DateTimeOffset startUtc)
    {
        if (entries is null || entries.Any(e => e is null)) throw new SourceIncoherentException("Sin cobertura.");
        var coverage = entries
            .GroupBy(e => (e.Source, e.Indicator))
            .ToDictionary(g => g.Key, g => g.Count() == 1 ? g.Single() : throw new SourceIncoherentException("Cobertura repetida."));
        var expected = Sources.SelectMany(s => PaymentIndicators.All.Select(i => (s, i))).ToHashSet();
        if (!coverage.Keys.ToHashSet().SetEquals(expected)) throw new SourceIncoherentException("Cobertura incompleta.");

        foreach (var entry in coverage.Values)
        {
            var coherent = entry.Measured
                ? entry is { Reason: null, CoveredSinceUtc: { } since } && since <= startUtc
                : entry.Reason switch
                {
                    NotMeasurableReasons.WriterNotAccredited => entry.CoveredSinceUtc is null,
                    NotMeasurableReasons.WindowStartsBeforeCoverage => entry.CoveredSinceUtc > startUtc,
                    NotMeasurableReasons.CurrencyNotRecorded => entry.Indicator == PaymentIndicators.AmountByCurrency
                        && entry.CoveredSinceUtc <= startUtc,
                    NotMeasurableReasons.SourceNotRecorded => entry.CoveredSinceUtc <= startUtc,
                    _ => false,
                };
            if (!coherent) throw new SourceIncoherentException("Cobertura incoherente.");
        }

        // Con históricos sin origen en la ventana, ningún origen es completo: podrían ser suyos.
        var withoutSource = groups.Any(g => g.Source is null);
        if (withoutSource && coverage.Values.Any(e => e.Measured))
        {
            throw new SourceIncoherentException("Origen medido con pagos sin origen.");
        }
        if (!withoutSource && coverage.Values.Any(e => e.Reason == NotMeasurableReasons.SourceNotRecorded))
        {
            throw new SourceIncoherentException("Origen no registrado sin pagos sin origen.");
        }

        // Cada indicador tiene su propia fecha de acreditación: el contrato no
        // promete que las tres capacidades de un origen empiecen a la vez.
        foreach (var source in Sources)
        {
            var amount = coverage[(source, PaymentIndicators.AmountByCurrency)];
            var withoutCurrency = groups.Any(g => g.Source == source && g.CurrencyCode is null);
            if (amount.Measured && withoutCurrency) throw new SourceIncoherentException("Importe completo con pagos sin moneda.");
            if (amount.Reason == NotMeasurableReasons.CurrencyNotRecorded && !withoutCurrency)
            {
                throw new SourceIncoherentException("Moneda no registrada sin pagos sin moneda.");
            }
        }
        return coverage;
    }

    private static string Coverage(int measured) => measured == Sources.Length ? CurrencyCoverage.Complete : CurrencyCoverage.Partial;

    /// <summary>Solo con todos los orígenes medidos un cero dice «no hubo».</summary>
    private static string CountStatus(long count, int measured)
        => measured == Sources.Length && count == 0 ? IndicatorStatus.Empty : IndicatorStatus.WithData;

    private static SourceMeasureView Source(PaymentCoverageEntry entry, long count)
        => entry.Measured
            ? new(entry.Source, MeasureStatus.Measured, count, null, entry.CoveredSinceUtc)
            : new(entry.Source, MeasureStatus.NotMeasurable, null, entry.Reason, entry.CoveredSinceUtc);

    private static PaymentCountView I05(Func<string, string, PaymentCoverageEntry> of, Func<string, Func<PaymentGroup, bool>?, long> countOf)
    {
        var measured = Sources.Where(s => of(s, PaymentIndicators.Count).Measured).ToArray();
        var total = MonetaryFacts.Total(measured.Select(s => countOf(s, null)));
        return new(CountStatus(total, measured.Length), total, Coverage(measured.Length),
            [.. Sources.Select(s => Source(of(s, PaymentIndicators.Count), countOf(s, null)))]);
    }

    private static PaymentAmountsView I06(
        Func<string, string, PaymentCoverageEntry> of, Func<string, Func<PaymentGroup, bool>?, long> countOf, PaymentGroup[] groups)
    {
        var bySource = Sources.Select(s => Source(of(s, PaymentIndicators.AmountByCurrency), countOf(s, null))).ToArray();
        var measured = Sources.Where(s => of(s, PaymentIndicators.AmountByCurrency).Measured).ToArray();
        // Pagos sin moneda de los orígenes cuyo recuento sí se mide: se cuentan, no se suman.
        var countedSources = Sources.Where(s => of(s, PaymentIndicators.Count).Measured).ToArray();
        var withoutCurrency = MonetaryFacts.Total(countedSources.Select(s => countOf(s, g => g.CurrencyCode is null)));

        if (measured.Length == 0)
        {
            // Sin moneda es «no acreditada»; cualquier otro motivo (escritor o ventana) es «no medible».
            return Sources.Any(s => of(s, PaymentIndicators.AmountByCurrency).Reason == NotMeasurableReasons.CurrencyNotRecorded)
                ? new(IndicatorStatus.CurrencyNotAccredited, null, null, withoutCurrency, CurrencyCoverage.None, bySource)
                : new(IndicatorStatus.NotMeasurable, null, null, withoutCurrency, null, bySource);
        }
        var priced = groups.Where(g => measured.Contains(g.Source)).ToArray();
        var byMethod = priced
            .GroupBy(g => (g.Method, Source: g.Source!, Currency: g.CurrencyCode!))
            .OrderBy(g => g.Key.Method, StringComparer.Ordinal)
            .ThenBy(g => Array.IndexOf(Sources, g.Key.Source))
            .ThenBy(g => g.Key.Currency, StringComparer.Ordinal)
            .Select(g => new MethodAmountView(g.Key.Method, g.Key.Source, g.Key.Currency,
                MonetaryFacts.Total(g.Select(x => x.Count)), MonetaryFacts.Format(Sum(g))))
            .ToArray();
        var total = MonetaryFacts.Total(priced.Select(g => g.Count));
        return new(CountStatus(total, measured.Length), ByCurrency(priced), byMethod, withoutCurrency, Coverage(measured.Length), bySource);
    }

    private static LatePaymentsView I07(
        Func<string, string, PaymentCoverageEntry> of, Func<string, Func<PaymentGroup, bool>?, long> countOf, PaymentGroup[] groups)
    {
        var bySource = Sources.Select(s => Source(of(s, PaymentIndicators.LateCount), countOf(s, g => g.WasLate))).ToArray();
        var measured = Sources.Where(s => of(s, PaymentIndicators.LateCount).Measured).ToArray();
        if (measured.Length == 0)
        {
            return new(IndicatorStatus.NotMeasurable, null, null, bySource, IndicatorStatus.NotMeasurable, null, null);
        }
        var late = MonetaryFacts.Total(measured.Select(s => countOf(s, g => g.WasLate)));

        // El importe de los tardíos, solo de los orígenes con tardanza e importe medidos.
        var priced = measured.Where(s => of(s, PaymentIndicators.AmountByCurrency).Measured).ToArray();
        if (priced.Length == 0)
        {
            return measured.Any(s => of(s, PaymentIndicators.AmountByCurrency).Reason == NotMeasurableReasons.CurrencyNotRecorded)
                ? new(CountStatus(late, measured.Length), late, Coverage(measured.Length), bySource,
                    IndicatorStatus.CurrencyNotAccredited, null, CurrencyCoverage.None)
                : new(CountStatus(late, measured.Length), late, Coverage(measured.Length), bySource,
                    IndicatorStatus.NotMeasurable, null, null);
        }
        var lateGroups = groups.Where(g => g.WasLate && priced.Contains(g.Source)).ToArray();
        return new(CountStatus(late, measured.Length), late, Coverage(measured.Length), bySource,
            CountStatus(MonetaryFacts.Total(lateGroups.Select(g => g.Count)), priced.Length),
            ByCurrency(lateGroups), Coverage(priced.Length));
    }

    /// <summary>Una fila por moneda: se suma dentro de una moneda, nunca entre monedas.</summary>
    private static CurrencyAmountView[] ByCurrency(IEnumerable<PaymentGroup> priced)
        => [.. priced
            .GroupBy(g => g.CurrencyCode!)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new CurrencyAmountView(g.Key, MonetaryFacts.Total(g.Select(x => x.Count)), MonetaryFacts.Format(Sum(g))))];

    private static decimal Sum(IEnumerable<PaymentGroup> sameCurrency)
    {
        try
        {
            return sameCurrency.Aggregate(0m, (sum, g) => sum + g.Amount!.Value);
        }
        catch (OverflowException)
        {
            throw new SourceIncoherentException("Los importes desbordan.");
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex MethodCode();
}

/// <summary>Estado de una medida por origen.</summary>
public static class MeasureStatus
{
    /// <summary>Medido: su cero es un cero comprobado.</summary>
    public const string Measured = "measured";

    /// <summary>No medible: sin cifra.</summary>
    public const string NotMeasurable = "not_measurable";
}

/// <summary>I05–I07 tal como los consume la pantalla. Cada indicador lleva su propio estado.</summary>
/// <param name="StartInclusive">Límite incluido, en UTC.</param>
/// <param name="EndExclusive">Límite excluido, en UTC.</param>
/// <param name="ObservedAtUtc">Instante en que ventas leyó; nulo si no se midió.</param>
/// <param name="NotMeasurableReason">Por qué ninguno se pudo medir (<see cref="NotMeasurableReasons"/>); nulo si se midió.</param>
/// <param name="I05">Cobros registrados.</param>
/// <param name="I06">Importe por medio y moneda.</param>
/// <param name="I07">Cobros tardíos.</param>
/// <remarks>
/// <see cref="HistoricalWithoutSource"/>: pagos históricos sin origen registrado
/// (JP-B), observados en la ventana. Nunca se suman a un origen; nulo si no hay.
/// </remarks>
public sealed record PaymentIndicatorsView(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset? ObservedAtUtc,
    string? NotMeasurableReason,
    PaymentCountView I05,
    PaymentAmountsView I06,
    LatePaymentsView I07)
{
    /// <summary>Pagos históricos sin origen observados en la ventana; nulo si no hay.</summary>
    public HistoricalWithoutSourceView? HistoricalWithoutSource { get; init; }

    internal static PaymentIndicatorsView Unmeasured(string status, string? reason, DateTimeOffset startUtc, DateTimeOffset endUtc)
        => new(startUtc, endUtc, null, reason,
            new(status, null, null, null),
            new(status, null, null, null, null, null),
            new(status, null, null, null, status, null, null));
}

/// <summary>
/// Pagos históricos sin origen registrado: «origen histórico no acreditado». Se
/// observan y se cuentan, pero no son del personal ni del proveedor, y no completan
/// ninguna cifra.
/// </summary>
public sealed record HistoricalWithoutSourceView(long Count, DateTimeOffset ObservedAtUtc);

/// <summary>Un origen con su medida: cuenta si se mide, motivo y desde cuándo si no.</summary>
public sealed record SourceMeasureView(string Source, string Status, long? Count, string? Reason, DateTimeOffset? CoveredSinceUtc);

/// <summary>I05: pagos de los orígenes medidos; cobertura <c>partial</c> si alguno no lo es.</summary>
public sealed record PaymentCountView(string Status, long? Count, string? Coverage, IReadOnlyList<SourceMeasureView>? BySource);

/// <summary>I06: importe por moneda y por medio, de los orígenes con importe medido.</summary>
public sealed record PaymentAmountsView(
    string Status,
    IReadOnlyList<CurrencyAmountView>? ByCurrency,
    IReadOnlyList<MethodAmountView>? ByMethod,
    long? WithoutCurrencyCount,
    string? Coverage,
    IReadOnlyList<SourceMeasureView>? BySource);

/// <summary>Importe de un medio y origen en una sola moneda.</summary>
public sealed record MethodAmountView(string Method, string Source, string CurrencyCode, long Count, string Amount);

/// <summary>I07: tardíos de los orígenes medidos y, aparte, su importe por moneda.</summary>
public sealed record LatePaymentsView(
    string Status,
    long? Count,
    string? Coverage,
    IReadOnlyList<SourceMeasureView>? BySource,
    string AmountStatus,
    IReadOnlyList<CurrencyAmountView>? AmountByCurrency,
    string? AmountCoverage);
