using Sillar.Core.Contracts;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Authorization;

/// <summary>
/// Adaptador candidato de ejecución editorial. Reevalúa CORE por operación y
/// exige además una autoridad revocable independiente. No emite identidad,
/// permisos ni entitlement. Es la única fachada registrada por M12; sin
/// autoridades productivas deniega y no habilita endpoints.
/// </summary>
public sealed class RevocableAssistantEditorialAdministration(
    IAssistantEditorialAdministration inner,
    IAssistantEditorialAuthority? coreAuthority = null,
    IRevocableAssistantEditorialAuthority? editorialAuthority = null) : IAssistantEditorialAdministration
{
    public Task<EditorialResult> CreateAsync(EditorialPrincipal actor, Guid articleId, CancellationToken ct)
        => Execute(actor, articleId, AssistantEditorialPermission.Create, EditorialOperation.Create,
            (principal, token) => inner.CreateAsync(principal, articleId, token), EditorialResult.Forbidden, ct);

    public Task<EditorialResult> SaveDraftAsync(EditorialPrincipal actor, Guid articleId,
        KnowledgeDraft draft, long expectedVersion, CancellationToken ct)
        => Execute(actor, articleId, AssistantEditorialPermission.SaveDraft, EditorialOperation.Edit,
            (principal, token) => inner.SaveDraftAsync(principal, articleId, draft, expectedVersion, token),
            EditorialResult.Forbidden, ct);

    public Task<EditorialResult> PublishAsync(EditorialPrincipal actor, Guid articleId,
        Guid revisionId, long expectedVersion, CancellationToken ct)
        => Execute(actor, articleId, AssistantEditorialPermission.Publish, EditorialOperation.Publish,
            (principal, token) => inner.PublishAsync(principal, articleId, revisionId, expectedVersion, token),
            EditorialResult.Forbidden, ct);

    public Task<EditorialResult> WithdrawAsync(EditorialPrincipal actor, Guid articleId,
        Guid revisionId, long expectedVersion, CancellationToken ct)
        => Execute(actor, articleId, AssistantEditorialPermission.Withdraw, EditorialOperation.Retire,
            (principal, token) => inner.WithdrawAsync(principal, articleId, revisionId, expectedVersion, token),
            EditorialResult.Forbidden, ct);

    public Task<EditorialArticleLookup> InspectAsync(EditorialPrincipal actor, Guid articleId, CancellationToken ct)
        // CORE no tiene una operación inequívoca para esta lectura administrativa.
        // Hasta que exista un contrato ratificado, no se consulta ninguna dependencia.
        => Task.FromResult(new EditorialArticleLookup(EditorialLookupStatus.Forbidden, null));

    private async Task<T> Execute<T>(EditorialPrincipal actor, Guid articleId,
        AssistantEditorialPermission permission, EditorialOperation coreOperation,
        Func<EditorialPrincipal, CancellationToken, Task<T>> operation,
        T denied, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (inner is null || coreAuthority is null || editorialAuthority is null ||
            articleId == Guid.Empty || !ValidPrincipal(actor, permission)) return denied;
        try
        {
            // Se reevalúa por artículo y operación. El productor canónico actual
            // nunca concede; un rol global tampoco es permiso sobre el artículo.
            var decision = await coreAuthority.EvaluateAsync(
                new EditorialAuthorityRequest(articleId.ToString("D"), coreOperation), ct);
            ct.ThrowIfCancellationRequested();
            if (!ValidCoreDecision(decision)) return denied;

            var request = new AssistantEditorialAuthorizationRequest(
                articleId, permission, coreOperation, decision);

            var invocations = 0;
            var finished = false;
            T? value = default;
            var revocation = CancellationToken.None;
            var closed = 0;
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
            AssistantAuthorizedExecutionOutcome outcome;
            try
            {
                outcome = await editorialAuthority.ExecuteAsync(request,
                async (principal, commitGate, revoked) =>
                {
                    if (Interlocked.Increment(ref invocations) != 1 ||
                        Volatile.Read(ref closed) != 0 || lifetime.IsCancellationRequested ||
                        !ValidPrincipal(principal, permission) || principal != actor ||
                        principal.Role != decision.Actor!.Role ||
                        !revoked.CanBeCanceled || revoked.IsCancellationRequested) return;
                    revocation = revoked;
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, revoked);
                    linked.Token.ThrowIfCancellationRequested();
                    // Toda operación que llega aquí es una mutación y exige la
                    // frontera que serializa revocación, commit y evidencia durable.
                    if (commitGate is null) return;
                    value = await commitGate.CommitAsync(request, principal,
                        token => operation(principal, token), linked.Token);
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

    private static bool ValidCoreDecision(EditorialAuthorityDecision? decision)
        => decision is
           {
               Outcome: EditorialAuthorityOutcome.Allowed,
               Actor: { AdminUserId: > 0, SessionId: var sessionId } actor,
               Installation: { InstallationKey: var installationKey, LicenseType: var licenseType },
               ObservedAt: var observedAt
           }
           && decision.IsAllowed
           && sessionId != Guid.Empty
           && installationKey != Guid.Empty
           && observedAt != default
           && !string.IsNullOrWhiteSpace(actor.HomeNode)
           && actor.Role is EditorialRoles.Editor or EditorialRoles.Admin or EditorialRoles.SuperAdmin
           && !string.IsNullOrWhiteSpace(licenseType);
}
