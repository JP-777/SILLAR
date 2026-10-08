using System.Globalization;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders.Data;
using Sillar.Shared.Data.Numbering;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Numbering;

internal static class ServiceOrderSettingsKeys
{
    public const string SeriesLabel = "service_orders.series_label";
}

internal sealed class ServiceOrderCodeAllocator(
    ServiceOrdersDbContext database,
    ISettingsReader settings,
    NodeIdentity node,
    TimeProvider clock)
{
    private static readonly TransactionalSeriesDefinition Series = new(
        ServiceOrdersDbContext.Schema,
        "service_order_series",
        "node_code",
        "year",
        "last_number");

    public string? ConfigurationError()
    {
        var label = settings.Get(ServiceOrderSettingsKeys.SeriesLabel);

        return IsConfigured(label)
            ? null
            : $"Configura el ajuste '{ServiceOrderSettingsKeys.SeriesLabel}' para este nodo antes de crear órdenes de servicio.";
    }

    private static bool IsConfigured(string? label)
        => !string.IsNullOrWhiteSpace(label)
           && !string.Equals(
               label.Trim(),
               "PENDIENTE_DEFINIR",
               StringComparison.OrdinalIgnoreCase);

    public async Task<string> NextAsync(CancellationToken cancellationToken)
    {
        // Se conserva antes de la configuración: ningún camino puede tocar el
        // contador sin compartir la transacción del alta de la orden.
        if (database.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "El código de orden de servicio se reserva dentro de la misma transacción que la orden; fuera de ella un rollback dejaría un hueco.");
        }

        var label = settings.Get(ServiceOrderSettingsKeys.SeriesLabel);
        if (!IsConfigured(label))
        {
            throw new InvalidOperationException(
                ConfigurationError()
                ?? $"Falta el ajuste '{ServiceOrderSettingsKeys.SeriesLabel}' para la serie propia de M05b.");
        }

        var year = TransactionalSeriesAllocator.YearInLima(clock.GetUtcNow());
        var number = await TransactionalSeriesAllocator.ReserveNextAsync(
            database, Series, node.Code, year, cancellationToken);
        return $"{label!.Trim()}-{year.ToString(CultureInfo.InvariantCulture)}-{number.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0')}";
    }
}
