using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>Un grupo de pedidos tal como lo devuelve PostgreSQL: moneda × actividad.</summary>
internal sealed record GrupoDeValor(string? Moneda, bool Activo, long Pedidos, decimal Total);

/// <summary>Un grupo de pagos tal como lo devuelve PostgreSQL: origen × moneda × tardío.</summary>
internal sealed record GrupoDePagos(string Origen, string? Moneda, bool Tardio, long Pagos, decimal Importe);

/// <summary>
/// Las reglas de los dos agregados monetarios, puras y sin base.
/// </summary>
/// <remarks>
/// <para>
/// <b>Separadas de la E/S por la misma razón que <c>ExternalCommercePolicy</c>:</b> que no
/// se suma lo que no tiene moneda, que no se cuenta cero lo que no se mide y que el
/// período es semiabierto se prueba en milisegundos. El lector solo agrupa en SQL y pasa
/// aquí un puñado de filas.
/// </para>
/// <para>
/// <b>Los grupos sin moneda pierden su importe aquí, a propósito.</b> SQL lo calcula
/// porque agrupa con todo lo demás; publicarlo sería ofrecer una suma sin unidad.
/// </para>
/// </remarks>
internal static class AgregadosMonetarios
{
    /// <summary>Lo que el esquema registra hoy. Cambia solo con una migración.</summary>
    public static readonly OrderPaymentAggregateCapabilities Capacidades = new(
        SourceRecorded: true,
        CurrencyRecorded: true,
        LateRecorded: true,
        ProviderWriterPresent: PaymentSource.Ratified.Contains(PaymentSource.Provider));

    /// <summary>Valida el período y lo lleva a UTC, que es lo que Npgsql exige para <c>timestamptz</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">El fin no es posterior al inicio.</exception>
    public static (DateTimeOffset Inicio, DateTimeOffset Fin) Periodo(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive)
    {
        if (endExclusive <= startInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endExclusive), "El fin debe ser posterior al inicio.");
        }

        return (startInclusive.ToUniversalTime(), endExclusive.ToUniversalTime());
    }

    public static OrderValueAggregateSnapshot ReducirValor(
        DateTimeOffset inicio, DateTimeOffset fin, DateTimeOffset observado,
        IReadOnlyCollection<GrupoDeValor> grupos)
    {
        var porMoneda = grupos
            .Where(g => g.Moneda is not null)
            .GroupBy(g => g.Moneda!, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new OrderValueByCurrency(
                g.Key,
                g.Where(x => x.Activo).Sum(x => x.Pedidos),
                g.Where(x => x.Activo).Sum(x => x.Total),
                g.Where(x => !x.Activo).Sum(x => x.Pedidos),
                g.Where(x => !x.Activo).Sum(x => x.Total)))
            .ToArray();

        var sinMoneda = new OrdersWithoutCurrency(
            grupos.Where(g => g.Moneda is null && g.Activo).Sum(g => g.Pedidos),
            grupos.Where(g => g.Moneda is null && !g.Activo).Sum(g => g.Pedidos));

        return new OrderValueAggregateSnapshot(
            inicio, fin, observado,
            grupos.Sum(g => g.Pedidos),
            porMoneda,
            sinMoneda);
    }

    public static OrderPaymentAggregateSnapshot ReducirPagos(
        DateTimeOffset inicio, DateTimeOffset fin, DateTimeOffset observado,
        IReadOnlyCollection<GrupoDePagos> grupos,
        OrderPaymentAggregateCapabilities capacidades)
    {
        var staff = PaymentCountMeasure.Measured(
            grupos.Where(g => g.Origen == PaymentSource.Staff).Sum(g => g.Pagos));

        // Sin escritor, la ausencia de filas no prueba que no hubo cobros de proveedor.
        var proveedor = capacidades.ProviderWriterPresent
            ? PaymentCountMeasure.Measured(
                grupos.Where(g => g.Origen == PaymentSource.Provider).Sum(g => g.Pagos))
            : PaymentCountMeasure.NotMeasurable(NotMeasurableReason.ProviderWriterAbsent);

        var tardios = PaymentCountMeasure.Measured(grupos.Where(g => g.Tardio).Sum(g => g.Pagos));

        var porMoneda = grupos
            .Where(g => g.Moneda is not null)
            .GroupBy(g => g.Moneda!, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new PaymentAmountByCurrency(g.Key, g.Sum(x => x.Pagos), g.Sum(x => x.Importe)))
            .ToArray();

        var sinMoneda = grupos.Where(g => g.Moneda is null).Sum(g => g.Pagos);

        var cobertura = sinMoneda == 0
            ? new PaymentAmountsCoverage(MeasureState.Measured, NotMeasurableReason.None)
            : new PaymentAmountsCoverage(MeasureState.NotMeasurable, NotMeasurableReason.CurrencyNotRecorded);

        return new OrderPaymentAggregateSnapshot(
            inicio, fin, observado,
            staff, proveedor, tardios,
            porMoneda, sinMoneda, cobertura);
    }
}

/// <summary>
/// Agrega exclusivamente <c>sales.orders</c>: una sola sentencia agrupa por moneda y
/// actividad, y en memoria solo se reducen esos grupos.
/// </summary>
internal sealed class LectorDeValorDePedidos(SalesDbContext database, TimeProvider clock)
    : IOrderValueAggregateReader
{
    /// <inheritdoc />
    public async Task<OrderValueAggregateSnapshot> ReadCreatedBetweenAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
    {
        var (inicio, fin) = AgregadosMonetarios.Periodo(startInclusive, endExclusive);
        var observado = clock.GetUtcNow();

        var grupos = await Consulta(inicio, fin).ToListAsync(cancellationToken);

        return AgregadosMonetarios.ReducirValor(inicio, fin, observado, grupos);
    }

    /// <summary>
    /// La consulta, expuesta para poder leer su SQL sin base. <b>No filtra
    /// <c>is_active</c></b>: las bajas se cuentan aparte, no se esconden.
    /// </summary>
    internal IQueryable<GrupoDeValor> Consulta(DateTimeOffset inicio, DateTimeOffset fin)
        => database.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= inicio && o.CreatedAt < fin)
            .GroupBy(o => new { o.CurrencyCode, o.IsActive })
            .Select(g => new GrupoDeValor(
                g.Key.CurrencyCode, g.Key.IsActive, g.LongCount(), g.Sum(o => o.TotalAmount)));
}

/// <summary>
/// Agrega exclusivamente <c>sales.order_payments</c> por <c>registered_at</c>.
/// </summary>
internal sealed class LectorAgregadoDePagos(SalesDbContext database, TimeProvider clock)
    : IOrderPaymentAggregateReader
{
    /// <inheritdoc />
    public OrderPaymentAggregateCapabilities Capabilities => AgregadosMonetarios.Capacidades;

    /// <inheritdoc />
    public async Task<OrderPaymentAggregateSnapshot> ReadRegisteredBetweenAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
    {
        var (inicio, fin) = AgregadosMonetarios.Periodo(startInclusive, endExclusive);
        var observado = clock.GetUtcNow();

        var grupos = await Consulta(inicio, fin).ToListAsync(cancellationToken);

        return AgregadosMonetarios.ReducirPagos(inicio, fin, observado, grupos, Capabilities);
    }

    /// <summary>La consulta, expuesta para poder leer su SQL sin base.</summary>
    internal IQueryable<GrupoDePagos> Consulta(DateTimeOffset inicio, DateTimeOffset fin)
        => database.OrderPayments
            .AsNoTracking()
            .Where(p => p.RegisteredAt >= inicio && p.RegisteredAt < fin)
            .GroupBy(p => new { p.Source, p.CurrencyCode, p.WasLate })
            .Select(g => new GrupoDePagos(
                g.Key.Source, g.Key.CurrencyCode, g.Key.WasLate, g.LongCount(), g.Sum(p => p.Amount)));
}

/// <summary>
/// Registro opcional. <b>Deliberadamente no se llama desde <c>SalesModule</c>:</b> los
/// dos contratos son candidatos hasta conciliarlos con su consumidor, y registrados en
/// el módulo quedarían disponibles para cualquiera por descuido.
/// </summary>
public static class CandidateSalesMonetaryRegistration
{
    /// <summary>Registra el lector de valor de pedidos y el de pagos.</summary>
    public static IServiceCollection AddCandidateSalesMonetaryReaders(this IServiceCollection services)
    {
        services.AddScoped<IOrderValueAggregateReader, LectorDeValorDePedidos>();
        services.AddScoped<IOrderPaymentAggregateReader, LectorAgregadoDePagos>();
        return services;
    }
}
