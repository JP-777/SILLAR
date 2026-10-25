using Sillar.Core.Contracts;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Authorization;

/// <summary>
/// Adaptador candidato de ejecución editorial. Reevalúa CORE por operación y
/// exige además una autoridad revocable independiente. No emite identidad,
/// permisos ni entitlement. No se registra automáticamente ni habilita endpoints.
/// </summary>
public sealed class RevocableAssistantEditorialAdministration(
    IAssistantEditorialAdministration inner,
    IAssistantAccessAuthority? accessAuthority = null,
    IRevocableAssistantEditorialAuthority? editorialAuthority = null) : IAssistantEditorialAdministration
{
    public Task<EditorialResult> CreateAsync(EditorialPrincipal actor, Guid articleId, CancellationToken ct)
        => Execute(actor, articleId, AssistantEditorialPermission.Create,
            (principal, token) => inner.CreateAsync(principal, articleId, token), EditorialResult.Forbidden, ct);

    public Task<EditorialResult> SaveDraftAsync(EditorialPrincipal actor, Guid articleId,
        KnowledgeDraft draft, long expectedVersion, CancellationToken ct)
        => Execute(actor, articleId, AssistantEditorialPermission.SaveDraft,
            (principal, token) => inner.SaveDraftAsync(principal, articleId, draft, expectedVersion, token),
            EditorialResult.Forbidden, ct);

    public Task<EditorialResult> PublishAsync(EditorialPrincipal actor, Guid articleId,
        Guid revisionId, long expectedVersion, CancellationToken ct)
        => Execute(actor, articleId, AssistantEditorialPermission.Publish,
            (principal, token) => inner.PublishAsync(principal, articleId, revisionId, expectedVersion, token),
            EditorialResult.Forbidden, ct);

    public Task<EditorialResult> WithdrawAsync(EditorialPrincipal actor, Guid articleId,
        Guid revisionId, long expectedVersion, CancellationToken ct)
        => Execute(actor, articleId, AssistantEditorialPermission.Withdraw,
            (principal, token) => inner.WithdrawAsync(principal, articleId, revisionId, expectedVersion, token),
            EditorialResult.Forbidden, ct);

    public Task<EditorialArticleLookup> InspectAsync(EditorialPrincipal actor, Guid articleId, CancellationToken ct)
        => Execute(actor, articleId, AssistantEditorialPermission.Inspect,
            (principal, token) => inner.InspectAsync(principal, articleId, token),
            new EditorialArticleLookup(EditorialLookupStatus.Forbidden, null), ct);

    private async Task<T> Execute<T>(EditorialPrincipal actor, Guid articleId,
        AssistantEditorialPermission permission, Func<EditorialPrincipal, CancellationToken, Task<T>> operation,
        T denied, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (inner is null || accessAuthority is null || editorialAuthority is null ||
            articleId == Guid.Empty || !ValidPrincipal(actor, permission)) return denied;
        try
        {
            // Nunca cachear una decisión positiva en el scope ni usar IsAllowed
            // sin comprobar también su causa. PendingDecision no es entitlement.
            var access = await accessAuthority.EvaluateCurrentInstallationAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (access is not { Outcome: AssistantAccessOutcome.Allowed,
                    Reason: AssistantAccessReason.AuthorizedByRatifiedPolicy,
                    InstallationKey: { } key } || key == Guid.Empty) return denied;

            var invocations = 0;
            var finished = false;
            T? value = default;
            var revocation = CancellationToken.None;
            var closed = 0;
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
            AssistantAuthorizedExecutionOutcome outcome;
            try
            {
                outcome = await editorialAuthority.ExecuteAsync(new(key, articleId, permission),
                async (principal, revoked) =>
                {
                    if (Interlocked.Increment(ref invocations) != 1 ||
                        Volatile.Read(ref closed) != 0 || lifetime.IsCancellationRequested ||
                        !ValidPrincipal(principal, permission) || principal != actor ||
                        !revoked.CanBeCanceled || revoked.IsCancellationRequested) return;
                    revocation = revoked;
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, revoked);
                    linked.Token.ThrowIfCancellationRequested();
                    value = await operation(principal, linked.Token);
                    linked.Token.ThrowIfCancellationRequested();
                    finished = true;
                }, ct);
            }
            finally
            {
                Volatile.Write(ref closed, 1);
                lifetime.Cancel();
            }
            ct.ThrowIfCancellationRequested();
            // También rechaza Completed sin callback, múltiples invocaciones y
            // revocación entre el callback y el retorno del proveedor de autoridad.
            if (outcome == AssistantAuthorizedExecutionOutcome.Completed && finished &&
                invocations == 1 && !revocation.IsCancellationRequested && value is not null)
                return value;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch
        {
            // Cancelación de autoridad, proveedor averiado o resultado incierto:
            // no revelar contenido, no reintentar ni declarar éxito. Esta capa no
            // puede deshacer una escritura que un productor defectuoso ya confirmó.
        }
        return denied;
    }

    private static bool ValidPrincipal(EditorialPrincipal? actor, AssistantEditorialPermission permission)
        => actor is not null && actor.InstallationId != Guid.Empty && actor.HasArticleWriteAuthority &&
           !string.IsNullOrWhiteSpace(actor.WriterNode) &&
           actor.Role is EditorialRoles.Editor or EditorialRoles.Admin or EditorialRoles.SuperAdmin &&
           (permission is not (AssistantEditorialPermission.Publish or AssistantEditorialPermission.Withdraw) ||
            actor.Role is EditorialRoles.Admin or EditorialRoles.SuperAdmin);
}
