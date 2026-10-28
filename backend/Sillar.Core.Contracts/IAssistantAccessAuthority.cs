namespace Sillar.Core.Contracts;

/// <summary>
/// Autoridad de acceso al Asistente (M12) para la instalación actual. Fase A, candidato.
/// </summary>
/// <remarks>
/// <para>
/// La identidad la resuelve CORE leyendo su propia base; nunca llega por HTTP ni
/// la aporta quien pregunta. Tampoco se deriva de <c>ICurrentAdmin</c> ni de
/// <c>IModuleRegistry.IsActive()</c>: la sesión dice quién opera y el registro
/// es la foto del arranque, y ninguna de las dos cosas es un derecho de uso.
/// </para>
/// <para>
/// <b>En Fase A nunca devuelve <see cref="AssistantAccessOutcome.Allowed"/>.</b>
/// Con hechos técnicos favorables devuelve <see cref="AssistantAccessOutcome.PendingDecision"/>,
/// porque el derecho de uso, la identidad y la política de acceso no están
/// ratificados. Sin autoridad, o con hechos inválidos, devuelve
/// <see cref="AssistantAccessOutcome.Denied"/>.
/// </para>
/// <para>
/// Si el token del llamador se cancela, propaga <see cref="OperationCanceledException"/>.
/// Una cancelación interna, un tiempo agotado o un fallo del proveedor no se
/// propagan: se responden como <see cref="AssistantAccessReason.AuthorityUnavailable"/>.
/// </para>
/// </remarks>
public interface IAssistantAccessAuthority
{
    /// <summary>Evalúa los hechos vigentes de la instalación en un solo instante.</summary>
    Task<AssistantAccessDecision> EvaluateCurrentInstallationAsync(CancellationToken cancellationToken);
}

/// <summary>Resultado de la evaluación.</summary>
public enum AssistantAccessOutcome
{
    /// <summary>No hay autoridad, o los hechos lo impiden.</summary>
    Denied,

    /// <summary>Los hechos técnicos no lo impiden, pero no hay política ratificada que lo conceda.</summary>
    PendingDecision,

    /// <summary>Reservado a una política ratificada. Ninguna implementación de Fase A lo devuelve.</summary>
    Allowed,
}

/// <summary>Por qué se llegó al resultado.</summary>
public enum AssistantAccessReason
{
    /// <summary>No hay fila en <c>core.installation</c>.</summary>
    InstallationMissing,

    /// <summary>La instalación no terminó su configuración inicial.</summary>
    InstallationIncomplete,

    /// <summary>La identidad persistida no sirve: clave vacía, o más de una instalación.</summary>
    InstallationIdentityInvalid,

    /// <summary>Tipo de licencia fuera de los admitidos (comparación ordinal).</summary>
    LicenseUnrecognized,

    /// <summary>Una licencia temporal (<c>trial</c>, <c>subscription</c>) sin fecha de fin.</summary>
    LicenseExpiryMissing,

    /// <summary>Una licencia perpetua con fecha de fin: los hechos se contradicen.</summary>
    LicenseInconsistent,

    /// <summary>La licencia venció. En el instante exacto de su fin ya está vencida.</summary>
    LicenseExpired,

    /// <summary>El módulo <c>assistant</c> no está declarado en <c>core.modules</c>.</summary>
    ModuleMissing,

    /// <summary>El módulo existe pero no tiene activación, o está desactivado.</summary>
    ModuleInactive,

    /// <summary>La activación venció. En el instante exacto de su fin ya está vencida.</summary>
    ModuleExpired,

    /// <summary>Hechos favorables sin política de derecho de uso ratificada. Acompaña a <see cref="AssistantAccessOutcome.PendingDecision"/>.</summary>
    EntitlementPolicyUnratified,

    /// <summary>No se pudo leer la autoridad: fallo del proveedor, tiempo agotado o cancelación interna.</summary>
    AuthorityUnavailable,

    /// <summary>Reservado a una política ratificada. Ninguna implementación de Fase A lo devuelve.</summary>
    AuthorizedByRatifiedPolicy,
}

/// <summary>La decisión, con los hechos de los que sale.</summary>
/// <param name="Outcome">Resultado.</param>
/// <param name="Reason">Causa tipada del resultado.</param>
/// <param name="InstallationKey">Identidad interna persistida. NO es un identificador público ni una credencial. Nula si no hay o no es válida.</param>
/// <param name="Source">Procedencia de los hechos observados. No es evidencia de un derecho de uso concedido.</param>
/// <param name="ObservedAt">El único instante con el que se comparó todo.</param>
/// <param name="ValidUntil">Hasta cuándo siguen siendo favorables los hechos técnicos: la primera fecha de fin entre licencia y activación. Solo con <see cref="AssistantAccessOutcome.PendingDecision"/>; nula si no vence o si se deniega. No es la vigencia de un permiso.</param>
/// <param name="Facts">Los hechos de licencia y activación leídos. Nulos si no se pudo leer o no hay instalación.</param>
public sealed record AssistantAccessDecision(
    AssistantAccessOutcome Outcome,
    AssistantAccessReason Reason,
    Guid? InstallationKey,
    string Source,
    DateTimeOffset ObservedAt,
    DateTimeOffset? ValidUntil,
    AssistantAccessFacts? Facts)
{
    /// <summary>Verdadero solo con <see cref="AssistantAccessOutcome.Allowed"/>.</summary>
    public bool IsAllowed => Outcome == AssistantAccessOutcome.Allowed;
}

/// <summary>Hechos de licencia y activación tal como se leyeron, en una sola lectura.</summary>
/// <param name="LicenseType">Tipo de licencia escrito en la instalación.</param>
/// <param name="LicensedUntil">Fin de la licencia, si tiene.</param>
/// <param name="ModuleDeclared">Si <c>assistant</c> existe en <c>core.modules</c>.</param>
/// <param name="ActivationPresent">Si el módulo tiene fila de activación.</param>
/// <param name="ActivationIsActive">Si la activación está encendida.</param>
/// <param name="ActivationExpiresAt">Fin de la activación, si tiene.</param>
public sealed record AssistantAccessFacts(
    string LicenseType,
    DateTimeOffset? LicensedUntil,
    bool ModuleDeclared,
    bool ActivationPresent,
    bool ActivationIsActive,
    DateTimeOffset? ActivationExpiresAt);
