namespace Sillar.Host.AssistantBridge;

/// <summary>
/// Política CAS provisional de FASE A: ninguna identidad puede escribir una
/// política de cuota hasta que exista un protocolo de revocación ratificado.
/// </summary>
/// <remarks>
/// La firma coincide con el consumidor actual de M12 para que la integración
/// futura no cambie la semántica. Todavía no implementa su interfaz porque M12
/// no está presente en esta base.
/// </remarks>
public sealed class DenyAllAssistantQuotaPolicyWriter
{
    public bool CanWritePolicy(Guid installationId) => false;
}
