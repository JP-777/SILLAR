using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Domain;

namespace Sillar.Modules.Sales.Commercial;

/// <summary>
/// Qué puede concluir la evaluación. <b>No tiene ningún miembro positivo.</b>
/// </summary>
/// <remarks>
/// No es un descuido que falte «elegible» o «aplicable»: la evaluación clasifica
/// hechos y nunca autoriza una liquidación. Un miembro positivo, aunque fuera
/// inalcanzable, sería una puerta que alguien acabaría abriendo para ver verde.
/// </remarks>
internal enum CommercialEvaluationVerdict
{
    /// <summary>Respuesta cerrada: con estos hechos no hay liquidación posible.</summary>
    Rejected,

    /// <summary>Falta una decisión que no es técnica. Repetir no ayuda; decidir sí.</summary>
    PendingDecision,

    /// <summary>
    /// Los hechos leídos no concuerdan entre sí o con la correlación. <b>No se elige
    /// cuál creer:</b> cualquier clasificación sobre ellos sería adivinar.
    /// </summary>
    Inconsistent,
}

/// <summary>Por qué, en una palabra que se puede ramificar.</summary>
internal enum CommercialEvaluationReason
{
    /// <summary>La declaración no trae nada autoritativo. Viene de la correlación.</summary>
    NotAuthoritative,

    /// <summary>La declaración no tiene la forma ratificada. Viene de la correlación.</summary>
    MalformedReference,

    /// <summary>La forma es válida y no hay tal pedido. Viene de la correlación.</summary>
    UnknownReference,

    /// <summary>Código e identificador señalan pedidos distintos. Viene de la correlación.</summary>
    DivergentDeclaration,

    /// <summary>Pedido dado de baja lógica. Viene de la pagabilidad.</summary>
    InactiveOrder,

    /// <summary>Total imposible. Viene de la pagabilidad.</summary>
    InvalidAmount,

    /// <summary>Cancelado o entregado: su política no está ratificada. Viene de la pagabilidad.</summary>
    OrderLifecycleUnratified,

    /// <summary>Lo ya registrado cubre el total. Viene de la pagabilidad.</summary>
    AlreadySettledManually,

    /// <summary>No hay autoridad de moneda ratificada. Viene de la pagabilidad.</summary>
    CurrencyAuthorityMissing,

    /// <summary>
    /// El pedido que resolvió la correlación ya no está, o cambió de identidad, al
    /// leer sus hechos. <b>Es una observación, no una garantía de snapshot.</b>
    /// </summary>
    OrderChangedBetweenReads,

    /// <summary>
    /// Hechos que la base no debería permitir o que se contradicen: estado fuera del
    /// vocabulario, importe negativo, moneda con forma inválida o distinta entre
    /// pedido y pagos, u origen de pago no ratificado.
    /// </summary>
    ContradictoryFacts,

    /// <summary>
    /// La política devolvió algo que esta evaluación no sabe clasificar —incluido un
    /// positivo—. Se queda cerrado en lugar de traducirlo.
    /// </summary>
    UnexpectedPolicyAnswer,
}

/// <summary>Lo que se puede decir de la moneda observada. <b>Nunca «autoritativa».</b></summary>
internal enum CurrencyObservation
{
    /// <summary>No hay moneda guardada. Se queda desconocida: no se supone ninguna.</summary>
    Absent,

    /// <summary>Hay un valor y no tiene la forma ISO 4217. La base no debería permitirlo.</summary>
    MalformedValue,

    /// <summary>
    /// Hay una moneda con forma válida, y <b>nadie ratificó quién la escribe</b>.
    /// Estar presente no la convierte en autoridad.
    /// </summary>
    PresentWithoutRatifiedAuthority,
}

/// <summary>Las columnas del pedido que la evaluación lee, y ninguna más.</summary>
internal sealed record OrderFactsRecord(
    Guid OrderId,
    string OrderCode,
    string Status,
    decimal TotalAmount,
    string? CurrencyCode,
    bool IsActive);

/// <summary>Las columnas de un pago que la evaluación lee: ni quién cobró ni la referencia.</summary>
internal sealed record PaymentFactRecord(decimal Amount, string? Source, string? CurrencyCode);

/// <summary>
/// Los hechos comerciales leídos, clasificados sin interpretarlos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Total, pagos y saldo van separados</b>, y el saldo no se calcula: restar
/// importes cuya moneda no tiene autoridad sería afirmar un pendiente que nadie
/// puede sostener.
/// </para>
/// <para>
/// <b>Los pagos se parten por origen.</b> Un pago sin origen es un histórico
/// anterior a la columna: no se cuenta como del personal, porque eso inventaría a
/// la persona que lo registró, y tampoco como de proveedor.
/// </para>
/// </remarks>
/// <param name="OrderId">El pedido.</param>
/// <param name="OrderCode">Su código visible, tal como está guardado.</param>
/// <param name="Status">Estado leído, del vocabulario de <see cref="OrderStatus"/> o no.</param>
/// <param name="FrozenTotal">Total congelado. No es el pendiente.</param>
/// <param name="IsActive">Si sigue vivo o está dado de baja lógica.</param>
/// <param name="OrderCurrencyCode">La moneda guardada en el pedido, tal cual, o <c>null</c>.</param>
/// <param name="OrderCurrency">Qué se puede decir de ella.</param>
/// <param name="StaffRecordedTotal">Suma de pagos con origen <see cref="PaymentSource.Staff"/>.</param>
/// <param name="StaffPaymentCount">Cuántos son.</param>
/// <param name="UnattributedHistoricalTotal">Suma de pagos sin origen: históricos.</param>
/// <param name="UnattributedHistoricalCount">Cuántos son.</param>
/// <param name="UnratifiedSourceTotal">Suma de pagos con un origen que la base no admite hoy.</param>
/// <param name="UnratifiedSourceCount">Cuántos son.</param>
/// <param name="PaymentsWithoutCurrencyCount">Pagos sin moneda guardada.</param>
internal sealed record CommercialFactsObservation(
    Guid OrderId,
    string OrderCode,
    string Status,
    decimal FrozenTotal,
    bool IsActive,
    string? OrderCurrencyCode,
    CurrencyObservation OrderCurrency,
    decimal StaffRecordedTotal,
    int StaffPaymentCount,
    decimal UnattributedHistoricalTotal,
    int UnattributedHistoricalCount,
    decimal UnratifiedSourceTotal,
    int UnratifiedSourceCount,
    int PaymentsWithoutCurrencyCount)
{
    /// <summary>Todo lo registrado, sea cual sea su origen. No es lo liquidado por un proveedor.</summary>
    public decimal RecordedPaymentsTotal => StaffRecordedTotal + UnattributedHistoricalTotal + UnratifiedSourceTotal;

    /// <summary>
    /// <b>Siempre <c>null</c>.</b> El saldo no es derivable mientras la moneda no tenga
    /// autoridad ratificada.
    /// </summary>
    public decimal? DerivableBalance => null;
}

/// <summary>
/// Lo que la evaluación observó. <b>Es una observación, no una autorización.</b>
/// </summary>
/// <remarks>
/// El pedido puede cambiar en cuanto esto se devuelve. Una futura operación que
/// escriba tendrá que releer y validar dentro de su propia transacción: este
/// resultado no sirve como prueba de nada que ocurra después.
/// </remarks>
/// <param name="Verdict">Qué se puede concluir, siempre negativo o pendiente.</param>
/// <param name="Reason">Por qué.</param>
/// <param name="Correlation">La respuesta de la correlación, tal como vino.</param>
/// <param name="Payability">
/// La respuesta de <see cref="ExternalCommercePolicy.EvaluarPagabilidad"/> sobre los
/// hechos leídos, o <c>null</c> si no se llegó a preguntar.
/// </param>
/// <param name="Facts">Los hechos leídos, o <c>null</c> si no hubo pedido que leer.</param>
internal sealed record CommercialEvaluationObservation(
    CommercialEvaluationVerdict Verdict,
    CommercialEvaluationReason Reason,
    OrderCorrelationCandidate Correlation,
    PayableOrderCandidate? Payability,
    CommercialFactsObservation? Facts)
{
    /// <summary>
    /// <b>Siempre <c>false</c>.</b> M03 no tiene productor de declaraciones autenticadas
    /// de proveedor, y no lo suple con un dato que traiga quien llama.
    /// </summary>
    public bool ProviderDeclarationAuthenticated => false;

    /// <summary><b>Siempre <c>false</c>.</b> Ninguna evaluación autoriza una liquidación.</summary>
    public bool AuthorizesSettlement => false;
}

/// <summary>
/// Clasifica los hechos leídos, como reglas puras.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sin base, sin reloj y sin estado</b>, igual que las otras dos políticas: la
/// clasificación se provoca en milisegundos y la lectura queda reducida a traer filas.
/// </para>
/// <para>
/// <b>No decide nada que no esté ya decidido.</b> La pagabilidad la sigue diciendo
/// <see cref="ExternalCommercePolicy.EvaluarPagabilidad"/>, con el mismo snapshot que
/// le da <see cref="CandidatePayableOrderReader"/>. Lo único propio de aquí es negarse
/// a clasificar cuando los hechos no concuerdan.
/// </para>
/// </remarks>
internal static class CommercialEvaluationPolicy
{
    /// <summary>
    /// La correlación no resolvió un pedido: no hay hechos que leer.
    /// </summary>
    /// <param name="correlacion">Lo que respondió la correlación.</param>
    public static CommercialEvaluationObservation SinPedido(OrderCorrelationCandidate correlacion)
    {
        ArgumentNullException.ThrowIfNull(correlacion);

        return correlacion.Outcome switch
        {
            OrderCorrelationOutcome.NotAuthoritative => Rechaza(correlacion, CommercialEvaluationReason.NotAuthoritative),
            OrderCorrelationOutcome.MalformedReference => Rechaza(correlacion, CommercialEvaluationReason.MalformedReference),
            OrderCorrelationOutcome.UnknownReference => Rechaza(correlacion, CommercialEvaluationReason.UnknownReference),
            OrderCorrelationOutcome.DivergentDeclaration => Rechaza(correlacion, CommercialEvaluationReason.DivergentDeclaration),

            // Un «resuelto» que llega aquí, o un desenlace que no existía al escribir
            // esto: ninguno de los dos se traduce a algo mejor.
            _ => Incoherente(correlacion, CommercialEvaluationReason.UnexpectedPolicyAnswer, null, null),
        };
    }

    /// <summary>
    /// Clasifica los hechos de un pedido que la correlación resolvió.
    /// </summary>
    /// <remarks>
    /// <b>El orden es la regla:</b> primero que la segunda lectura sea del mismo pedido,
    /// después que los hechos concuerden, y solo entonces la pagabilidad. Clasificar
    /// hechos contradictorios daría una razón comercial a lo que es un dato roto.
    /// </remarks>
    /// <param name="correlacion">La correlación, que debe ser <see cref="OrderCorrelationOutcome.Resolved"/>.</param>
    /// <param name="pedido">El pedido releído, o <c>null</c> si ya no estaba.</param>
    /// <param name="pagos">Sus pagos registrados.</param>
    public static CommercialEvaluationObservation Clasificar(
        OrderCorrelationCandidate correlacion,
        OrderFactsRecord? pedido,
        IReadOnlyList<PaymentFactRecord> pagos)
    {
        ArgumentNullException.ThrowIfNull(correlacion);
        ArgumentNullException.ThrowIfNull(pagos);

        if (correlacion.Outcome is not OrderCorrelationOutcome.Resolved)
        {
            return SinPedido(correlacion);
        }

        if (correlacion.OrderId is not { } orderId || correlacion.OrderCode is not { } codigo)
        {
            return Incoherente(correlacion, CommercialEvaluationReason.ContradictoryFacts, null, null);
        }

        // Dos lecturas, sin transacción que las una. Si no hablan del mismo pedido, no
        // se elige cuál vale.
        if (pedido is null
            || pedido.OrderId != orderId
            || !string.Equals(pedido.OrderCode, codigo, StringComparison.Ordinal))
        {
            return Incoherente(correlacion, CommercialEvaluationReason.OrderChangedBetweenReads, null, null);
        }

        var hechos = Hechos(pedido, pagos);

        // Una identidad presente pero degenerada —el uuid nulo, que ninguna fila v7
        // lleva (ADR-016), o un código en blanco— es un dato roto del propio pedido.
        // Precede a toda regla comercial, también a la baja lógica, y conserva la
        // correlación y los hechos tal como se leyeron.
        if (IdentidadDegenerada(orderId, codigo))
        {
            return Incoherente(correlacion, CommercialEvaluationReason.ContradictoryFacts, null, hechos);
        }

        if (SeContradicen(pedido, pagos))
        {
            return Incoherente(correlacion, CommercialEvaluationReason.ContradictoryFacts, null, hechos);
        }

        // El mismo snapshot que arma CandidatePayableOrderReader: lo ya cobrado es la
        // suma de todos los pagos del pedido, sin mirar su origen. Así esta evaluación
        // y el lector legado no pueden dar respuestas distintas sobre los mismos hechos.
        var pagabilidad = ExternalCommercePolicy.EvaluarPagabilidad(
            orderId,
            new OrderCommercialSnapshot(
                pedido.OrderId,
                pedido.OrderCode,
                pedido.Status,
                pedido.TotalAmount,
                pagos.Sum(p => p.Amount),
                pedido.IsActive));

        return Traducir(correlacion, pagabilidad, hechos);
    }

    private static CommercialEvaluationObservation Traducir(
        OrderCorrelationCandidate correlacion,
        PayableOrderCandidate pagabilidad,
        CommercialFactsObservation hechos)
    {
        CommercialEvaluationReason? motivo = pagabilidad.Reason switch
        {
            PayableReason.InactiveOrder => CommercialEvaluationReason.InactiveOrder,
            PayableReason.InvalidAmount => CommercialEvaluationReason.InvalidAmount,
            PayableReason.OrderLifecycleUnratified => CommercialEvaluationReason.OrderLifecycleUnratified,
            PayableReason.AlreadySettledManually => CommercialEvaluationReason.AlreadySettledManually,
            PayableReason.CurrencyAuthorityMissing => CommercialEvaluationReason.CurrencyAuthorityMissing,
            _ => null,
        };

        var veredicto = pagabilidad.Outcome switch
        {
            PayableOutcome.NotEligible => CommercialEvaluationVerdict.Rejected,
            PayableOutcome.PendingDecision => CommercialEvaluationVerdict.PendingDecision,

            // Eligible y NotFound no tienen traducción aquí: el primero no está
            // ratificado y el segundo contradice la lectura recién hecha.
            _ => (CommercialEvaluationVerdict?)null,
        };

        return motivo is { } m && veredicto is { } v
            ? new CommercialEvaluationObservation(v, m, correlacion, pagabilidad, hechos)
            : Incoherente(correlacion, CommercialEvaluationReason.UnexpectedPolicyAnswer, pagabilidad, hechos);
    }

    private static CommercialFactsObservation Hechos(OrderFactsRecord pedido, IReadOnlyList<PaymentFactRecord> pagos)
    {
        var personal = pagos.Where(p => string.Equals(p.Source, PaymentSource.Staff, StringComparison.Ordinal)).ToArray();
        var historicos = pagos.Where(p => p.Source is null).ToArray();
        var otros = pagos.Where(p => p.Source is not null
            && !string.Equals(p.Source, PaymentSource.Staff, StringComparison.Ordinal)).ToArray();

        return new CommercialFactsObservation(
            pedido.OrderId,
            pedido.OrderCode,
            pedido.Status,
            pedido.TotalAmount,
            pedido.IsActive,
            pedido.CurrencyCode,
            Moneda(pedido.CurrencyCode),
            personal.Sum(p => p.Amount),
            personal.Length,
            historicos.Sum(p => p.Amount),
            historicos.Length,
            otros.Sum(p => p.Amount),
            otros.Length,
            pagos.Count(p => p.CurrencyCode is null));
    }

    private static bool IdentidadDegenerada(Guid orderId, string codigo)
        => orderId == Guid.Empty || string.IsNullOrWhiteSpace(codigo);

    private static CurrencyObservation Moneda(string? codigo) => codigo switch
    {
        null => CurrencyObservation.Absent,
        _ when CurrencyCode.TieneFormaIso(codigo) => CurrencyObservation.PresentWithoutRatifiedAuthority,
        _ => CurrencyObservation.MalformedValue,
    };

    /// <remarks>
    /// <b>Todo lo de aquí lo prohíbe ya la base</b> —el <c>CHECK</c> de estados, de
    /// importes, de forma de moneda y de origen—, y por eso mismo se comprueba: si
    /// aparece, la lectura no es de fiar. Las monedas se comparan solo cuando ambas
    /// están: una ausente es desconocida, no distinta.
    /// </remarks>
    private static bool SeContradicen(OrderFactsRecord pedido, IReadOnlyList<PaymentFactRecord> pagos)
    {
        if (!OrderStatus.All.Contains(pedido.Status, StringComparer.Ordinal))
        {
            return true;
        }

        if (pedido.CurrencyCode is not null && !CurrencyCode.TieneFormaIso(pedido.CurrencyCode))
        {
            return true;
        }

        foreach (var pago in pagos)
        {
            if (pago.Amount < 0)
            {
                return true;
            }

            if (pago.Source is not null && !PaymentSource.Ratified.Contains(pago.Source, StringComparer.Ordinal))
            {
                return true;
            }

            if (pago.CurrencyCode is not null && !CurrencyCode.TieneFormaIso(pago.CurrencyCode))
            {
                return true;
            }

            if (pago.CurrencyCode is not null
                && pedido.CurrencyCode is not null
                && !string.Equals(pago.CurrencyCode, pedido.CurrencyCode, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return pagos
            .Select(p => p.CurrencyCode)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Skip(1)
            .Any();
    }

    private static CommercialEvaluationObservation Rechaza(
        OrderCorrelationCandidate correlacion,
        CommercialEvaluationReason motivo)
        => new(CommercialEvaluationVerdict.Rejected, motivo, correlacion, null, null);

    private static CommercialEvaluationObservation Incoherente(
        OrderCorrelationCandidate correlacion,
        CommercialEvaluationReason motivo,
        PayableOrderCandidate? pagabilidad,
        CommercialFactsObservation? hechos)
        => new(CommercialEvaluationVerdict.Inconsistent, motivo, correlacion, pagabilidad, hechos);
}

/// <summary>
/// Evalúa, sin efectos, las precondiciones comerciales de una futura liquidación externa.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es un camino aparte del aplicador.</b> No implementa ningún contrato, no
/// depende de <see cref="ISettlementCandidateJournal"/>, no llama a
/// <see cref="IExternalSettlementCandidate"/> y no se registra en ningún contenedor:
/// quien lo quiera lo construye. El aplicador recuerda propuestas antes de responder;
/// esto no recuerda nada.
/// </para>
/// <para>
/// <b>Solo lee, y lee sin rastrear.</b> Tres consultas como mucho —la de la
/// correlación, el pedido y sus pagos—, todas con <c>AsNoTracking</c> y proyectadas a
/// lo que se usa. No hay <c>Add</c>, <c>Update</c>, <c>Remove</c> ni <c>SaveChanges</c>.
/// </para>
/// <para>
/// <b>No es un snapshot.</b> Las lecturas no comparten transacción: si la segunda
/// contradice a la primera el resultado es <see cref="CommercialEvaluationVerdict.Inconsistent"/>,
/// pero que concuerden no garantiza que el pedido siga igual al responder. No resuelve
/// la carrera entre evaluar y aplicar.
/// </para>
/// <para>
/// <b>El único estado es el contexto</b>, de solo lectura. Nada se guarda entre
/// llamadas: dos evaluaciones de los mismos hechos responden lo mismo y ninguna sabe
/// de la otra.
/// </para>
/// </remarks>
internal sealed class CandidateCommercialReadOnlyEvaluator
{
    private readonly SalesDbContext database;

    public CandidateCommercialReadOnlyEvaluator(SalesDbContext database)
    {
        ArgumentNullException.ThrowIfNull(database);
        this.database = database;
    }

    /// <summary>
    /// Correlaciona la declaración, lee los hechos del pedido y los clasifica.
    /// </summary>
    /// <remarks>
    /// <b>No lanza por un pedido desconocido</b> —es una respuesta— pero <b>sí propaga
    /// un fallo real</b> de la base o una cancelación, como los dos lectores que compone.
    /// Un fallo de lectura no se convierte en una respuesta comercial.
    /// </remarks>
    public async Task<CommercialEvaluationObservation> EvaluateAsync(
        OrderCorrelationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // El lector real de la correlación, no una copia de sus reglas.
        var correlacion = await new CandidateOrderCorrelationReader(database)
            .ResolveAsync(request, cancellationToken);

        if (correlacion.Outcome is not OrderCorrelationOutcome.Resolved || correlacion.OrderId is not { } orderId)
        {
            return CommercialEvaluationPolicy.Clasificar(correlacion, null, []);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var pedido = await database.Orders
            .AsNoTracking()
            .Where(o => o.OrderId == orderId)
            .Select(o => new OrderFactsRecord(o.OrderId, o.OrderCode, o.Status, o.TotalAmount, o.CurrencyCode, o.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

        if (pedido is null)
        {
            return CommercialEvaluationPolicy.Clasificar(correlacion, null, []);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var pagos = await database.OrderPayments
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .Select(p => new PaymentFactRecord(p.Amount, p.Source, p.CurrencyCode))
            .ToListAsync(cancellationToken);

        return CommercialEvaluationPolicy.Clasificar(correlacion, pedido, pagos);
    }
}
