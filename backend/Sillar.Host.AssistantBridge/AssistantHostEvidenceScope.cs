using Sillar.Modules.Assistant.Endpoints;

namespace Sillar.Host.AssistantBridge;

/// <summary>
/// Contenedor scoped de la foto. Leerlo nunca bloquea ni inicia una consulta.
/// </summary>
public sealed class AssistantHostEvidenceScope
{
    private AssistantHostEvidenceSnapshot _current =
        AssistantHostEvidenceSnapshot.Denied(AssistantHostDenialReason.Unresolved);

    public AssistantHostEvidenceSnapshot Current => Volatile.Read(ref _current);

    internal void Publish(AssistantHostEvidenceSnapshot snapshot)
        => Volatile.Write(ref _current, snapshot);
}

/// <summary>
/// Adaptador síncrono sobre la foto ya resuelta. Antes de la resolución devuelve
/// la foto fail-closed del ámbito; sus getters no ejecutan E/S.
/// </summary>
public sealed class AssistantHostEvidenceAdapter(AssistantHostEvidenceScope scope)
    : ITrustedAssistantHostEvidence
{
    public bool IsInstallationConfigured => scope.Current.IsInstallationConfigured;

    public bool IsAssistantActive => scope.Current.IsAssistantActive;

    public bool IsAssistantLicensed => scope.Current.IsAssistantLicensed;

    public Guid? InstallationId => scope.Current.InstallationId;
}
