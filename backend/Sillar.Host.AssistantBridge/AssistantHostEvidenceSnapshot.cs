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
/// Foto inmutable que alimentará los getters síncronos de M12 sin ejecutar E/S.
/// </summary>
/// <remarks>
/// Sus cuatro propiedades públicas coinciden deliberadamente con la forma
/// consumidora actual de M12, pero este tipo todavía no implementa esa interfaz:
/// M12 no forma parte de esta base y sus contratos no están ratificados.
/// </remarks>
public sealed record AssistantHostEvidenceSnapshot
{
    private AssistantHostEvidenceSnapshot(AssistantHostDenialReason denialReason)
        => DenialReason = denialReason;

    public bool IsInstallationConfigured => false;

    public bool IsAssistantActive => false;

    public bool IsAssistantLicensed => false;

    public Guid? InstallationId => null;

    /// <summary>
    /// Diagnóstico interno. No debe exponerse como prueba de licencia ni como
    /// protocolo hacia clientes.
    /// </summary>
    public AssistantHostDenialReason DenialReason { get; }

    public static AssistantHostEvidenceSnapshot Denied(AssistantHostDenialReason reason)
        => new(reason);
}
