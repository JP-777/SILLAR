using Sillar.Modules.Assistant.Data.Repositories;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;

namespace Sillar.Modules.Assistant.Services;

/// <summary>Adaptador EF interno; DI solo expone la fachada revocable.</summary>
internal sealed class EfAssistantEditorialAdministration(EfEditorialArticleStore repository)
    : IAssistantEditorialAdministration
{
    public Task<EditorialResult> CreateAsync(EditorialPrincipal actor, Guid articleId, CancellationToken ct)
        => repository.CreateAsync(actor, articleId, ct);

    public Task<EditorialResult> SaveDraftAsync(EditorialPrincipal actor, Guid articleId,
        KnowledgeDraft draft, long expectedVersion, CancellationToken ct)
        => repository.SaveDraftAsync(actor, articleId, draft, expectedVersion, ct);

    public Task<EditorialResult> PublishAsync(EditorialPrincipal actor, Guid articleId,
        Guid revisionId, long expectedVersion, CancellationToken ct)
        => repository.PublishAsync(actor, articleId, revisionId, expectedVersion, ct);

    public Task<EditorialResult> WithdrawAsync(EditorialPrincipal actor, Guid articleId,
        Guid revisionId, long expectedVersion, CancellationToken ct)
        => repository.WithdrawAsync(actor, articleId, revisionId, expectedVersion, ct);

    public Task<EditorialArticleLookup> InspectAsync(EditorialPrincipal actor,
        Guid articleId, CancellationToken ct)
        => repository.InspectAsync(actor, articleId, ct);
}
