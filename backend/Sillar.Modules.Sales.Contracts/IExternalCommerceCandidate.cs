namespace Sillar.Modules.Sales.Contracts;

/// <summary>
/// Candidato de FASE A: lo que M11 necesitaría preguntar a M03 sobre un pedido.
/// </summary>
/// <remarks>
/// <para>
/// <b>M03 es el único dueño del hecho comercial.</b> Este contrato deja consultar
/// y proponer; no publica endpoints, no se registra en <c>SalesModule</c> y no
/// escribe nada. La frontera es la de siempre: quien consume ve contratos, nunca
/// entidades ni <c>DbContext</c> de Ventas.
/// </para>
/// <para>
/// <b>Es candidato, no ratificado.</b> Mientras falten las tres decisiones de JP
/// —autoridad de moneda, identidad durable de liquidación y política de ciclo de
/// vida del pedido ante un pago externo— ninguna rama puede devolver
/// <see cref="PayableOutcome.Eligible"/>. Un contrato que respondiera «elegible»
/// sin ellas estaría decidiendo en lugar de JP.
/// </para>
/// </remarks>
public interface IPayableOrderCandidate
{
    /// <summary>
    /// Evalúa si un pedido podría recibir una liquidación externa.
    /// </summary>
    /// <remarks>
    /// <b>No lanza por un pedido inexistente</b> —eso es una respuesta, no un
    /// fallo— pero <b>sí propaga un fallo real</b> de la base o una cancelación.
    /// Disfrazar una avería de respuesta de negocio es la forma de que nadie la
    /// vea: es la misma doctrina que M10 ya prueba sobre los agregados de M03.
    /// </remarks>
    Task<PayableOrderCandidate> EvaluateAsync(Guid orderId, CancellationToken cancellationToken);
}

/// <summary>Qué puede responder M03 sobre la pagabilidad de un pedido.</summary>
public enum PayableOutcome
{
    /// <summary>No existe el pedido en esta base.</summary>
    NotFound,

    /// <summary>Existe y no puede recibir el pago. Es una respuesta cerrada.</summary>
    NotEligible,

    /// <summary>Existe, y falta una decisión que no es técnica. <b>Lo único que M03 puede devolver hoy.</b></summary>
    PendingDecision,

    /// <summary>Reservado: inalcanzable hasta que JP ratifique moneda, identidad y ciclo de vida.</summary>
    Eligible,
}

/// <summary>Por qué, en una palabra que el consumidor puede ramificar.</summary>
public enum PayableReason
{
    /// <summary>El identificador no corresponde a ningún pedido de esta base.</summary>
    UnknownOrder,

    /// <summary>Pedido dado de baja lógica. La baja es la ley de la casa y se respeta.</summary>
    InactiveOrder,

    /// <summary>Importe imposible. No debería ocurrir: lo prohíbe un <c>CHECK</c>.</summary>
    InvalidAmount,

    /// <summary>No hay autoridad de moneda ratificada, y M03 no almacena la del pedido.</summary>
    CurrencyAuthorityMissing,

    /// <summary>Reservado: las reglas de liquidación externa no están ratificadas.</summary>
    SettlementRulesUnratified,

    /// <summary>Reservado: la autoridad comercial no se pudo consultar.</summary>
    AuthorityUnavailable,

    /// <summary>Reservado: inalcanzable mientras <see cref="PayableOutcome.Eligible"/> lo sea.</summary>
    RatifiedAndVerified,

    /// <summary>
    /// El pedido está cancelado o entregado. <b>M03 ya tiene escrito que ese caso
    /// necesita una decisión</b> —y la tiene escrita para el pago manual, no para
    /// el externo—, así que extenderla al proveedor no está ratificado.
    /// </summary>
    OrderLifecycleUnratified,

    /// <summary>
    /// Ya hay pagos manuales registrados que cubren el total congelado. Aplicar
    /// además una liquidación externa exige una política que no está ratificada.
    /// </summary>
    AlreadySettledManually,
}

/// <summary>
/// Lo que M03 sabe del pedido, sin inventar nada de lo que no sabe.
/// </summary>
/// <remarks>
/// <b>El constructor posicional no cambia</b>, para que un consumidor que ya lo
/// lee siga compilando; lo añadido viaja en propiedades <c>init</c>.
/// </remarks>
/// <param name="OrderId">El pedido consultado. Nunca sale al navegador.</param>
/// <param name="FrozenTotal">
/// Total congelado del pedido, tal como está guardado. <b>Es el total, no el
/// pendiente:</b> lo ya cobrado a mano viaja aparte en <see cref="ManuallySettledTotal"/>.
/// </param>
/// <param name="Currency">
/// <c>null</c> significa DESCONOCIDA. <b>Jamás PEN ni USD implícitos:</b> M03 no
/// tiene columna de moneda y no puede afirmar la de un pedido existente.
/// </param>
/// <param name="Outcome">Qué puede responder M03.</param>
/// <param name="Reason">Por qué.</param>
/// <param name="Source">De dónde salió el dato. Ver <see cref="CandidateSources"/>.</param>
public sealed record PayableOrderCandidate(
    Guid OrderId, decimal? FrozenTotal, string? Currency,
    PayableOutcome Outcome, PayableReason Reason, string Source)
{
    /// <summary>Código visible del pedido, para que el consumidor no tenga que resolverlo.</summary>
    public string? OrderCode { get; init; }

    /// <summary>Estado actual, con el vocabulario de <see cref="OrderStatus"/>.</summary>
    public string? Status { get; init; }

    /// <summary>
    /// Suma de los pagos manuales ya registrados. <c>null</c> si no se pudo leer el
    /// pedido. Separar esto del total es lo que permite no fraccionar por accidente.
    /// </summary>
    public decimal? ManuallySettledTotal { get; init; }
}

/// <summary>De dónde sale lo que M03 responde.</summary>
public static class CandidateSources
{
    /// <summary>
    /// Esta base de datos. <b>No es «este nodo»:</b> las ventas se replican, así que
    /// una vez haya replicación esta base contendrá filas nacidas en otros nodos.
    /// </summary>
    public const string CurrentDatabase = "sales.current_database";
}

/// <summary>
/// Candidato de FASE A para proponer a M03 una liquidación externa ya verificada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Solo un adaptador de confianza propondría hechos de proveedor</b>, y proponer
/// no es aplicar. Quien llama trae la evidencia ya verificada; M03 no verifica
/// proveedores y no se convierte en su pasarela.
/// </para>
/// <para>
/// <b>En FASE A no existe efecto durable.</b> No hay almacén, no hay migración y no
/// hay transacción: por eso <see cref="ExternalSettlementOutcome.AppliedDurable"/> y
/// <see cref="ExternalSettlementOutcome.AlreadyAppliedEquivalent"/> son
/// deliberadamente inalcanzables. Un ACK técnico de bandeja <b>no</b> es un efecto
/// aplicado, y confundirlos es exactamente el error que este candidato evita.
/// </para>
/// </remarks>
public interface IExternalSettlementCandidate
{
    /// <summary>Propone la liquidación. Nunca afirma haberla efectuado.</summary>
    Task<ExternalSettlementCandidateResult> TryApplyAsync(
        ExternalSettlementCandidateEvent candidate,
        CancellationToken cancellationToken);
}

/// <summary>
/// El hecho que el proveedor dice haber verificado.
/// </summary>
/// <param name="Provider">Código del proveedor. Obligatorio.</param>
/// <param name="ProviderEventId">
/// Identidad del evento dentro del proveedor. Obligatorio: es la clave con la que
/// se reconoce un reintento.
/// </param>
/// <param name="ExternalSettlementId">
/// Identidad durable de la liquidación, que es distinta del evento: dos eventos
/// pueden anunciar la misma liquidación. <c>null</c> mientras su política no esté
/// ratificada.
/// </param>
/// <param name="OrderId">Pedido al que se atribuye.</param>
/// <param name="Amount">Importe liquidado.</param>
/// <param name="Currency">Moneda ISO del hecho. Obligatoria por hecho, nunca supuesta.</param>
/// <param name="VerifiedEvidenceReference">
/// Referencia a la evidencia ya verificada por quien propone. Sin ella el evento no
/// está autenticado y no llega a ser una pregunta comercial.
/// </param>
public sealed record ExternalSettlementCandidateEvent(
    string Provider, string ProviderEventId, string? ExternalSettlementId,
    Guid OrderId, decimal Amount, string? Currency,
    string? VerifiedEvidenceReference);

/// <summary>Los cuatro desenlaces, explícitamente distintos.</summary>
public enum ExternalSettlementOutcome
{
    /// <summary>Reservado: efecto comercial realmente persistido. <b>Inalcanzable en FASE A.</b></summary>
    AppliedDurable,

    /// <summary>
    /// Reservado: repetición equivalente de algo <b>ya aplicado durablemente</b>.
    /// <b>Inalcanzable en FASE A</b>, porque nada se aplicó nunca.
    /// </summary>
    AlreadyAppliedEquivalent,

    /// <summary>Rechazo cerrado: la propuesta no es admisible y repetirla no cambia nada.</summary>
    Rejected,

    /// <summary>Falta una decisión que no es técnica. Repetir no ayuda; decidir sí.</summary>
    PendingDecision,
}

/// <summary>Por qué.</summary>
public enum ExternalSettlementReason
{
    /// <summary>Falta pedido, proveedor o identidad de evento.</summary>
    InvalidIdentity,

    /// <summary>Importe no positivo.</summary>
    InvalidAmount,

    /// <summary>Sin moneda del hecho. No se supone ninguna.</summary>
    MissingCurrency,

    /// <summary>Sin evidencia verificada. <b>Se comprueba antes que cualquier regla comercial.</b></summary>
    UnauthenticatedEvent,

    /// <summary>La política comercial del pago externo no está ratificada.</summary>
    CommercialPolicyUnratified,

    /// <summary>La identidad durable de la liquidación no está ratificada.</summary>
    DurableIdentityPolicyUnratified,

    /// <summary>Reservado: política ratificada y escritura durable no disponible.</summary>
    DurableWriteUnavailable,

    /// <summary>
    /// Se repitió la misma identidad de evento con hechos distintos. <b>No se devuelve
    /// la primera respuesta en silencio:</b> comparar solo la clave y no los hechos es
    /// cómo un reintento incompatible pasa por duplicado.
    /// </summary>
    InconsistentReplay,

    /// <summary>Reservado: inalcanzable mientras lo sean los desenlaces terminales.</summary>
    RatifiedAndPersisted,

    /// <summary>
    /// Repetición equivalente de una propuesta que <b>nunca llegó a aplicarse</b>. No
    /// es <see cref="ExternalSettlementOutcome.AlreadyAppliedEquivalent"/>: no hay
    /// efecto del que esto sea duplicado.
    /// </summary>
    EquivalentReplayWithoutDurableEffect,
}

/// <summary>
/// La respuesta de M03.
/// </summary>
/// <param name="Outcome">Qué pasó.</param>
/// <param name="Reason">Por qué.</param>
/// <param name="DurableEffectReference">
/// Referencia del efecto persistido. <b>Siempre <c>null</c> en FASE A:</b> un valor
/// aquí afirmaría una transacción que no ocurrió.
/// </param>
public sealed record ExternalSettlementCandidateResult(
    ExternalSettlementOutcome Outcome, ExternalSettlementReason Reason,
    string? DurableEffectReference = null);
