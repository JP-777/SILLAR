namespace Sillar.Host.AssistantBridge;

/// <summary>Motivo interno por el que la foto confiable quedó denegada.</summary>
public enum AssistantHostDenialReason
{
    Unresolved,
    AuthorityDenied,
    PendingDecision,
    InstallationIdentityInvalid,
    InstallationIdentityUnattested,
    AuthorityUnavailable,
    AuthorityCancelled
}

/// <summary>
/// Evidencia diagnóstica conservada sin convertirla en permiso de M12.
/// </summary>
public sealed record AssistantAuthorityTrace(
    AssistantAuthorityVerdict Verdict,
    AssistantInstallationIdentityEvidence InstallationIdentity,
    string Reason,
    string Source,
    DateTimeOffset ObservedAt,
    DateTimeOffset? ValidUntil,
    Guid? AuthorityInstallationKey);

/// <summary>
/// Foto inmutable que alimentará los getters síncronos de M12 sin ejecutar E/S.
/// </summary>
/// <remarks>
/// Sus cuatro propiedades públicas conservan la forma consumidora de M12. La
/// implementación del contrato vive en <see cref="AssistantHostEvidenceAdapter"/>,
/// para no convertir la traza diagnóstica en una autoridad publicable.
/// </remarks>
public sealed record AssistantHostEvidenceSnapshot
{
    private AssistantHostEvidenceSnapshot(
        AssistantHostDenialReason denialReason,
        AssistantAuthorityTrace? authorityTrace)
    {
        DenialReason = denialReason;
        AuthorityTrace = authorityTrace;
    }

    public bool IsInstallationConfigured => false;

    public bool IsAssistantActive => false;

    public bool IsAssistantLicensed => false;

    public Guid? InstallationId => null;

    /// <summary>
    /// Diagnóstico interno. No debe exponerse como prueba de licencia ni como
    /// protocolo hacia clientes.
    /// </summary>
    public AssistantHostDenialReason DenialReason { get; }

    /// <summary>
    /// Traza CORE para diagnóstico y revalidación futura. Su presencia nunca
    /// concede licencia, activación, identidad M12 ni autorización del actor.
    /// </summary>
    public AssistantAuthorityTrace? AuthorityTrace { get; }

    public static AssistantHostEvidenceSnapshot Denied(
        AssistantHostDenialReason reason,
        AssistantAuthorityTrace? authorityTrace = null)
        => new(reason, authorityTrace);
}
