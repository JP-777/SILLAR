namespace Sillar.Core.Contracts;

/// <summary>
/// Auditoría editorial durable del Asistente (M12). Fase A, candidato: los
/// contratos existen; ninguna estrategia está ratificada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dos guardados no son una transacción.</b> Llamar a <see cref="IAuditWriter"/>
/// (que guarda en <c>CoreDbContext</c>) y después guardar el cambio en el contexto
/// de M12 son dos <c>SaveChanges</c> en dos contextos y dos conexiones: si el
/// segundo falla, el primero ya está confirmado, y al revés. Ningún productor de
/// este contrato puede presentar eso como atómico.
/// </para>
/// <para>
/// Las dos salidas posibles, <b>ninguna ratificada</b>:
/// </para>
/// <list type="bullet">
/// <item><see cref="EditorialAuditStrategy.SharedTransaction"/>: CORE y M12 sobre
/// la misma conexión y la misma transacción. No hay en el repositorio ningún uso de
/// transacción compartida entre contextos, ni decisión que la permita entre módulos.</item>
/// <item><see cref="EditorialAuditStrategy.DurableOutbox"/>: M12 guarda el cambio y
/// el evento de auditoría en su propia transacción, y un relevo lo lleva a
/// <c>core.audit_log</c> de forma idempotente por <see cref="EditorialAuditRecord.EventId"/>.
/// Exige una clave de deduplicación que <c>core.audit_log</c> no tiene (migración).</item>
/// </list>
/// <para>
/// Hasta que una se ratifique, M12 no debe confirmar cambios editoriales:
/// <see cref="EditorialAuditRequirements.PermitsEditorialCommit"/> devuelve falso.
/// </para>
/// </remarks>
public interface IEditorialAudit
{
    /// <summary>Lo que este productor garantiza de verdad.</summary>
    EditorialAuditCapability Capability { get; }

    /// <summary>Registra un evento editorial.</summary>
    Task<EditorialAuditResult> RecordAsync(EditorialAuditRecord record, CancellationToken cancellationToken);
}

/// <summary>Cómo se hace durable la auditoría junto al cambio editorial.</summary>
public enum EditorialAuditStrategy
{
    /// <summary>Sin estrategia: no hay garantía de que auditoría y cambio vayan juntos.</summary>
    None,

    /// <summary>Misma conexión y misma transacción. No ratificada.</summary>
    SharedTransaction,

    /// <summary>Outbox en el schema de M12 y relevo idempotente. No ratificada.</summary>
    DurableOutbox,
}

/// <summary>Lo que garantiza un productor.</summary>
/// <param name="Strategy">Estrategia que implementa.</param>
/// <param name="IsAtomicWithEditorialCommit">Si auditoría y cambio se confirman o se deshacen juntos.</param>
/// <param name="IsRatified">Si la estrategia está ratificada.</param>
public sealed record EditorialAuditCapability(EditorialAuditStrategy Strategy, bool IsAtomicWithEditorialCommit, bool IsRatified);

/// <summary>Un evento editorial. Sin contenido del artículo.</summary>
/// <param name="EventId">uuid v7 generado por M12; clave de idempotencia.</param>
/// <param name="InstallationKey">Instalación, por su <c>installation_key</c>.</param>
/// <param name="Actor">Actor según <see cref="IAssistantEditorialAuthority"/>, con su sesión.</param>
/// <param name="ArticleId">Artículo.</param>
/// <param name="Operation">Operación.</param>
/// <param name="DecisionObservedAt">Instante de la decisión de autoridad en que se basó.</param>
public sealed record EditorialAuditRecord(
    Guid EventId,
    Guid InstallationKey,
    EditorialActor Actor,
    string ArticleId,
    EditorialOperation Operation,
    DateTimeOffset DecisionObservedAt);

/// <summary>Resultado del registro.</summary>
public enum EditorialAuditOutcome
{
    /// <summary>Registrado según la garantía del productor.</summary>
    Recorded,

    /// <summary>No registrado.</summary>
    Rejected,
}

/// <summary>Por qué no se registró.</summary>
public enum EditorialAuditFailure
{
    /// <summary>Ninguna estrategia ratificada: no se registra nada.</summary>
    StrategyUnratified,

    /// <summary>El evento no es válido.</summary>
    RecordInvalid,

    /// <summary>No se pudo escribir.</summary>
    AuditUnavailable,
}

/// <summary>El resultado.</summary>
/// <param name="Outcome">Registrado o rechazado.</param>
/// <param name="Failure">La causa, si se rechazó.</param>
/// <param name="IsAtomicWithEditorialCommit">Si el registro va atado al cambio. Falso salvo estrategia ratificada que lo garantice.</param>
public sealed record EditorialAuditResult(EditorialAuditOutcome Outcome, EditorialAuditFailure? Failure, bool IsAtomicWithEditorialCommit)
{
    /// <summary>Rechazado, sin atomicidad.</summary>
    public static EditorialAuditResult Rejected(EditorialAuditFailure failure) => new(EditorialAuditOutcome.Rejected, failure, false);
}

/// <summary>Lo que M12 comprueba antes de confirmar un cambio editorial.</summary>
public static class EditorialAuditRequirements
{
    /// <summary>
    /// Solo con una estrategia distinta de <see cref="EditorialAuditStrategy.None"/>,
    /// ratificada y atómica con el cambio. Hoy ningún productor lo cumple.
    /// </summary>
    public static bool PermitsEditorialCommit(EditorialAuditCapability capability)
        => capability.Strategy != EditorialAuditStrategy.None
            && capability.IsRatified
            && capability.IsAtomicWithEditorialCommit;
}
