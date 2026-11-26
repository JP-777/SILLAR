namespace Sillar.Modules.Assistant.Endpoints;

/// <summary>Solo contiene la pregunta, nunca la instalación, rol, modalidad o cuota.</summary>
public sealed record AssistantFaqRequest(string? Question);

/// <summary>No incluye contenido editorial no publicado, autoridad CORE o datos del actor.</summary>
public sealed record AssistantFaqReply(
    string Status, string Answer, string? SourceTitle, string? SourceUrl);

/// <summary>
/// Contrato que solo puede construir un adaptador confiable del host: determina
/// instalación y activación del módulo sin leer identificadores del cuerpo HTTP.
/// No registrar una implementación que lea el ámbito de parámetros de cliente.
/// </summary>
public interface ITrustedAssistantInstallationScope
{
    bool TryGetEnabledInstallation(out Guid installationId);
}
