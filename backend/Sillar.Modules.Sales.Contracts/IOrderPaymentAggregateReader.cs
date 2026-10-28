namespace Sillar.Modules.Sales.Contracts;

/// <summary>
/// Pagos registrados en el libro de M03, agregados para consumidores como M10.
/// </summary>
/// <remarks>
/// <para>
/// <b>Separa lo observado de lo que se puede afirmar.</b> <see cref="OrderPaymentAggregateSnapshot.Groups"/>
/// son las filas que hay, agrupadas; <see cref="OrderPaymentAggregateSnapshot.Coverage"/> dice, por
/// origen, indicador y ventana, si esas filas son <b>todo</b> lo que ocurrió. Sin un escritor
/// acreditado para un origen, que no haya filas no prueba que no hubo pagos, y lo que sí hay
/// no prueba que sea completo.
/// </para>
/// <para>
/// Solo trabaja sobre esta base de datos. No publica pedidos, clientes ni trabajadores.
/// </para>
/// </remarks>
public interface IOrderPaymentAggregateReader
{
    /// <summary>
    /// Agrega los pagos registrados en el intervalo semiabierto
    /// [<paramref name="startInclusive"/>, <paramref name="endExclusive"/>), en UTC,
    /// por <c>registered_at</c>.
    /// </summary>
    /// <remarks><b>Propaga un fallo real</b> de la base o una cancelación.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">El fin no es posterior al inicio.</exception>
    Task<OrderPaymentAggregateSnapshot> ReadRegisteredBetweenAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken);
}

/// <summary>Una ventana de pagos, un instante de lectura.</summary>
/// <param name="StartInclusive">Límite inferior incluido, en UTC.</param>
/// <param name="EndExclusive">Límite superior excluido, en UTC.</param>
/// <param name="ObservedAtUtc">Instante del servidor tomado antes de la consulta.</param>
/// <param name="Groups">
/// Filas observadas por origen + método + moneda + tardío, ordenadas. <b>Observadas no es
/// completas:</b> lo dice <paramref name="Coverage"/>.
/// </param>
/// <param name="Coverage">
/// Una entrada por cada origen de <see cref="PaymentSource.All"/> y cada
/// <see cref="PaymentIndicator"/>, para esta ventana.
/// </param>
public sealed record OrderPaymentAggregateSnapshot(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<PaymentAggregateGroup> Groups,
    IReadOnlyList<PaymentCoverage> Coverage);

/// <summary>Pagos observados con el mismo origen, método, moneda y condición de tardío.</summary>
/// <param name="Source">
/// Origen, de <see cref="PaymentSource"/>. <b><c>null</c>: pago histórico sin origen
/// registrado</b>, anterior a la columna. No se atribuye a ningún canal ni se cuenta en
/// la cobertura de ninguno.
/// </param>
/// <param name="Method">Medio, de <see cref="PaymentMethod"/>.</param>
/// <param name="CurrencyCode">ISO 4217, o <c>null</c> si la moneda es desconocida.</param>
/// <param name="WasLate">El hecho guardado <c>was_late</c>; no se recalcula.</param>
/// <param name="Count">Pagos del grupo.</param>
/// <param name="Amount">
/// Suma de importes. <b><c>null</c> cuando la moneda es desconocida:</b> un total sin unidad
/// no se publica.
/// </param>
public sealed record PaymentAggregateGroup(
    string? Source,
    string Method,
    string? CurrencyCode,
    bool WasLate,
    long Count,
    decimal? Amount);

/// <summary>Qué se mide sobre los pagos de un origen.</summary>
public enum PaymentIndicator
{
    /// <summary>Cuántos pagos se registraron.</summary>
    Count,

    /// <summary>Cuántos de ellos fueron tardíos (<c>was_late</c>).</summary>
    LateCount,

    /// <summary>Cuánto se cobró, por moneda.</summary>
    AmountByCurrency,
}

/// <summary>Si un valor está respaldado por lo que M03 registra.</summary>
public enum MeasureState
{
    /// <summary>Medido. Un cero aquí es un cero comprobado.</summary>
    Measured,

    /// <summary>No medible: lo observado no se puede presentar como completo, y su ausencia no es cero.</summary>
    NotMeasurable,
}

/// <summary>Por qué un valor no se puede medir.</summary>
public enum NotMeasurableReason
{
    /// <summary>Sí se pudo medir.</summary>
    None,

    /// <summary>No hay escritor acreditado para el origen: no consta que se registre todo, ni siquiera algo.</summary>
    WriterNotAccredited,

    /// <summary>El escritor está acreditado, pero desde después del inicio de la ventana.</summary>
    WindowStartsBeforeCoverage,

    /// <summary>Hay pagos del origen sin moneda en la ventana: el importe no se puede afirmar completo.</summary>
    CurrencyNotRecorded,

    /// <summary>
    /// Hay pagos históricos sin origen en la ventana: cualquiera de ellos podría ser de este
    /// origen, así que nada de él se puede afirmar completo.
    /// </summary>
    SourceNotRecorded,
}

/// <summary>Cobertura de un indicador, para un origen, en la ventana pedida.</summary>
/// <param name="Source">Origen, de <see cref="PaymentSource.All"/>.</param>
/// <param name="Indicator">Qué se mide.</param>
/// <param name="State">Medido o no medible.</param>
/// <param name="Reason"><see cref="NotMeasurableReason.None"/> si y solo si es medible.</param>
/// <param name="CoveredSinceUtc">
/// Desde cuándo está acreditado el escritor de ese origen, o <c>null</c> si no lo está.
/// </param>
public sealed record PaymentCoverage(
    string Source,
    PaymentIndicator Indicator,
    MeasureState State,
    NotMeasurableReason Reason,
    DateTimeOffset? CoveredSinceUtc);
