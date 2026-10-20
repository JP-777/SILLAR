using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Endpoints;

/// <summary>Datos de borrador; nunca incluye instalación, actor, permisos o nodo escritor.</summary>
public sealed record AssistantDraftRequest(
    string? Question, string? Answer, string? Title, string? PublicUrl, long ExpectedVersion);

/// <summary>Versión esperada obligatoria al publicar o retirar.</summary>
public sealed record AssistantPublicationRequest(Guid RevisionId, long ExpectedVersion);

/// <summary>Confirmación de operación sin identidad interna del escritor.</summary>
public sealed record AssistantEditorialWriteReply(
    string Outcome, Guid ArticleId, Guid? RevisionId, long Version);

public sealed record AssistantEditorialDraftReply(Guid RevisionId, string Question,
    string Answer, string Title, string PublicUrl);

public sealed record AssistantEditorialPublishedReply(Guid RevisionId, string Question,
    string Answer, string Title, string PublicUrl, long PublicationVersion, DateTimeOffset PublishedAt);

public sealed record AssistantEditorialArticleReply(Guid ArticleId, long Version,
    AssistantEditorialDraftReply? Draft, AssistantEditorialPublishedReply? Published);

/// <summary>
/// Exclusivamente un servicio confiable del host puede construir este ámbito.
/// No leer rol, instalación, nodo o permisos desde body, query, cookies arbitrarias ni cabeceras públicas.
/// </summary>
public interface ITrustedEditorialAdminScope
{
    bool TryGetPrincipal(out EditorialPrincipal? actor);
}

/// <summary>Frontera de aplicación independiente del transporte HTTP.</summary>
public interface IAssistantEditorialAdministration
{
    Task<EditorialResult> CreateAsync(EditorialPrincipal actor, Guid articleId, CancellationToken ct);
    Task<EditorialResult> SaveDraftAsync(EditorialPrincipal actor, Guid articleId,
        KnowledgeDraft draft, long expectedVersion, CancellationToken ct);
    Task<EditorialResult> PublishAsync(EditorialPrincipal actor, Guid articleId,
        Guid revisionId, long expectedVersion, CancellationToken ct);
    Task<EditorialResult> WithdrawAsync(EditorialPrincipal actor, Guid articleId,
        Guid revisionId, long expectedVersion, CancellationToken ct);
    Task<EditorialArticleLookup> InspectAsync(EditorialPrincipal actor, Guid articleId, CancellationToken ct);
}
