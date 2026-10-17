using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Reports.Orders;

/// <summary>
/// Consumidor M10 de agregados públicos de M03. Resuelve una dependencia
/// opcional: M10 puede existir sin M03; nunca consulta directamente sales.*.
/// </summary>
public sealed class OrderIndicatorsReport(IServiceProvider services) : IOrderIndicatorsReport
{
    /// <inheritdoc />
    public async Task<OrderIndicatorsResult> ReadAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        if (endExclusive <= startInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endExclusive), "El fin debe ser posterior al inicio.");
        }

        var startUtc = startInclusive.ToUniversalTime();
        var endUtc = endExclusive.ToUniversalTime();

        // Consulta el contenedor, no un schema ajeno ni un detector de capacidades.
        // La dependencia es blanda: si M03 está ausente no se inventan ceros.
        if (services.GetService(typeof(IOrderAggregateReader)) is not IOrderAggregateReader source)
        {
            return new OrderIndicatorsResult(
                OrderIndicatorsSourceStatus.SourceUnavailable,
                startUtc, endUtc,
                null, null, null, null, null, null);
        }

        var snapshot = await source.ReadCreatedBetweenAsync(
            startUtc, endUtc, cancellationToken);

        // Copiar solo recuentos y el instante del propietario; nunca materializar
        // pedidos, contactos, importes, pagos ni identificadores de pedidos.
        return new OrderIndicatorsResult(
            OrderIndicatorsSourceStatus.Available,
            snapshot.StartInclusive,
            snapshot.EndExclusive,
            snapshot.ObservedAtUtc,
            snapshot.CreatedCount,
            snapshot.CurrentStatusCounts
                .Select(x => new ReportOrderStatusCount(x.Status, x.Count))
                .ToArray(),
            snapshot.CurrentlyExpiredCount,
            snapshot.ActivityCounts.Active,
            snapshot.ActivityCounts.Inactive);
    }
}

/// <summary>Inscripción explícita del lector consumidor en DI de M10.</summary>
public static class OrderIndicatorsReportRegistration
{
    /// <summary>
    /// Registra únicamente la capa lectora; no añade dependencia dura de M03
    /// ni registra rutas, instalación, tareas de fondo o migraciones.
    /// </summary>
    public static IServiceCollection AddOrderIndicatorsReport(this IServiceCollection services)
    {
        services.AddScoped<IOrderIndicatorsReport, OrderIndicatorsReport>();
        return services;
    }
}
