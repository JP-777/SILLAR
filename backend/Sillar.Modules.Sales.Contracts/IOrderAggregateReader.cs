namespace Sillar.Modules.Sales.Contracts;

/// <summary>
/// Lectura agregada de pedidos de M03 para módulos consumidores como M10.
/// Solo trabaja sobre esta base de datos: no agrega ni sincroniza nodos.
/// No publica clientes, identificadores de pedidos, pagos ni importes.
/// </summary>
public interface IOrderAggregateReader
{
    /// <summary>
    /// Cuenta pedidos por creación en el intervalo semiabierto [startInclusive, endExclusive),
    /// sin filtrar bajas lógicas. El estado y la actividad se observan al consultar,
    /// no se reconstruyen como estaban cuando se creó cada pedido.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">El fin no es posterior al inicio.</exception>
    Task<OrderAggregateSnapshot> ReadCreatedBetweenAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken);
}

/// <summary>
/// Una sola cohorte y un solo instante de lectura. Los recuentos por estado y
/// actividad son distribuciones separadas: nunca se suman entre dimensiones.
/// </summary>
/// <param name="StartInclusive">Límite inferior incluido (UTC).</param>
/// <param name="EndExclusive">Límite superior excluido (UTC).</param>
/// <param name="ObservedAtUtc">Instante del servidor capturado antes de la consulta.</param>
/// <param name="CreatedCount">I01: todos los pedidos creados en la cohorte, incluso inactivos.</param>
/// <param name="CurrentStatusCounts">I03: siete estados actuales, incluidos los que cuentan cero.</param>
/// <param name="CurrentlyExpiredCount">I04: pedidos cuyo estado actual es expired.</param>
/// <param name="ActivityCounts">Distribución independiente activo/inactivo, sin excluir historia.</param>
public sealed record OrderAggregateSnapshot(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset ObservedAtUtc,
    long CreatedCount,
    IReadOnlyList<OrderStatusCount> CurrentStatusCounts,
    long CurrentlyExpiredCount,
    OrderActivityCounts ActivityCounts);

/// <summary>Recuento de pedidos de la cohorte cuyo estado actual es Status.</summary>
public sealed record OrderStatusCount(string Status, long Count);

/// <summary>Partición de la cohorte por baja lógica; no es estado comercial.</summary>
public sealed record OrderActivityCounts(long Active, long Inactive);
