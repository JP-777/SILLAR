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
/// Proyección interna de una evaluación autoritativa. El identificador solo es
/// admisible cuando el veredicto es <see cref="AssistantAuthorityVerdict.Allowed"/>.
/// </summary>
public sealed record AssistantAuthorityObservation(
    AssistantAuthorityVerdict Verdict,
    Guid? InstallationId);

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
