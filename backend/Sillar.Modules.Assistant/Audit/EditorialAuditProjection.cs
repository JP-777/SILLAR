using Sillar.Core.Contracts;
using Sillar.Modules.Assistant.Authorization;
using Sillar.Modules.Assistant.Domain;

namespace Sillar.Modules.Assistant.Audit;

/// <summary>
/// Proyección mínima candidata para CT-M12-02. No escribe, autoriza ni acredita
/// identidad, resultado o durabilidad. El consumidor debe obtenerlos de fuentes
/// confiables y resolver la atomicidad con CORE antes de conectar esta proyección.
/// </summary>
public static class EditorialAuditProjection
{
    /// <summary>
    /// Construye evidencia de publicación/retirada con vocabulario cerrado.
    /// No acepta preguntas, contenido, correo, roles ni excepciones. Un rechazo
    /// puede carecer de actor identificado; un resultado no prohibido lo exige.
    /// Update es la acción CORE existente; operación y resultado quedan explícitos
    /// en Summary. No introduce nuevas acciones ni requiere cambiar el CHECK.
    /// </summary>
    public static AuditEntry? Create(AssistantEditorialPermission operation,
        EditorialResult result, Guid articleId, int? verifiedAdminUserId)
    {
        if (articleId == Guid.Empty || verifiedAdminUserId is <= 0 ||
            (verifiedAdminUserId is null && result != EditorialResult.Forbidden)) return null;

        var action = operation switch
        {
            AssistantEditorialPermission.Publish => "publish",
            AssistantEditorialPermission.Withdraw => "withdraw",
            _ => null
        };
        var outcome = result switch
        {
            EditorialResult.Published when operation == AssistantEditorialPermission.Publish => "published",
            EditorialResult.Withdrawn when operation == AssistantEditorialPermission.Withdraw => "withdrawn",
            EditorialResult.Forbidden => "forbidden",
            EditorialResult.Conflict => "conflict",
            EditorialResult.Invalid => "invalid",
            EditorialResult.Unchanged => "unchanged",
            _ => null
        };
        if (action is null || outcome is null) return null;

        return new AuditEntry(AuditAction.Update)
        {
            AdminUserId = verifiedAdminUserId,
            ModuleCode = "assistant",
            EntityType = "knowledge_article",
            EntityId = articleId.ToString("D"),
            Summary = $"assistant.editorial.{action}:{outcome}"
        };
    }
}
