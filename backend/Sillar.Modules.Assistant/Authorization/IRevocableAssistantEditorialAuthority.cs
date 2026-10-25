using Sillar.Modules.Assistant.Domain;

namespace Sillar.Modules.Assistant.Authorization;

/// <summary>Operaciones editoriales; no concede derechos ni incluye CAS o configuración de motores.</summary>
public enum AssistantEditorialPermission
{
    Inspect,
    Create,
    SaveDraft,
    Publish,
    Withdraw
}

/// <summary>
/// Objetivo de una operación, no una credencial. InstallationKey solo vincula la
/// evaluación CORE: la autoridad debe acreditar por su cuenta la identidad M12
/// y el permiso vigente. Nunca convertir la clave por igualdad o formato.
/// </summary>
public sealed record AssistantEditorialAuthorizationRequest(
    Guid InstallationKey, Guid ArticleId, AssistantEditorialPermission Permission);

public enum AssistantAuthorizedExecutionOutcome
{
    Denied,
    Revoked,
    Completed
}

/// <summary>
/// Contrato CONSUMIDOR candidato de M12, a implementar por el dueño de la autoridad.
/// No existe emisor productivo aquí ni registro automático en Host.
/// </summary>
/// <remarks>
/// La implementación debe resolver la sesión y la identidad autorizada, validar
/// su vinculación con CORE y comprobar el permiso sobre ESTE artículo/operación.
/// Un rol, principal suministrado por el llamador o procedencia observada no bastan.
/// Solo entonces puede invocar operation, una vez, con el principal resuelto y un
/// token cancelable ligado a revocación, no solo al timeout del llamador.
///
/// El productor debe serializar autorización, ejecución y revocación según el
/// contrato transaccional que apruebe su dueño. Una lectura booleana seguida de
/// una escritura no cumple este contrato. Sin poder garantizarlo, debe devolver
/// Denied sin invocar operation. Completed exige operación finalizada con autoridad
/// todavía válida; no equivale a una garantía de revocación posterior a su retorno.
/// La cancelación del llamador se propaga; ningún error debe conceder permiso.
/// </remarks>
public interface IRevocableAssistantEditorialAuthority
{
    Task<AssistantAuthorizedExecutionOutcome> ExecuteAsync(
        AssistantEditorialAuthorizationRequest request,
        Func<EditorialPrincipal, CancellationToken, Task> operation,
        CancellationToken cancellationToken);
}
