using System.Text.RegularExpressions;
using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Reports.Orders;

/// <summary>
/// Puerto consumidor de I05–I07, con la forma propuesta a M03 en L-NR-04.
/// </summary>
/// <remarks>
/// Interno por la misma razón que <see cref="IOrderValueAggregatePort"/>: M03 no
/// tiene todavía ni ruta de registro de cobros ni lector agregado, y M10 no
/// publica una firma propia. Nadie lo registra fuera de las pruebas: en
/// producción I05–I07 salen «no medible» (D-M10-11).
/// </remarks>
internal interface IOrderPaymentAggregatePort
{
    /// <summary>Pagos por <c>registered_at</c> en [inicio, fin).</summary>
    Task<OrderPaymentAggregate> ReadRegisteredBetweenAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken);
}

/// <summary>Si M03 puede registrar y leer cobros. Solo lo afirma M03.</summary>
internal enum OrderPaymentCapability
{
    NotMeasurable,
    Measurable,
}

/// <param name="StartInclusive">Límite incluido, en UTC; tiene que ser el pedido.</param>
/// <param name="EndExclusive">Límite excluido, en UTC; tiene que ser el pedido.</param>
/// <param name="ObservedAtUtc">Instante en que ventas leyó, según su reloj.</param>
/// <param name="Capability">Si M03 acredita poder registrar y leer cobros.</param>
/// <param name="RegisteredCount">Pagos, no pedidos; nulo si no es medible.</param>
/// <param name="Groups">Un grupo por método, origen, moneda y tardanza; nulo si no es medible.</param>
internal sealed record OrderPaymentAggregate(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset ObservedAtUtc,
    OrderPaymentCapability Capability,
    long? RegisteredCount,
    IReadOnlyList<OrderPaymentGroup>? Groups);

/// <param name="Method">Código de medio de M03 (<see cref="PaymentMethod"/>).</param>
/// <param name="Source"><c>staff</c>, <c>provider</c> o null si el pago no guarda su origen.</param>
/// <param name="CurrencyCode">ISO 4217 guardado en el pago; null si no lo tiene.</param>
/// <param name="WasLate">El valor congelado al registrar; nunca recalculado.</param>
/// <param name="Count">Pagos del grupo.</param>
/// <param name="Amount">Null si y solo si no hay moneda.</param>
internal sealed record OrderPaymentGroup(
    string Method, string? Source, string? CurrencyCode, bool WasLate, long Count, decimal? Amount);

/// <summary>
/// I05 cobros registrados, I06 importe por medio, I07 cobros tardíos (D-M10-04:
/// «cobros registrados», no ingresos). Ningún 0 sin capacidad acreditada.
/// </summary>
internal sealed partial class OrderPaymentReport(IServiceProvider services)
{
    /// <summary>Origen de un pago que no lo guarda.</summary>
    public const string UnrecordedSource = "unrecorded";

    private static readonly string[] SourceOrder = ["staff", "provider", UnrecordedSource];

    /// <summary>Lee los cobros del período. Un fallo o una respuesta que no cuadra se propagan.</summary>
    public async Task<PaymentIndicatorsView> ReadAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
    {
        var startUtc = startInclusive.ToUniversalTime();
        var endUtc = endExclusive.ToUniversalTime();

        if (services.GetService(typeof(IOrderAggregateReader)) is null)
        {
            return PaymentIndicatorsView.Unmeasured(IndicatorStatus.SourceUnavailable, startUtc, endUtc);
        }
        // Que exista sales.order_payments no hace operativo el registro de cobros:
        // sin el lector de M03 no hay capacidad acreditada (D-M10-11).
        if (services.GetService(typeof(IOrderPaymentAggregatePort)) is not IOrderPaymentAggregatePort port)
        {
            return PaymentIndicatorsView.Unmeasured(IndicatorStatus.NotMeasurable, startUtc, endUtc);
        }

        var aggregate = await port.ReadRegisteredBetweenAsync(startUtc, endUtc, cancellationToken);
        return Project(aggregate, startUtc, endUtc);
    }

    /// <summary>Comprueba la respuesta y la reparte en los tres indicadores.</summary>
    internal static PaymentIndicatorsView Project(OrderPaymentAggregate aggregate, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        MonetaryFacts.CheckPeriod(aggregate.StartInclusive, aggregate.EndExclusive, startUtc, endUtc);

        // Capacidad no acreditada o respuesta sin cobertura completa: no medible.
        // Lo que viniera además no se mira, para no dejar escapar un 0.
        if (aggregate is not { Capability: OrderPaymentCapability.Measurable, RegisteredCount: { } registered, Groups: { } groups })
        {
            return PaymentIndicatorsView.Unmeasured(IndicatorStatus.NotMeasurable, startUtc, endUtc);
        }

        foreach (var group in groups)
        {
            if (group.Method is null || !MethodCode().IsMatch(group.Method)) throw new SourceIncoherentException("Medio de pago sin código.");
            if (group.Source is not (null or "staff" or "provider")) throw new SourceIncoherentException("Origen desconocido.");
            MonetaryFacts.CheckGroup(group.CurrencyCode, group.Count, group.Amount);
        }
        if (groups.Select(g => (g.Method, g.Source, g.CurrencyCode, g.WasLate)).Distinct().Count() != groups.Count)
        {
            throw new SourceIncoherentException("Grupo repetido.");
        }
        if (MonetaryFacts.Total(groups.Select(g => g.Count)) != registered)
        {
            throw new SourceIncoherentException("Los grupos no suman los cobros registrados.");
        }

        var observed = aggregate.ObservedAtUtc;
        if (registered == 0)
        {
            return new(startUtc, endUtc, observed,
                new(IndicatorStatus.Empty, 0, []),
                new(IndicatorStatus.Empty, [], [], CurrencyCoverage.Complete),
                new(IndicatorStatus.Empty, 0, IndicatorStatus.Empty, [], 0));
        }

        var present = groups.Where(g => g.Count > 0).ToArray();
        return new(startUtc, endUtc, observed, I05(registered, present), I06(present), I07(present));
    }

    private static PaymentCountView I05(long registered, OrderPaymentGroup[] groups)
    {
        var bySource = groups
            .GroupBy(g => g.Source ?? UnrecordedSource)
            .OrderBy(g => Array.IndexOf(SourceOrder, g.Key))
            .Select(g => new SourceCountView(g.Key, MonetaryFacts.Total(g.Select(x => x.Count))))
            .ToArray();
        return new(IndicatorStatus.WithData, registered, bySource);
    }

    private static PaymentAmountsView I06(OrderPaymentGroup[] groups)
    {
        // Se juntan tardíos y a tiempo de un mismo medio, origen y moneda; nunca dos monedas.
        var byMethod = groups
            .Where(g => g.CurrencyCode is not null)
            .GroupBy(g => (g.Method, Source: g.Source ?? UnrecordedSource, Currency: g.CurrencyCode!))
            .OrderBy(g => g.Key.Method, StringComparer.Ordinal)
            .ThenBy(g => Array.IndexOf(SourceOrder, g.Key.Source))
            .ThenBy(g => g.Key.Currency, StringComparer.Ordinal)
            .Select(g => new MethodAmountView(g.Key.Method, g.Key.Source, g.Key.Currency,
                MonetaryFacts.Total(g.Select(x => x.Count)), MonetaryFacts.Format(Sum(g))))
            .ToArray();
        var withoutCurrency = groups
            .Where(g => g.CurrencyCode is null)
            .GroupBy(g => (g.Method, Source: g.Source ?? UnrecordedSource))
            .OrderBy(g => g.Key.Method, StringComparer.Ordinal)
            .ThenBy(g => Array.IndexOf(SourceOrder, g.Key.Source))
            .Select(g => new MethodCountView(g.Key.Method, g.Key.Source, MonetaryFacts.Total(g.Select(x => x.Count))))
            .ToArray();

        return byMethod.Length == 0
            ? new(IndicatorStatus.CurrencyNotAccredited, null, withoutCurrency, CurrencyCoverage.None)
            : new(IndicatorStatus.WithData, byMethod, withoutCurrency,
                withoutCurrency.Length == 0 ? CurrencyCoverage.Complete : CurrencyCoverage.Partial);
    }

    private static LatePaymentsView I07(OrderPaymentGroup[] groups)
    {
        var late = groups.Where(g => g.WasLate).ToArray();
        var lateCount = MonetaryFacts.Total(late.Select(g => g.Count));
        if (lateCount == 0)
        {
            return new(IndicatorStatus.Empty, 0, IndicatorStatus.Empty, [], 0);
        }
        var byCurrency = late
            .Where(g => g.CurrencyCode is not null)
            .GroupBy(g => g.CurrencyCode!)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new CurrencyAmountView(g.Key, MonetaryFacts.Total(g.Select(x => x.Count)), MonetaryFacts.Format(Sum(g))))
            .ToArray();
        var withoutCurrency = MonetaryFacts.Total(late.Where(g => g.CurrencyCode is null).Select(g => g.Count));

        // El recuento de tardíos se publica aunque su importe no tenga moneda.
        return byCurrency.Length == 0
            ? new(IndicatorStatus.WithData, lateCount, IndicatorStatus.CurrencyNotAccredited, null, withoutCurrency)
            : new(IndicatorStatus.WithData, lateCount, IndicatorStatus.WithData, byCurrency, withoutCurrency);
    }

    /// <summary>Suma de una sola moneda.</summary>
    private static decimal Sum(IEnumerable<OrderPaymentGroup> sameCurrency)
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

/// <summary>I05–I07 tal como los consume la pantalla. Cada indicador lleva su propio estado.</summary>
public sealed record PaymentIndicatorsView(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset? ObservedAtUtc,
    PaymentCountView I05,
    PaymentAmountsView I06,
    LatePaymentsView I07)
{
    internal static PaymentIndicatorsView Unmeasured(string status, DateTimeOffset startUtc, DateTimeOffset endUtc)
        => new(startUtc, endUtc, null,
            new(status, null, null),
            new(status, null, null, null),
            new(status, null, status, null, null));
}

/// <summary>I05: cuántos pagos se registraron, separados por origen.</summary>
public sealed record PaymentCountView(string Status, long? Count, IReadOnlyList<SourceCountView>? BySource);

/// <summary>Pagos de un origen: <c>staff</c>, <c>provider</c> o <c>unrecorded</c>.</summary>
public sealed record SourceCountView(string Source, long Count);

/// <summary>I06: importe por medio, origen y moneda; los pagos sin moneda, contados aparte.</summary>
public sealed record PaymentAmountsView(
    string Status,
    IReadOnlyList<MethodAmountView>? ByMethod,
    IReadOnlyList<MethodCountView>? WithoutCurrency,
    string? Coverage);

/// <summary>Importe de un medio y origen en una sola moneda.</summary>
public sealed record MethodAmountView(string Method, string Source, string CurrencyCode, long Count, string Amount);

/// <summary>Pagos de un medio y origen sin moneda: cuentan, no suman.</summary>
public sealed record MethodCountView(string Method, string Source, long Count);

/// <summary>I07: pagos marcados como tardíos al registrarse; el importe, solo con moneda.</summary>
public sealed record LatePaymentsView(
    string Status,
    long? Count,
    string AmountStatus,
    IReadOnlyList<CurrencyAmountView>? AmountByCurrency,
    long? WithoutCurrencyCount);
