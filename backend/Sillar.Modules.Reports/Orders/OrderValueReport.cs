using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Reports.Orders;

/// <summary>
/// Puerto consumidor de I02. Refleja campo a campo <c>IOrderValueAggregateReader</c>
/// de M03 (candidato <c>77ffa20</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es interno a propósito.</b> La firma pública es la de M03; esta rama no la
/// tiene porque las ramas no están integradas, y M10 no copia contratos públicos.
/// El adaptador explícito está en <see cref="MonetarySources"/>: con el contrato
/// de M03 presente, I02 se lee de él; sin él, de este puerto, que solo registran
/// las pruebas.
/// </para>
/// </remarks>
internal interface IOrderValueAggregatePort
{
    /// <summary>Pedidos por <c>created_at</c> en [inicio, fin), incluidos los inactivos.</summary>
    Task<OrderValueCohort> ReadCreatedBetweenAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken);
}

/// <summary>Una cohorte de creación, como <c>OrderValueAggregateSnapshot</c>.</summary>
/// <param name="StartInclusive">Límite incluido, en UTC; tiene que ser el pedido.</param>
/// <param name="EndExclusive">Límite excluido, en UTC; tiene que ser el pedido.</param>
/// <param name="ObservedAtUtc">Instante en que ventas leyó, según su reloj.</param>
/// <param name="CohortCount">Todos los pedidos de la cohorte, con o sin moneda, activos o no.</param>
/// <param name="ByCurrency">Un grupo por moneda presente.</param>
/// <param name="WithoutCurrencyActive">Pedidos activos sin moneda: solo se cuentan.</param>
/// <param name="WithoutCurrencyInactive">Pedidos dados de baja sin moneda: solo se cuentan.</param>
internal sealed record OrderValueCohort(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset ObservedAtUtc,
    long CohortCount,
    IReadOnlyList<OrderValueCurrencyGroup> ByCurrency,
    long WithoutCurrencyActive,
    long WithoutCurrencyInactive);

/// <summary>Valor congelado de una moneda, con activos y bajas por separado: no se suman entre sí.</summary>
/// <param name="CurrencyCode">ISO 4217.</param>
/// <param name="ActiveCount">Pedidos activos.</param>
/// <param name="ActiveTotal">Suma de sus totales congelados.</param>
/// <param name="InactiveCount">Pedidos dados de baja.</param>
/// <param name="InactiveTotal">Suma de sus totales congelados.</param>
internal sealed record OrderValueCurrencyGroup(
    string CurrencyCode, long ActiveCount, decimal ActiveTotal, long InactiveCount, decimal InactiveTotal);

/// <summary>
/// I02 — valor de pedidos creados (D-M10-04): el total congelado de cada pedido,
/// no un cobro ni un ingreso. Una fila por moneda, nunca un total entre monedas,
/// y dentro de cada moneda activos y bajas por separado, como los publica M03.
/// </summary>
internal sealed class OrderValueReport(IServiceProvider services)
{
    /// <summary>Lee la cohorte. Un fallo o una respuesta que no cuadra se propagan.</summary>
    public async Task<OrderValueView> ReadAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
    {
        var startUtc = startInclusive.ToUniversalTime();
        var endUtc = endExclusive.ToUniversalTime();

        // Sin ventas no hay fuente; con ventas y sin lector de valor, ventas no
        // acredita la medición. Ninguno de los dos casos es un 0.
        if (services.GetService(typeof(IOrderAggregateReader)) is null)
        {
            return OrderValueView.Unmeasured(IndicatorStatus.SourceUnavailable, startUtc, endUtc);
        }
        if (MonetarySources.Value(services) is not { } port)
        {
            return OrderValueView.Unmeasured(IndicatorStatus.NotMeasurable, startUtc, endUtc);
        }

        var cohort = await port.ReadCreatedBetweenAsync(startUtc, endUtc, cancellationToken);
        return Project(cohort, startUtc, endUtc);
    }

    /// <summary>
    /// Comprueba la respuesta y la traduce. Cada moneda va en su propia fila; los
    /// pedidos sin moneda se cuentan aparte y su importe no existe.
    /// </summary>
    internal static OrderValueView Project(OrderValueCohort cohort, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        MonetaryFacts.CheckPeriod(cohort.StartInclusive, cohort.EndExclusive, startUtc, endUtc);
        if (cohort.ByCurrency is null) throw new SourceIncoherentException("Sin grupos.");
        foreach (var group in cohort.ByCurrency)
        {
            if (group.CurrencyCode is null) throw new SourceIncoherentException("Grupo de moneda sin moneda.");
            MonetaryFacts.CheckGroup(group.CurrencyCode, group.ActiveCount, group.ActiveTotal);
            MonetaryFacts.CheckGroup(group.CurrencyCode, group.InactiveCount, group.InactiveTotal);
            // M03 solo publica las monedas presentes: una fila vacía dice que agrupó mal.
            if (group.ActiveCount + group.InactiveCount == 0) throw new SourceIncoherentException("Moneda sin pedidos.");
        }
        // Una moneda, un grupo. Dos grupos de la misma moneda no se juntan aquí:
        // dicen que ventas agrupó mal, y lo que agrupó mal no se publica.
        if (cohort.ByCurrency.Select(g => g.CurrencyCode).Distinct().Count() != cohort.ByCurrency.Count)
        {
            throw new SourceIncoherentException("Moneda repetida.");
        }
        if (cohort.WithoutCurrencyActive < 0 || cohort.WithoutCurrencyInactive < 0)
        {
            throw new SourceIncoherentException("Recuento negativo.");
        }
        var withoutCurrency = MonetaryFacts.Total([cohort.WithoutCurrencyActive, cohort.WithoutCurrencyInactive]);
        var listed = MonetaryFacts.Total(cohort.ByCurrency.SelectMany(g => new[] { g.ActiveCount, g.InactiveCount }));
        if (MonetaryFacts.Total([listed, withoutCurrency]) != cohort.CohortCount)
        {
            throw new SourceIncoherentException("Los grupos no suman los pedidos de la cohorte.");
        }

        var without = new OrdersWithoutCurrencyView(cohort.WithoutCurrencyActive, cohort.WithoutCurrencyInactive);
        if (cohort.CohortCount == 0)
        {
            return new(IndicatorStatus.Empty, startUtc, endUtc, cohort.ObservedAtUtc, 0, [], without, CurrencyCoverage.Complete);
        }
        if (cohort.ByCurrency.Count == 0)
        {
            return new(IndicatorStatus.CurrencyNotAccredited, startUtc, endUtc, cohort.ObservedAtUtc,
                cohort.CohortCount, null, without, CurrencyCoverage.None);
        }

        var byCurrency = cohort.ByCurrency
            .OrderBy(g => g.CurrencyCode, StringComparer.Ordinal)
            .Select(g => new OrderValueCurrencyView(g.CurrencyCode, g.ActiveCount, MonetaryFacts.Format(g.ActiveTotal),
                g.InactiveCount, MonetaryFacts.Format(g.InactiveTotal)))
            .ToArray();
        return new(IndicatorStatus.WithData, startUtc, endUtc, cohort.ObservedAtUtc, cohort.CohortCount, byCurrency, without,
            withoutCurrency == 0 ? CurrencyCoverage.Complete : CurrencyCoverage.Partial);
    }
}

/// <summary>I02 tal como lo consume la pantalla.</summary>
/// <param name="Status">Uno de <see cref="IndicatorStatus"/>.</param>
/// <param name="StartInclusive">Límite incluido, en UTC.</param>
/// <param name="EndExclusive">Límite excluido, en UTC.</param>
/// <param name="ObservedAtUtc">Instante en que ventas leyó; nulo si no se pudo medir.</param>
/// <param name="CreatedCount">Pedidos de la cohorte; nulo si no se pudo medir.</param>
/// <param name="ByCurrency">Una fila por moneda; nula si no hay ningún importe publicable.</param>
/// <param name="WithoutCurrency">Pedidos sin moneda, activos y bajas: cuentan, no suman.</param>
/// <param name="Coverage">Uno de <see cref="CurrencyCoverage"/>; nulo si no se pudo medir.</param>
public sealed record OrderValueView(
    string Status,
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset? ObservedAtUtc,
    long? CreatedCount,
    IReadOnlyList<OrderValueCurrencyView>? ByCurrency,
    OrdersWithoutCurrencyView? WithoutCurrency,
    string? Coverage)
{
    internal static OrderValueView Unmeasured(string status, DateTimeOffset startUtc, DateTimeOffset endUtc)
        => new(status, startUtc, endUtc, null, null, null, null, null);
}

/// <summary>Valor de una moneda: activos y dados de baja por separado. Importes como texto decimal exacto.</summary>
public sealed record OrderValueCurrencyView(
    string CurrencyCode, long ActiveCount, string ActiveAmount, long InactiveCount, string InactiveAmount);

/// <summary>Pedidos sin moneda: solo se cuentan.</summary>
public sealed record OrdersWithoutCurrencyView(long Active, long Inactive);
