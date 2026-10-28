#if SALES_MONETARY_CONTRACTS
using Sillar.Modules.Sales.Contracts;
#endif

namespace Sillar.Modules.Reports.Orders;

/// <summary>
/// Dónde lee M10 los agregados monetarios: el único punto de adaptación entre la
/// firma pública de M03 y los puertos internos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué hay compilación condicional.</b> Los contratos
/// <c>IOrderValueAggregateReader</c> e <c>IOrderPaymentAggregateReader</c> están en
/// la rama de M03 (<c>30cb8c3</c>) y no en esta; las ramas no están integradas y M10
/// no los copia. Los adaptadores se compilan con
/// <c>-p:SalesMonetaryContracts=true</c> sobre un árbol que los tenga. Al integrar,
/// la condición se retira y queda el código.
/// </para>
/// <para>
/// Sin contrato de M03 en el contenedor, se usa el puerto interno, que solo
/// registran las pruebas: en producción I02 e I05–I07 salen «no medible».
/// </para>
/// </remarks>
internal static class MonetarySources
{
    /// <summary>Lector de I02, o null si ventas no lo publica.</summary>
    public static IOrderValueAggregatePort? Value(IServiceProvider services)
    {
#if SALES_MONETARY_CONTRACTS
        if (services.GetService(typeof(IOrderValueAggregateReader)) is IOrderValueAggregateReader reader)
        {
            return new SalesOrderValueAdapter(reader);
        }
#endif
        return services.GetService(typeof(IOrderValueAggregatePort)) as IOrderValueAggregatePort;
    }

    /// <summary>Lector de I05–I07, o null si ventas no lo publica.</summary>
    public static IOrderPaymentAggregatePort? Payments(IServiceProvider services)
    {
#if SALES_MONETARY_CONTRACTS
        if (services.GetService(typeof(IOrderPaymentAggregateReader)) is IOrderPaymentAggregateReader reader)
        {
            return new SalesOrderPaymentAdapter(reader);
        }
#endif
        return services.GetService(typeof(IOrderPaymentAggregatePort)) as IOrderPaymentAggregatePort;
    }
}

#if SALES_MONETARY_CONTRACTS
/// <summary>I02: copia campo a campo; no decide nada que no decida M03.</summary>
internal sealed class SalesOrderValueAdapter(IOrderValueAggregateReader reader) : IOrderValueAggregatePort
{
    public async Task<OrderValueCohort> ReadCreatedBetweenAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
        => Map(await reader.ReadCreatedBetweenAsync(startInclusive, endExclusive, cancellationToken));

    internal static OrderValueCohort Map(OrderValueAggregateSnapshot snapshot)
    {
        if (snapshot.ByCurrency is null || snapshot.WithoutCurrency is null) throw new SourceIncoherentException("Cohorte incompleta.");
        return new(
            snapshot.StartInclusive,
            snapshot.EndExclusive,
            snapshot.ObservedAtUtc,
            snapshot.CohortCount,
            [.. snapshot.ByCurrency.Select(g => new OrderValueCurrencyGroup(
                g.CurrencyCode, g.ActiveCount, g.ActiveFrozenTotal, g.InactiveCount, g.InactiveFrozenTotal))],
            snapshot.WithoutCurrency.Active,
            snapshot.WithoutCurrency.Inactive);
    }
}

/// <summary>I05–I07: copia campo a campo; los enumerados pasan al vocabulario de M10.</summary>
internal sealed class SalesOrderPaymentAdapter(IOrderPaymentAggregateReader reader) : IOrderPaymentAggregatePort
{
    public async Task<PaymentWindow> ReadRegisteredBetweenAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
        => Map(await reader.ReadRegisteredBetweenAsync(startInclusive, endExclusive, cancellationToken));

    internal static PaymentWindow Map(OrderPaymentAggregateSnapshot snapshot)
    {
        if (snapshot.Groups is null || snapshot.Coverage is null) throw new SourceIncoherentException("Ventana incompleta.");
        return new(
            snapshot.StartInclusive,
            snapshot.EndExclusive,
            snapshot.ObservedAtUtc,
            [.. snapshot.Groups.Select(g => g is null
                ? throw new SourceIncoherentException("Grupo ausente.")
                : new PaymentGroup(g.Source, g.Method, g.CurrencyCode, g.WasLate, g.Count, g.Amount))],
            [.. snapshot.Coverage.Select(Map)]);
    }

    internal static PaymentCoverageEntry Map(PaymentCoverage coverage)
    {
        if (coverage is null) throw new SourceIncoherentException("Cobertura ausente.");
        var measured = (coverage.State, coverage.Reason) switch
        {
            (MeasureState.Measured, NotMeasurableReason.None) => true,
            (MeasureState.NotMeasurable, not NotMeasurableReason.None) => false,
            _ => throw new SourceIncoherentException("Estado y motivo que no concuerdan."),
        };
        return new(coverage.Source, Indicator(coverage.Indicator), measured,
            measured ? null : Reason(coverage.Reason), coverage.CoveredSinceUtc);
    }

    private static string Indicator(PaymentIndicator indicator) => indicator switch
    {
        PaymentIndicator.Count => PaymentIndicators.Count,
        PaymentIndicator.LateCount => PaymentIndicators.LateCount,
        PaymentIndicator.AmountByCurrency => PaymentIndicators.AmountByCurrency,
        _ => throw new SourceIncoherentException("Indicador desconocido."),
    };

    // Un motivo que M10 no conoce no se convierte en otro: no se publica.
    private static string Reason(NotMeasurableReason reason) => reason switch
    {
        NotMeasurableReason.WriterNotAccredited => NotMeasurableReasons.WriterNotAccredited,
        NotMeasurableReason.WindowStartsBeforeCoverage => NotMeasurableReasons.WindowStartsBeforeCoverage,
        NotMeasurableReason.CurrencyNotRecorded => NotMeasurableReasons.CurrencyNotRecorded,
        NotMeasurableReason.SourceNotRecorded => NotMeasurableReasons.SourceNotRecorded,
        _ => throw new SourceIncoherentException("Motivo desconocido."),
    };
}
#endif
