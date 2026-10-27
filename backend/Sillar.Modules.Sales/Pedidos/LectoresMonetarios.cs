using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>Un grupo de pedidos tal como lo devuelve PostgreSQL: moneda × actividad.</summary>
internal sealed record GrupoDeValor(string? Moneda, bool Activo, long Pedidos, decimal Total);

/// <summary>Un grupo de pagos tal como lo devuelve PostgreSQL: origen × medio × moneda × tardío.</summary>
internal sealed record GrupoDePagos(
    string Origen, string Medio, string? Moneda, bool Tardio, long Pagos, decimal Importe);

/// <summary>
/// Desde cuándo consta que un escritor registra <b>todos</b> los pagos de un origen.
/// <c>null</c> en <see cref="CubiertoDesdeUtc"/>: no acreditado.
/// </summary>
internal sealed record AcreditacionDeEscritor(string Origen, DateTimeOffset? CubiertoDesdeUtc);

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
    /// <summary>
    /// <b>Ningún escritor acreditado, de ningún origen.</b>
    /// </summary>
    /// <remarks>
    /// No es pesimismo: M03 no tiene hoy ninguna operación que cree un
    /// <c>OrderPayment</c>, ni del personal ni de un proveedor. Las filas que haya
    /// llegaron por otro camino y no prueban cobertura. Acreditar un origen es cambiar
    /// esta lista, con la fecha desde la que el escritor está en servicio, cuando exista.
    /// </remarks>
    public static readonly AcreditacionDeEscritor[] Acreditaciones =
        PaymentSource.All.Select(origen => new AcreditacionDeEscritor(origen, null)).ToArray();

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
        IReadOnlyCollection<AcreditacionDeEscritor> acreditaciones)
    {
        var observados = grupos
            .GroupBy(g => (g.Origen, g.Medio, g.Moneda, g.Tardio))
            .Select(g => new PaymentAggregateGroup(
                g.Key.Origen, g.Key.Medio, g.Key.Moneda, g.Key.Tardio,
                g.Sum(x => x.Pagos),
                // Sin moneda no hay unidad, y un importe sin unidad no se publica.
                g.Key.Moneda is null ? null : g.Sum(x => x.Importe)))
            .OrderBy(g => g.Source, StringComparer.Ordinal)
            .ThenBy(g => g.Method, StringComparer.Ordinal)
            .ThenBy(g => g.CurrencyCode is null)
            .ThenBy(g => g.CurrencyCode, StringComparer.Ordinal)
            .ThenBy(g => g.WasLate)
            .ToArray();

        var cobertura = new List<PaymentCoverage>();

        foreach (var origen in PaymentSource.All)
        {
            var desde = acreditaciones.SingleOrDefault(a => a.Origen == origen)?.CubiertoDesdeUtc;
            var sinMoneda = grupos.Any(g => g.Origen == origen && g.Moneda is null);

            foreach (var indicador in Enum.GetValues<PaymentIndicator>())
            {
                var motivo = desde is null ? NotMeasurableReason.WriterNotAccredited
                    : desde > inicio ? NotMeasurableReason.WindowStartsBeforeCoverage
                    : indicador == PaymentIndicator.AmountByCurrency && sinMoneda
                        ? NotMeasurableReason.CurrencyNotRecorded
                    : NotMeasurableReason.None;

                cobertura.Add(new PaymentCoverage(
                    origen, indicador,
                    motivo == NotMeasurableReason.None ? MeasureState.Measured : MeasureState.NotMeasurable,
                    motivo, desde));
            }
        }

        return new OrderPaymentAggregateSnapshot(inicio, fin, observado, observados, cobertura);
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
    public async Task<OrderPaymentAggregateSnapshot> ReadRegisteredBetweenAsync(
        DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
    {
        var (inicio, fin) = AgregadosMonetarios.Periodo(startInclusive, endExclusive);
        var observado = clock.GetUtcNow();

        var grupos = await Consulta(inicio, fin).ToListAsync(cancellationToken);

        return AgregadosMonetarios.ReducirPagos(
            inicio, fin, observado, grupos, AgregadosMonetarios.Acreditaciones);
    }

    /// <summary>La consulta, expuesta para poder leer su SQL sin base.</summary>
    internal IQueryable<GrupoDePagos> Consulta(DateTimeOffset inicio, DateTimeOffset fin)
        => database.OrderPayments
            .AsNoTracking()
            .Where(p => p.RegisteredAt >= inicio && p.RegisteredAt < fin)
            .GroupBy(p => new { p.Source, p.Method, p.CurrencyCode, p.WasLate })
            .Select(g => new GrupoDePagos(
                g.Key.Source, g.Key.Method, g.Key.CurrencyCode, g.Key.WasLate,
                g.LongCount(), g.Sum(p => p.Amount)));
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
