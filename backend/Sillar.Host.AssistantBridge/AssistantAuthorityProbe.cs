namespace Sillar.Host.AssistantBridge;

/// <summary>
/// Estado mínimo que el Host necesita recibir de una autoridad confiable.
/// No es un contrato de CORE: es la costura privada en la que un adaptador futuro
/// proyectará el resultado de CORE una vez conciliadas sus firmas.
/// </summary>
public enum AssistantAuthorityVerdict
{
    Denied,
    PendingDecision,
    Allowed
}

/// <summary>
/// Evidencia de identidad que el Host puede conservar antes de que JP ratifique
/// qué identidad consume M12. Nunca contiene ni renombra la clave interna.
/// </summary>
public enum AssistantInstallationIdentityEvidence
{
    Missing,
    InternalKeyObservedButUnattested
}

/// <summary>
/// Traza inmutable de la decisión autoritativa recibida por Host.
/// </summary>
/// <remarks>
/// Conserva causa, fuente y vigencia sin interpretarlas como licencia. La clave
/// interna sirve exclusivamente para cotejar la identidad de la observación;
/// nunca se publica como <c>InstallationId</c> de M12 ni como credencial.
/// </remarks>
public sealed record AssistantAuthorityObservation(
    AssistantAuthorityVerdict Verdict,
    AssistantInstallationIdentityEvidence InstallationIdentity,
    string Reason,
    string Source,
    DateTimeOffset ObservedAt,
    DateTimeOffset? ValidUntil,
    Guid? AuthorityInstallationKey);

/// <summary>
/// Punto de composición asíncrono del Host. Nunca recibe identidad, licencia ni
/// activación aportadas por HTTP.
/// </summary>
/// <remarks>
/// La implementación productiva será un adaptador sobre el contrato ratificado
/// de CORE. FASE A solo registra sondas explícitas en pruebas; no hay productor
/// predeterminado ni lectura de configuración, cookies, headers o claims.
/// </remarks>
public delegate ValueTask<AssistantAuthorityObservation> AssistantAuthorityProbe(
    CancellationToken cancellationToken);
