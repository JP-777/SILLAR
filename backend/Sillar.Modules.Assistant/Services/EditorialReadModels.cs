using Sillar.Modules.Assistant.Domain;

namespace Sillar.Modules.Assistant.Services;

public enum EditorialLookupStatus
{
    Found,
    NotFound,
    Forbidden
}

/// <summary>Estado de UN artículo para el responsable editorial autenticado.</summary>
public sealed record EditorialArticleSnapshot(
    Guid ArticleId, Guid InstallationId, string WriterNode, long Version,
    KnowledgeDraft? Draft, PublishedKnowledge? Published);

public sealed record EditorialArticleLookup(
    EditorialLookupStatus Status, EditorialArticleSnapshot? Article);
