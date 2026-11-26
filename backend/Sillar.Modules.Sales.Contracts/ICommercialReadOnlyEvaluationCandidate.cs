namespace Sillar.Modules.Sales.Contracts;

/// <summary>
/// Candidato de FASE A: observar, sin efectos, los hechos comerciales de un pedido
/// antes de una futura liquidación externa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es una interfaz de observación, no de autorización.</b> Correlaciona la
/// declaración, lee el pedido y sus pagos, y los clasifica. Ningún desenlace
/// autoriza un cobro: el vocabulario solo tiene <see cref="CommercialReadOnlyEvaluationVerdict.Rejected"/>,
/// <see cref="CommercialReadOnlyEvaluationVerdict.PendingDecision"/> y
/// <see cref="CommercialReadOnlyEvaluationVerdict.Inconsistent"/>.
/// </para>
/// <para>
/// <b>M03 sigue siendo el dueño de las reglas.</b> Exponer la evaluación no traslada a
/// quien la consume la autoridad sobre pedidos, estados ni pagos. No se registra en
/// <c>SalesModule</c>, no publica endpoints y no escribe nada.
/// </para>
/// <para>
/// <b>No es un snapshot ni una prueba para después.</b> Las lecturas no comparten
/// transacción, y el pedido puede cambiar en cuanto se responde. Una operación que
/// escriba tendrá que releer y validar dentro de su propia transacción.
/// </para>
/// </remarks>
public interface ICommercialReadOnlyEvaluationCandidate
{
    /// <summary>
    /// Evalúa los hechos comerciales del pedido al que se refiere la declaración.
    /// </summary>
    /// <remarks>
    /// <b>No lanza por un pedido desconocido</b> —es una respuesta— pero <b>sí propaga
    /// un fallo real</b> de la base o una cancelación. Una respuesta interna imposible
    /// también se propaga como fallo, en vez de disfrazarse de rechazo comercial.
    /// </remarks>
    Task<CommercialReadOnlyEvaluationResult> EvaluateAsync(
        OrderCorrelationRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Qué se puede concluir. <b>No tiene ningún miembro positivo.</b></summary>
public enum CommercialReadOnlyEvaluationVerdict
{
    /// <summary>Respuesta cerrada: con estos hechos no hay liquidación posible.</summary>
    Rejected = 0,

    /// <summary>Falta una decisión que no es técnica. Repetir no ayuda; decidir sí.</summary>
    PendingDecision = 1,

    /// <summary>Los hechos leídos no concuerdan entre sí o con la correlación. M03 no elige cuál creer.</summary>
    Inconsistent = 2,
}

/// <summary>Por qué, en una palabra que se puede ramificar. Un valor por cada razón interna.</summary>
public enum CommercialReadOnlyEvaluationReason
{
    /// <summary>La declaración no trae nada autoritativo. Rechazo de correlación.</summary>
    NotAuthoritative = 0,

    /// <summary>La declaración no tiene la forma ratificada. Rechazo de correlación.</summary>
    MalformedReference = 1,

    /// <summary>Forma válida y no hay tal pedido en esta base. Rechazo de correlación.</summary>
    UnknownReference = 2,

    /// <summary>Código e identificador señalan pedidos distintos. Rechazo de correlación.</summary>
    DivergentDeclaration = 3,

    /// <summary>Pedido dado de baja lógica. Rechazo comercial.</summary>
    InactiveOrder = 4,

    /// <summary>Total imposible. Rechazo comercial.</summary>
    InvalidAmount = 5,

    /// <summary>Cancelado o entregado: su política ante un pago externo no está ratificada.</summary>
    OrderLifecycleUnratified = 6,

    /// <summary>
    /// Lo ya registrado cubre el total. <b>Cuenta todos los pagos, también los históricos
    /// sin origen</b>: es la semántica heredada de la pagabilidad. Cuáles son de quién está
    /// en <see cref="CommercialReadOnlyOrderFacts"/>.
    /// </summary>
    AlreadySettledManually = 7,

    /// <summary>No hay autoridad de moneda ratificada.</summary>
    CurrencyAuthorityMissing = 8,

    /// <summary>El pedido resuelto ya no estaba, o cambió de identidad, al leer sus hechos.</summary>
    OrderChangedBetweenReads = 9,

    /// <summary>Hechos que la base no debería permitir o que se contradicen.</summary>
    ContradictoryFacts = 10,

    /// <summary>La política respondió algo que la evaluación no sabe clasificar. Se queda cerrado.</summary>
    UnexpectedPolicyAnswer = 11,
}

/// <summary>Lo que se puede decir de la moneda guardada. <b>Nunca «autoritativa».</b></summary>
public enum CommercialReadOnlyCurrencyObservation
{
    /// <summary>No hay moneda guardada. Desconocida: no se supone ninguna.</summary>
    Absent = 0,

    /// <summary>Hay un valor sin forma ISO 4217. La base no debería permitirlo.</summary>
    MalformedValue = 1,

    /// <summary>Hay una moneda con forma válida, y nadie ratificó quién la escribe.</summary>
    PresentWithoutRatifiedAuthority = 2,
}

/// <summary>
/// Los hechos del pedido que la evaluación leyó, clasificados sin interpretarlos.
/// </summary>
/// <remarks>
/// <para>
/// <b>No hay saldo.</b> Restar importes cuya moneda no tiene autoridad afirmaría un
/// pendiente que nadie puede sostener, así que no se calcula ni se transporta.
/// </para>
/// <para>
/// <b>Los pagos van partidos por origen.</b> Un pago sin origen es histórico: no es del
/// personal ni de un proveedor. Un origen no ratificado no se cuenta como personal.
/// </para>
/// <para>
/// <b>Sin datos del cliente.</b> La identidad del pedido viaja en la correlación, y es
/// la misma: si no lo fuera, no habría hechos sino <see cref="CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads"/>.
/// </para>
/// </remarks>
/// <param name="Status">Estado leído, del vocabulario de <see cref="OrderStatus"/> o no.</param>
/// <param name="FrozenTotal">Total congelado. No es el pendiente.</param>
/// <param name="IsActive">Si sigue vivo o está dado de baja lógica.</param>
/// <param name="OrderCurrencyCode">La moneda guardada, tal cual, o <c>null</c>.</param>
/// <param name="OrderCurrency">Qué se puede decir de ella.</param>
/// <param name="StaffRecordedTotal">Suma de pagos con origen <see cref="PaymentSource.Staff"/>.</param>
/// <param name="StaffPaymentCount">Cuántos son.</param>
/// <param name="UnattributedHistoricalTotal">Suma de pagos sin origen.</param>
/// <param name="UnattributedHistoricalCount">Cuántos son.</param>
/// <param name="UnratifiedSourceTotal">Suma de pagos con un origen que la base no admite hoy.</param>
/// <param name="UnratifiedSourceCount">Cuántos son.</param>
/// <param name="PaymentsWithoutCurrencyCount">Pagos sin moneda guardada.</param>
public sealed record CommercialReadOnlyOrderFacts(
    string Status,
    decimal FrozenTotal,
    bool IsActive,
    string? OrderCurrencyCode,
    CommercialReadOnlyCurrencyObservation OrderCurrency,
    decimal StaffRecordedTotal,
    int StaffPaymentCount,
    decimal UnattributedHistoricalTotal,
    int UnattributedHistoricalCount,
    decimal UnratifiedSourceTotal,
    int UnratifiedSourceCount,
    int PaymentsWithoutCurrencyCount);

/// <summary>
/// Lo que M03 observó. <b>Una observación, no una autorización.</b>
/// </summary>
/// <param name="Verdict">Siempre negativo o pendiente.</param>
/// <param name="Reason">La razón exacta.</param>
/// <param name="Correlation">La respuesta de la correlación, tal como vino.</param>
/// <param name="Facts">
/// Los hechos leídos, o <c>null</c> si no se llegaron a leer. <b><c>null</c> no es cero:</b>
/// un pedido leído sin pagos trae ceros; uno no leído no trae hechos.
/// </param>
public sealed record CommercialReadOnlyEvaluationResult(
    CommercialReadOnlyEvaluationVerdict Verdict,
    CommercialReadOnlyEvaluationReason Reason,
    OrderCorrelationCandidate Correlation,
    CommercialReadOnlyOrderFacts? Facts);
