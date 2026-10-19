namespace Sillar.Modules.Assistant.Domain;

/// <summary>Roles ya existentes en CORE; el adaptador futuro debe suministrarlos desde la sesión autenticada.</summary>
public static class EditorialRoles
{
    public const string Editor = "editor";
    public const string Admin = "admin";
    public const string SuperAdmin = "super_admin";
}

/// <summary>Identidad editorial que debe construir exclusivamente una frontera CORE confiable, nunca el navegador.</summary>
public sealed record EditorialPrincipal(
    Guid InstallationId, string Role, string WriterNode, bool HasArticleWriteAuthority);

/// <summary>Versión no pública que todavía no autoriza citas ni respuestas.</summary>
public sealed record KnowledgeDraft(Guid RevisionId, string Question, string Answer, string Title, Uri PublicUri);

/// <summary>Hecho autoritativo de publicación, separado de cualquier borrador posterior.</summary>
public sealed record PublishedKnowledge(
    Guid ArticleId, Guid RevisionId, Guid InstallationId,
    string Question, string Answer, string Title, Uri PublicUri,
    long ArticleVersion, DateTimeOffset PublishedAt);

public enum EditorialResult
{
    Saved,
    Published,
    Withdrawn,
    Forbidden,
    Conflict,
    Invalid,
    Unchanged
}

public enum KnowledgeReadStatus
{
    Authoritative,
    NotFound,
    Indeterminate
}

/// <summary>Resultado de lectura que demuestra la vigencia actual en la fuente editorial local.</summary>
public sealed record KnowledgeRead(
    KnowledgeReadStatus Status,
    PublishedKnowledge? Publication,
    bool IsLocalAuthority,
    bool IsCurrentPublished,
    bool IsWithdrawn,
    DateTimeOffset? ExpiresAt);

/// <summary>El índice puede sugerir candidatos; la comprobación final debe proceder de la fuente editorial.</summary>
public interface ILocalKnowledgeAuthority
{
    IReadOnlyList<Guid> FindCandidateArticleIds(Guid installationId, string normalizedQuestion, int maxCandidates);
    KnowledgeRead ReadCurrent(Guid installationId, Guid articleId);
    bool IsAllowedPublicUri(Guid installationId, Uri uri);
}

public enum FaqOutcome
{
    Answered,
    Insufficient,
    Unavailable
}

public sealed record FaqResult(FaqOutcome Outcome, string Answer, Guid? ArticleId,
    Guid? RevisionId, string? SourceTitle, string? PublicUrl)
{
    public static FaqResult Abstain(FaqOutcome outcome) => new(
        outcome, "No tengo información pública verificada suficiente para responder esa consulta.",
        null, null, null, null);
}
