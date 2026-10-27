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
/// la rama de M03 (<c>014fba0</c>) y no en esta; las ramas no están integradas y M10
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

/// <summary>
/// I05–I07: copia campo a campo, salvo una diferencia declarada: el contrato de
/// M03 no acredita un escritor de pagos del personal, así que aquí es falso.
/// </summary>
internal sealed class SalesOrderPaymentAdapter(IOrderPaymentAggregateReader reader) : IOrderPaymentAggregatePort
{
    public PaymentCapabilities Capabilities => Map(reader.Capabilities);

    public async Task<PaymentCohort> ReadRegisteredBetweenAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
        => Map(await reader.ReadRegisteredBetweenAsync(startInclusive, endExclusive, cancellationToken));

    internal static PaymentCapabilities Map(OrderPaymentAggregateCapabilities capabilities)
        => new(capabilities.SourceRecorded, capabilities.CurrencyRecorded, capabilities.LateRecorded,
            capabilities.ProviderWriterPresent,
            // D-M10-11: Sales.Contracts no declara escritor de pagos del personal y
            // M03 no tiene ruta que registre pagos. Cambia cuando M03 lo acredite.
            StaffWriterPresent: false);

    internal static PaymentCohort Map(OrderPaymentAggregateSnapshot snapshot)
    {
        if (snapshot.AmountsByCurrency is null || snapshot.Amounts is null) throw new SourceIncoherentException("Cohorte incompleta.");
        var amountsComplete = Measured(snapshot.Amounts.State, snapshot.Amounts.Reason);
        return new(
            snapshot.StartInclusive,
            snapshot.EndExclusive,
            snapshot.ObservedAtUtc,
            Map(snapshot.Staff),
            Map(snapshot.Provider),
            Map(snapshot.Late),
            [.. snapshot.AmountsByCurrency.Select(g => new PaymentCurrencyGroup(g.CurrencyCode, g.Count, g.Amount))],
            snapshot.PaymentsWithoutCurrency,
            amountsComplete);
    }

    internal static PaymentMeasure Map(PaymentCountMeasure measure)
    {
        if (measure is null) throw new SourceIncoherentException("Medida ausente.");
        return Measured(measure.State, measure.Reason)
            ? new(true, measure.Count, null)
            : new(false, measure.Count, Reason(measure.Reason));
    }

    /// <summary>El estado y su motivo tienen que concordar: medido sin motivo, no medible con él.</summary>
    private static bool Measured(MeasureState state, NotMeasurableReason reason) => (state, reason) switch
    {
        (MeasureState.Measured, NotMeasurableReason.None) => true,
        (MeasureState.NotMeasurable, not NotMeasurableReason.None) => false,
        _ => throw new SourceIncoherentException("Estado y motivo que no concuerdan."),
    };

    private static string Reason(NotMeasurableReason reason) => reason switch
    {
        NotMeasurableReason.ProviderWriterAbsent => NotMeasurableReasons.ProviderWriterAbsent,
        NotMeasurableReason.CurrencyNotRecorded => NotMeasurableReasons.CurrencyNotRecorded,
        // Un motivo que M10 no conoce no se convierte en otro: no se publica.
        _ => throw new SourceIncoherentException("Motivo desconocido."),
    };
}
#endif
