using Sillar.Core.Contracts;
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
/// Objetivo de una operación, no una credencial. Conserva la decisión CORE
/// original para que el productor pueda vincular por su cuenta actor, sesión e
/// instalación con la identidad M12 y comprobar el permiso vigente. HomeNode no
/// es WriterNode e InstallationKey no es InstallationId: nunca convertirlos ni
/// compararlos por igualdad o formato.
/// </summary>
public sealed record AssistantEditorialAuthorizationRequest(
    Guid ArticleId,
    AssistantEditorialPermission Permission,
    EditorialOperation CoreOperation,
    EditorialAuthorityDecision CoreDecision);

public enum AssistantAuthorizedExecutionOutcome
{
    Denied,
    Revoked,
    Completed
}

/// <summary>
/// Frontera de commit aportada exclusivamente por el productor de autoridad.
/// No existe implementación productiva en M12. Para una mutación, el productor
/// debe mantener serializados permiso, sesión, revocación y commit; escribir en la
/// misma transacción la evidencia CORE o una intención outbox M12 durable, según
/// la decisión de JP. No puede confirmar el resultado si falla esa evidencia.
/// </summary>
public interface IEditorialCommitGate
{
    Task<T> CommitAsync<T>(AssistantEditorialAuthorizationRequest request,
        EditorialPrincipal principal, Func<CancellationToken, Task<T>> mutation,
        CancellationToken cancellationToken);
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
/// token cancelable ligado a revocación, no solo al timeout del llamador, y
/// una frontera de commit para todas las mutaciones.
///
/// El productor debe serializar autorización, ejecución y revocación según el
/// contrato transaccional que apruebe su dueño. El gate debe conservar el bloqueo
/// o fencing hasta DESPUÉS del commit editorial y de la evidencia de auditoría.
/// Una lectura booleana seguida de una escritura no cumple este contrato. Sin poder
/// garantizarlo, debe devolver
/// Denied sin invocar operation. Completed exige operación finalizada con autoridad
/// todavía válida; no equivale a una garantía de revocación posterior a su retorno.
/// La cancelación del llamador se propaga; ningún error debe conceder permiso.
/// </remarks>
public interface IRevocableAssistantEditorialAuthority
{
    Task<AssistantAuthorizedExecutionOutcome> ExecuteAsync(
        AssistantEditorialAuthorizationRequest request,
        Func<EditorialPrincipal, IEditorialCommitGate?, CancellationToken, Task> operation,
        CancellationToken cancellationToken);
}
