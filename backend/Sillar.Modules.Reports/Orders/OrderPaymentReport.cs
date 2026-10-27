using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Reports.Orders;

/// <summary>
/// Puerto consumidor de I05–I07. Refleja <c>IOrderPaymentAggregateReader</c> de M03
/// (candidato <c>014fba0</c>), más una señal que ese contrato no declara.
/// </summary>
/// <remarks>
/// Interno por la misma razón que <see cref="IOrderValueAggregatePort"/>. El
/// adaptador explícito está en <see cref="MonetarySources"/>.
/// </remarks>
internal interface IOrderPaymentAggregatePort
{
    /// <summary>Lo que el libro de pagos de M03 registra hoy.</summary>
    PaymentCapabilities Capabilities { get; }

    /// <summary>Pagos por <c>registered_at</c> en [inicio, fin).</summary>
    Task<PaymentCohort> ReadRegisteredBetweenAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken);
}

/// <summary>Capacidades del libro de pagos, como <c>OrderPaymentAggregateCapabilities</c>.</summary>
/// <param name="SourceRecorded">El origen staff/provider se guarda por pago.</param>
/// <param name="CurrencyRecorded">Existe la moneda por pago.</param>
/// <param name="LateRecorded">El retraso se guarda como hecho.</param>
/// <param name="ProviderWriterPresent">Existe un escritor de pagos de proveedor.</param>
/// <param name="StaffWriterPresent">
/// Existe un escritor funcional de pagos del personal. <b>No está en el contrato de
/// M03</b>: D-M10-11 exige que M03 acredite la capacidad, y que exista la tabla no la
/// acredita. Hasta que M03 la declare, el adaptador la da por falsa.
/// </param>
internal sealed record PaymentCapabilities(
    bool SourceRecorded, bool CurrencyRecorded, bool LateRecorded, bool ProviderWriterPresent, bool StaffWriterPresent);

/// <summary>Un recuento que dice si está respaldado, como <c>PaymentCountMeasure</c>.</summary>
/// <param name="Measured">Medido. Un cero aquí es un cero comprobado.</param>
/// <param name="Count">Nulo si y solo si no es medible.</param>
/// <param name="Reason">Por qué no es medible (<see cref="NotMeasurableReasons"/>); nulo si lo es.</param>
internal sealed record PaymentMeasure(bool Measured, long? Count, string? Reason);

/// <summary>Una cohorte de pagos, como <c>OrderPaymentAggregateSnapshot</c>.</summary>
/// <param name="StartInclusive">Límite incluido, en UTC; tiene que ser el pedido.</param>
/// <param name="EndExclusive">Límite excluido, en UTC; tiene que ser el pedido.</param>
/// <param name="ObservedAtUtc">Instante en que ventas leyó, según su reloj.</param>
/// <param name="Staff">Pagos registrados por el personal.</param>
/// <param name="Provider">Pagos de proveedor.</param>
/// <param name="Late">Pagos con <c>was_late</c> guardado, de cualquier origen registrado.</param>
/// <param name="AmountsByCurrency">Importe por moneda de los pagos que la tienen.</param>
/// <param name="PaymentsWithoutCurrency">Pagos sin moneda: solo se cuentan.</param>
/// <param name="AmountsComplete">Si todos los pagos de la cohorte tienen moneda.</param>
internal sealed record PaymentCohort(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset ObservedAtUtc,
    PaymentMeasure Staff,
    PaymentMeasure Provider,
    PaymentMeasure Late,
    IReadOnlyList<PaymentCurrencyGroup> AmountsByCurrency,
    long PaymentsWithoutCurrency,
    bool AmountsComplete);

/// <summary>Importe de una moneda, como <c>PaymentAmountByCurrency</c>.</summary>
/// <param name="CurrencyCode">ISO 4217.</param>
/// <param name="Count">Pagos en esa moneda.</param>
/// <param name="Amount">Suma de sus importes.</param>
internal sealed record PaymentCurrencyGroup(string CurrencyCode, long Count, decimal Amount);

/// <summary>Por qué un valor no se puede medir, con el vocabulario de la pantalla.</summary>
public static class NotMeasurableReasons
{
    /// <summary>Ventas está, pero no publica el lector de cobros.</summary>
    public const string ReaderAbsent = "reader_absent";

    /// <summary>M03 no acredita un escritor funcional de pagos del personal (D-M10-11).</summary>
    public const string StaffWriterNotAccredited = "staff_writer_not_accredited";

    /// <summary>El libro no guarda el origen de cada pago.</summary>
    public const string SourceNotRecorded = "source_not_recorded";

    /// <summary>No hay escritor de pagos de proveedor: su ausencia no prueba que no los hubo.</summary>
    public const string ProviderWriterAbsent = "provider_writer_absent";

    /// <summary>Hay pagos sin moneda: el importe no se puede afirmar completo.</summary>
    public const string CurrencyNotRecorded = "currency_not_recorded";
}

/// <summary>
/// I05 cobros registrados, I06 importe cobrado, I07 cobros tardíos (D-M10-04:
/// «cobros registrados», no ingresos).
/// </summary>
/// <remarks>
/// <b>Un origen que no se puede medir nunca cuenta como cero</b> y nunca deja
/// afirmar cobertura total: con el proveedor no medible, los recuentos son los de
/// los orígenes medidos y la cobertura es <c>partial</c>, aunque M03 diga que el
/// importe está completo en moneda.
/// </remarks>
internal sealed class OrderPaymentReport(IServiceProvider services)
{
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

        // La capacidad se decide antes de leer: sin escritor acreditado no se
        // consulta nada que pudiera parecer un cero.
        var capabilities = port.Capabilities;
        if (Unaccredited(capabilities) is { } reason)
        {
            return PaymentIndicatorsView.Unmeasured(IndicatorStatus.NotMeasurable, reason, startUtc, endUtc);
        }

        var cohort = await port.ReadRegisteredBetweenAsync(startUtc, endUtc, cancellationToken);
        return Project(capabilities, cohort, startUtc, endUtc);
    }

    private static string? Unaccredited(PaymentCapabilities capabilities)
        => !capabilities.StaffWriterPresent ? NotMeasurableReasons.StaffWriterNotAccredited
            : !capabilities.SourceRecorded ? NotMeasurableReasons.SourceNotRecorded
            : null;

    /// <summary>Comprueba la respuesta y la reparte en los tres indicadores.</summary>
    internal static PaymentIndicatorsView Project(
        PaymentCapabilities capabilities, PaymentCohort cohort, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        MonetaryFacts.CheckPeriod(cohort.StartInclusive, cohort.EndExclusive, startUtc, endUtc);
        if (Unaccredited(capabilities) is { } reason)
        {
            return PaymentIndicatorsView.Unmeasured(IndicatorStatus.NotMeasurable, reason, startUtc, endUtc);
        }

        Check(cohort.Staff);
        Check(cohort.Provider);
        Check(cohort.Late);
        // Lo que M03 dice medir tiene que cuadrar con lo que dice registrar.
        if (cohort.Provider.Measured != capabilities.ProviderWriterPresent
            || (cohort.Late.Measured && !capabilities.LateRecorded))
        {
            throw new SourceIncoherentException("Medición que contradice las capacidades.");
        }
        if (!cohort.Staff.Measured)
        {
            return PaymentIndicatorsView.Unmeasured(IndicatorStatus.NotMeasurable, cohort.Staff.Reason, startUtc, endUtc);
        }

        if (cohort.AmountsByCurrency is null) throw new SourceIncoherentException("Sin importes.");
        foreach (var group in cohort.AmountsByCurrency)
        {
            if (group.CurrencyCode is null) throw new SourceIncoherentException("Importe sin moneda.");
            MonetaryFacts.CheckGroup(group.CurrencyCode, group.Count, group.Amount);
            if (group.Count == 0) throw new SourceIncoherentException("Moneda sin pagos.");
        }
        if (cohort.AmountsByCurrency.Select(g => g.CurrencyCode).Distinct().Count() != cohort.AmountsByCurrency.Count)
        {
            throw new SourceIncoherentException("Moneda repetida.");
        }
        if (!capabilities.CurrencyRecorded && cohort.AmountsByCurrency.Count > 0)
        {
            throw new SourceIncoherentException("Moneda sin columna de moneda.");
        }
        if (cohort.PaymentsWithoutCurrency < 0 || cohort.AmountsComplete != (cohort.PaymentsWithoutCurrency == 0))
        {
            throw new SourceIncoherentException("Cobertura de importes que no cuadra.");
        }

        var staff = cohort.Staff.Count!.Value;
        var provider = cohort.Provider.Count ?? 0;
        var measured = MonetaryFacts.Total([staff, provider]);
        var listed = MonetaryFacts.Total(cohort.AmountsByCurrency.Select(g => g.Count));
        if (MonetaryFacts.Total([listed, cohort.PaymentsWithoutCurrency]) != measured)
        {
            throw new SourceIncoherentException("Los importes no suman los pagos medidos.");
        }
        if (cohort.Late.Measured && cohort.Late.Count > measured)
        {
            throw new SourceIncoherentException("Más tardíos que pagos.");
        }

        var allSources = cohort.Provider.Measured;
        return new(startUtc, endUtc, cohort.ObservedAtUtc, null,
            I05(cohort, measured, allSources),
            I06(cohort, measured, allSources),
            I07(cohort.Late, allSources));
    }

    /// <summary>Medido: cuenta y sin motivo. No medible: sin cuenta y con motivo.</summary>
    private static void Check(PaymentMeasure measure)
    {
        if (measure is null) throw new SourceIncoherentException("Medida ausente.");
        var coherent = measure.Measured
            ? measure is { Count: >= 0, Reason: null }
            : measure is { Count: null, Reason.Length: > 0 };
        if (!coherent) throw new SourceIncoherentException("Medida incoherente.");
    }

    private static string Coverage(bool allSources) => allSources ? CurrencyCoverage.Complete : CurrencyCoverage.Partial;

    /// <summary>Solo con todos los orígenes medidos un cero dice «no hubo».</summary>
    private static string CountStatus(long count, bool allSources)
        => allSources && count == 0 ? IndicatorStatus.Empty : IndicatorStatus.WithData;

    private static PaymentCountView I05(PaymentCohort cohort, long measured, bool allSources)
        => new(CountStatus(measured, allSources), measured, Coverage(allSources),
        [
            Source("staff", cohort.Staff),
            Source("provider", cohort.Provider),
        ]);

    private static SourceMeasureView Source(string source, PaymentMeasure measure)
        => measure.Measured
            ? new(source, MeasureStatus.Measured, measure.Count, null)
            : new(source, MeasureStatus.NotMeasurable, null, measure.Reason);

    private static PaymentAmountsView I06(PaymentCohort cohort, long measured, bool allSources)
    {
        var without = cohort.PaymentsWithoutCurrency;
        if (cohort.AmountsByCurrency.Count == 0 && without > 0)
        {
            return new(IndicatorStatus.CurrencyNotAccredited, null, without, CurrencyCoverage.None, MethodBreakdown.NotPublished);
        }
        var byCurrency = cohort.AmountsByCurrency
            .OrderBy(g => g.CurrencyCode, StringComparer.Ordinal)
            .Select(g => new CurrencyAmountView(g.CurrencyCode, g.Count, MonetaryFacts.Format(g.Amount)))
            .ToArray();
        // «Completo» exige las dos cosas: todos los pagos con moneda y todos los orígenes medidos.
        var coverage = allSources && without == 0 ? CurrencyCoverage.Complete : CurrencyCoverage.Partial;
        return new(CountStatus(measured, allSources), byCurrency, without, coverage, MethodBreakdown.NotPublished);
    }

    private static LatePaymentsView I07(PaymentMeasure late, bool allSources)
        => late.Measured
            ? new(CountStatus(late.Count!.Value, allSources), late.Count, Coverage(allSources), IndicatorStatus.NotMeasurable)
            : new(IndicatorStatus.NotMeasurable, null, null, IndicatorStatus.NotMeasurable);
}

/// <summary>Estado de un recuento por origen.</summary>
public static class MeasureStatus
{
    /// <summary>Medido: su cero es un cero comprobado.</summary>
    public const string Measured = "measured";

    /// <summary>No medible: sin cifra.</summary>
    public const string NotMeasurable = "not_measurable";
}

/// <summary>El desglose por medio de pago.</summary>
public static class MethodBreakdown
{
    /// <summary>M03 no publica importes por medio: I06 va solo por moneda.</summary>
    public const string NotPublished = "not_published";
}

/// <summary>I05–I07 tal como los consume la pantalla. Cada indicador lleva su propio estado.</summary>
/// <param name="StartInclusive">Límite incluido, en UTC.</param>
/// <param name="EndExclusive">Límite excluido, en UTC.</param>
/// <param name="ObservedAtUtc">Instante en que ventas leyó; nulo si no se midió.</param>
/// <param name="NotMeasurableReason">Por qué ninguno se pudo medir (<see cref="NotMeasurableReasons"/>); nulo si se midió.</param>
/// <param name="I05">Cobros registrados.</param>
/// <param name="I06">Importe cobrado.</param>
/// <param name="I07">Cobros tardíos.</param>
public sealed record PaymentIndicatorsView(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset? ObservedAtUtc,
    string? NotMeasurableReason,
    PaymentCountView I05,
    PaymentAmountsView I06,
    LatePaymentsView I07)
{
    internal static PaymentIndicatorsView Unmeasured(string status, string? reason, DateTimeOffset startUtc, DateTimeOffset endUtc)
        => new(startUtc, endUtc, null, reason,
            new(status, null, null, null),
            new(status, null, null, null, MethodBreakdown.NotPublished),
            new(status, null, null, status));
}

/// <summary>I05: pagos de los orígenes medidos; cobertura <c>partial</c> si alguno no lo es.</summary>
public sealed record PaymentCountView(string Status, long? Count, string? Coverage, IReadOnlyList<SourceMeasureView>? BySource);

/// <summary>Un origen: <c>staff</c> o <c>provider</c>, medido o no, con su motivo.</summary>
public sealed record SourceMeasureView(string Source, string Status, long? Count, string? Reason);

/// <summary>I06: importe por moneda y pagos sin moneda contados aparte. Sin desglose por medio.</summary>
public sealed record PaymentAmountsView(
    string Status,
    IReadOnlyList<CurrencyAmountView>? ByCurrency,
    long? WithoutCurrencyCount,
    string? Coverage,
    string MethodBreakdown);

/// <summary>I07: pagos marcados tardíos al registrarse. M03 no publica su importe.</summary>
public sealed record LatePaymentsView(string Status, long? Count, string? Coverage, string AmountStatus);
