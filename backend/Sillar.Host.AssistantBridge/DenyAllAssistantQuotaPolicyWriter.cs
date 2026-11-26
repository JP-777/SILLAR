using Sillar.Modules.Assistant.Services;

namespace Sillar.Host.AssistantBridge;

/// <summary>
/// Política CAS provisional de FASE A: ninguna identidad puede escribir una
/// política de cuota hasta que exista un protocolo de revocación ratificado.
/// </summary>
/// <remarks>
/// Implementa el contrato consumidor real de M12 sin convertir la presencia del
/// módulo, su activación o una identidad observada en permiso de escritura.
/// </remarks>
public sealed class DenyAllAssistantQuotaPolicyWriter : ITrustedAssistantQuotaPolicyWriter
{
    public bool CanWritePolicy(Guid installationId) => false;
}
