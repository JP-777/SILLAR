using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Reports.Orders;

/// <summary>
/// Puerto consumidor de I02, con la forma propuesta a M03 en L-NR-04.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es interno a propósito.</b> M03 todavía no publica su contrato de valor de
/// pedidos y M10 no publica una segunda firma pública que pudiera divergir de la
/// suya. Cuando Claude A la publique en <c>Sales.Contracts</c>, este puerto se
/// sustituye por ella o se adapta uno a uno.
/// </para>
/// <para>
/// Nadie lo registra fuera de las pruebas: en producción I02 sale «no medible».
/// </para>
/// </remarks>
internal interface IOrderValueAggregatePort
{
    /// <summary>Pedidos por <c>created_at</c> en [inicio, fin), incluidos los inactivos.</summary>
    Task<OrderValueAggregate> ReadCreatedBetweenAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken);
}

/// <summary>Una cohorte de creación agrupada por la moneda guardada en cada pedido.</summary>
/// <param name="CreatedCount">Suma de los recuentos de los grupos; solo sirve para comprobar que cuadran.</param>
internal sealed record OrderValueAggregate(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset ObservedAtUtc,
    long CreatedCount,
    IReadOnlyList<OrderValueGroup> Groups);

/// <param name="CurrencyCode">ISO 4217 guardado en el pedido; null en los pedidos sin moneda.</param>
/// <param name="Amount">Suma del total congelado; null si y solo si no hay moneda.</param>
internal sealed record OrderValueGroup(string? CurrencyCode, long Count, decimal? Amount);

/// <summary>
/// I02 — valor de pedidos creados (D-M10-04): el total congelado de cada pedido,
/// no un cobro ni un ingreso. Una suma por moneda, nunca un total entre monedas.
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
        if (services.GetService(typeof(IOrderValueAggregatePort)) is not IOrderValueAggregatePort port)
        {
            return OrderValueView.Unmeasured(IndicatorStatus.NotMeasurable, startUtc, endUtc);
        }

        var aggregate = await port.ReadCreatedBetweenAsync(startUtc, endUtc, cancellationToken);
        return Project(aggregate, startUtc, endUtc);
    }

    /// <summary>
    /// Comprueba la respuesta y la traduce. Cada moneda va en su propia fila; los
    /// pedidos sin moneda se cuentan aparte y su importe no existe.
    /// </summary>
    internal static OrderValueView Project(OrderValueAggregate aggregate, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        MonetaryFacts.CheckPeriod(aggregate.StartInclusive, aggregate.EndExclusive, startUtc, endUtc);
        if (aggregate.Groups is null) throw new SourceIncoherentException("Sin grupos.");
        foreach (var group in aggregate.Groups)
        {
            MonetaryFacts.CheckGroup(group.CurrencyCode, group.Count, group.Amount);
        }
        // Una moneda, un grupo. Dos grupos de la misma moneda no se juntan aquí:
        // dicen que ventas agrupó mal, y lo que agrupó mal no se publica.
        if (aggregate.Groups.Select(g => g.CurrencyCode).Distinct().Count() != aggregate.Groups.Count)
        {
            throw new SourceIncoherentException("Moneda repetida.");
        }
        if (MonetaryFacts.Total(aggregate.Groups.Select(g => g.Count)) != aggregate.CreatedCount)
        {
            throw new SourceIncoherentException("Los grupos no suman los pedidos creados.");
        }

        if (aggregate.CreatedCount == 0)
        {
            return new(IndicatorStatus.Empty, startUtc, endUtc, aggregate.ObservedAtUtc, 0, [], 0, CurrencyCoverage.Complete);
        }

        var accredited = aggregate.Groups
            .Where(g => g is { CurrencyCode: not null, Count: > 0 })
            .OrderBy(g => g.CurrencyCode, StringComparer.Ordinal)
            .Select(g => new CurrencyAmountView(g.CurrencyCode!, g.Count, MonetaryFacts.Format(g.Amount!.Value)))
            .ToArray();
        var withoutCurrency = aggregate.Groups.Where(g => g.CurrencyCode is null).Sum(g => g.Count);

        return accredited.Length == 0
            ? new(IndicatorStatus.CurrencyNotAccredited, startUtc, endUtc, aggregate.ObservedAtUtc,
                aggregate.CreatedCount, null, withoutCurrency, CurrencyCoverage.None)
            : new(IndicatorStatus.WithData, startUtc, endUtc, aggregate.ObservedAtUtc,
                aggregate.CreatedCount, accredited, withoutCurrency,
                withoutCurrency == 0 ? CurrencyCoverage.Complete : CurrencyCoverage.Partial);
    }
}

/// <summary>I02 tal como lo consume la pantalla.</summary>
/// <param name="Status">Uno de <see cref="IndicatorStatus"/>.</param>
/// <param name="CreatedCount">Pedidos de la cohorte; nulo si no se pudo medir.</param>
/// <param name="ByCurrency">Una fila por moneda; nula si no hay ningún importe publicable.</param>
/// <param name="WithoutCurrencyCount">Pedidos sin moneda: cuentan, no suman.</param>
/// <param name="Coverage">Uno de <see cref="CurrencyCoverage"/>; nulo si no se pudo medir.</param>
public sealed record OrderValueView(
    string Status,
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset? ObservedAtUtc,
    long? CreatedCount,
    IReadOnlyList<CurrencyAmountView>? ByCurrency,
    long? WithoutCurrencyCount,
    string? Coverage)
{
    internal static OrderValueView Unmeasured(string status, DateTimeOffset startUtc, DateTimeOffset endUtc)
        => new(status, startUtc, endUtc, null, null, null, null, null);
}
