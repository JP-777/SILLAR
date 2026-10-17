namespace Sillar.Modules.Reports.Orders;

/// <summary>
/// Contrato de lectura de los indicadores I01, I03 e I04 de M10.
/// No lee entidades, dinero ni datos personales.
/// </summary>
public interface IOrderIndicatorsReport
{
    /// <summary>
    /// Consulta una cohorte de creación [inicio, fin). Si no está registrado M03,
    /// devuelve SourceUnavailable y métricas nulas; nunca interpreta ausencia como cero.
    /// Un error del proveedor presente se propaga: no se disfraza de ausencia.
    /// </summary>
    Task<OrderIndicatorsResult> ReadAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken);
}

/// <summary>Disponibilidad del contrato agregado de M03 en esta instalación.</summary>
public enum OrderIndicatorsSourceStatus
{
    /// <summary>M03 no registró el proveedor: los indicadores no se pueden medir.</summary>
    SourceUnavailable,

    /// <summary>M03 respondió con una cohorte observada.</summary>
    Available
}

/// <summary>Conteo por estado comercial actual, no por estado histórico.</summary>
public sealed record ReportOrderStatusCount(string Status, long Count);

/// <summary>Resultado de lectura de una misma cohorte.</summary>
/// <param name="SourceStatus">Estado real de la fuente.</param>
/// <param name="StartInclusive">Límite temporal incluido, normalizado a UTC.</param>
/// <param name="EndExclusive">Límite temporal excluido, normalizado a UTC.</param>
/// <param name="ObservedAtUtc">Momento observado por M03, nulo sin fuente.</param>
/// <param name="I01CreatedOrders">Cantidad de pedidos creados; nulo sin fuente.</param>
/// <param name="I03CurrentStatusCounts">Distribución actual por estado; nula sin fuente.</param>
/// <param name="I04CurrentlyExpiredOrders">Cantidad en estado expired; nula sin fuente.</param>
/// <param name="ActiveOrders">Dimensión de actividad independiente; nula sin fuente.</param>
/// <param name="InactiveOrders">Bajas lógicas preservadas; nula sin fuente.</param>
public sealed record OrderIndicatorsResult(
    OrderIndicatorsSourceStatus SourceStatus,
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset? ObservedAtUtc,
    long? I01CreatedOrders,
    IReadOnlyList<ReportOrderStatusCount>? I03CurrentStatusCounts,
    long? I04CurrentlyExpiredOrders,
    long? ActiveOrders,
    long? InactiveOrders);
