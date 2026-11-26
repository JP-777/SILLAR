namespace Sillar.Modules.Assistant.Endpoints;

/// <summary>
/// Evidencia adquirida por la COMPOSICIÓN CONFIABLE del host. No implementar a
/// partir de datos HTTP (host header, query, cookie, JWT del visitante o body).
/// CORE debe determinar la instalación y su estado; la licencia no se infiere
/// del mero hecho de que un módulo figure en el catálogo.
/// </summary>
public interface ITrustedAssistantHostEvidence
{
    bool IsInstallationConfigured { get; }
    bool IsAssistantActive { get; }
    bool IsAssistantLicensed { get; }
    Guid? InstallationId { get; }
}

/// <summary>
/// Frontera fail-closed de M12. Es un adaptador PREPARATORIO: la fuente real de
/// ITrustedAssistantHostEvidence sigue pendiente de un contrato del dueño CORE.
/// No se registra automáticamente, ni monta rutas o credenciales nuevas.
/// </summary>
public sealed class HostAttestedAssistantInstallationScope : ITrustedAssistantInstallationScope
{
    private readonly ITrustedAssistantHostEvidence _evidence;

    public HostAttestedAssistantInstallationScope(ITrustedAssistantHostEvidence evidence)
        => _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));

    public bool TryGetEnabledInstallation(out Guid installationId)
    {
        installationId = Guid.Empty;
        try
        {
            if (!_evidence.IsInstallationConfigured ||
                !_evidence.IsAssistantActive ||
                !_evidence.IsAssistantLicensed)
                return false;

            var candidate = _evidence.InstallationId;
            if (!candidate.HasValue || candidate.Value == Guid.Empty)
                return false;

            installationId = candidate.Value;
            return true;
        }
        catch
        {
            installationId = Guid.Empty;
            return false;
        }
    }
}
