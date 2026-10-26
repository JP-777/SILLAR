namespace Sillar.Modules.Sales.Contracts;

/// <summary>
/// Pagos registrados en el libro de M03, agregados para consumidores como M10.
/// </summary>
/// <remarks>
/// <para>
/// <b>Distingue lo comprobado de lo que no se puede medir.</b> Un recuento que M03 no
/// puede respaldar llega como <see cref="MeasureState.NotMeasurable"/> con
/// <c>Count = null</c>, nunca como cero: un cero afirmaría que no hubo nada, y lo único
/// que M03 sabe es que no lo registra.
/// </para>
/// <para>
/// Solo trabaja sobre esta base de datos. No publica pedidos, clientes ni trabajadores.
/// </para>
/// </remarks>
public interface IOrderPaymentAggregateReader
{
    /// <summary>
    /// Lo que el esquema de M03 registra hoy. Es constante del código, no se sondea la base.
    /// </summary>
    OrderPaymentAggregateCapabilities Capabilities { get; }

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

/// <summary>Qué hechos registra el libro de pagos de M03.</summary>
/// <param name="SourceRecorded">El origen staff/provider se guarda explícito por pago.</param>
/// <param name="CurrencyRecorded">Existe la columna de moneda. Puede estar vacía en pagos anteriores.</param>
/// <param name="LateRecorded">El pago tardío se guarda como hecho, no se recalcula.</param>
/// <param name="ProviderWriterPresent">Existe un escritor de pagos de proveedor. <b>Hoy no.</b></param>
public sealed record OrderPaymentAggregateCapabilities(
    bool SourceRecorded,
    bool CurrencyRecorded,
    bool LateRecorded,
    bool ProviderWriterPresent);

/// <summary>Si un valor está respaldado por lo que M03 registra.</summary>
public enum MeasureState
{
    /// <summary>Medido. Un cero aquí es un cero comprobado.</summary>
    Measured,

    /// <summary>No medible: el valor es <c>null</c> y no significa cero.</summary>
    NotMeasurable,
}

/// <summary>Por qué un valor no se puede medir.</summary>
public enum NotMeasurableReason
{
    /// <summary>Sí se pudo medir.</summary>
    None,

    /// <summary>No existe escritor de pagos de proveedor: su ausencia no prueba que no los hubo.</summary>
    ProviderWriterAbsent,

    /// <summary>Hay pagos sin moneda en la cohorte: el importe cobrado no se puede afirmar completo.</summary>
    CurrencyNotRecorded,
}

/// <summary>Un recuento que dice si está respaldado.</summary>
/// <param name="State">Medido o no medible.</param>
/// <param name="Count"><c>null</c> si y solo si no es medible.</param>
/// <param name="Reason"><see cref="NotMeasurableReason.None"/> si y solo si es medible.</param>
public sealed record PaymentCountMeasure(MeasureState State, long? Count, NotMeasurableReason Reason)
{
    /// <summary>Un recuento comprobado.</summary>
    public static PaymentCountMeasure Measured(long count) =>
        new(MeasureState.Measured, count, NotMeasurableReason.None);

    /// <summary>Un recuento que no se puede afirmar.</summary>
    public static PaymentCountMeasure NotMeasurable(NotMeasurableReason reason) =>
        new(MeasureState.NotMeasurable, null, reason);
}

/// <summary>Importe cobrado en una moneda.</summary>
/// <param name="CurrencyCode">Código ISO 4217.</param>
/// <param name="Count">Pagos en esa moneda.</param>
/// <param name="Amount">Suma de sus importes.</param>
public sealed record PaymentAmountByCurrency(string CurrencyCode, long Count, decimal Amount);

/// <summary>
/// Una cohorte de pagos por <c>registered_at</c>, un instante de lectura.
/// </summary>
/// <param name="StartInclusive">Límite inferior incluido, en UTC.</param>
/// <param name="EndExclusive">Límite superior excluido, en UTC.</param>
/// <param name="ObservedAtUtc">Instante del servidor tomado antes de la consulta.</param>
/// <param name="Staff">Pagos registrados por el personal. Siempre medible.</param>
/// <param name="Provider">Pagos de proveedor. No medible mientras no haya escritor.</param>
/// <param name="Late">Pagos con <c>was_late</c> guardado, de cualquier origen registrado.</param>
/// <param name="AmountsByCurrency">Importe por moneda de los pagos que la tienen, ordenado por código.</param>
/// <param name="PaymentsWithoutCurrency">Pagos sin moneda: <b>solo se cuentan</b>, su importe no se publica.</param>
/// <param name="Amounts">
/// Si el importe cobrado de la cohorte está completo. No medible en cuanto haya un solo
/// pago sin moneda; los grupos por moneda siguen siendo ciertos, pero no son el todo.
/// </param>
public sealed record OrderPaymentAggregateSnapshot(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset ObservedAtUtc,
    PaymentCountMeasure Staff,
    PaymentCountMeasure Provider,
    PaymentCountMeasure Late,
    IReadOnlyList<PaymentAmountByCurrency> AmountsByCurrency,
    long PaymentsWithoutCurrency,
    PaymentAmountsCoverage Amounts);

/// <summary>Cobertura del importe cobrado de una cohorte.</summary>
/// <param name="State">Medido si todos los pagos de la cohorte tienen moneda.</param>
/// <param name="Reason"><see cref="NotMeasurableReason.None"/> si y solo si es medible.</param>
public sealed record PaymentAmountsCoverage(MeasureState State, NotMeasurableReason Reason);
