namespace Sillar.Modules.Assistant.Domain;

/// <summary>
/// Agregado editorial en memoria para una instalación y un escritor autorizado.
/// No sustituye el control de concurrencia transaccional ni el contrato CORE.
/// </summary>
public sealed class KnowledgeArticle
{
    public Guid Id { get; }
    public Guid InstallationId { get; }
    public string WriterNode { get; }
    public long Version { get; private set; }
    public KnowledgeDraft? Draft { get; private set; }
    public PublishedKnowledge? Published { get; private set; }

    public KnowledgeArticle(Guid id, Guid installationId, string writerNode)
    {
        if (id == Guid.Empty || installationId == Guid.Empty || string.IsNullOrWhiteSpace(writerNode))
            throw new ArgumentException("Artículo, instalación y escritor son obligatorios.");
        Id = id;
        InstallationId = installationId;
        WriterNode = writerNode;
    }

    public EditorialResult SaveDraft(EditorialPrincipal actor, KnowledgeDraft draft, long expectedVersion)
    {
        if (!CanEdit(actor)) return EditorialResult.Forbidden;
        if (expectedVersion != Version) return EditorialResult.Conflict;
        if (draft is null || !ValidDraft(draft)) return EditorialResult.Invalid;
        Draft = draft;
        Version++;
        return EditorialResult.Saved;
    }

    public EditorialResult Publish(EditorialPrincipal actor, Guid draftRevisionId,
        long expectedVersion, DateTimeOffset now)
    {
        if (!CanPublish(actor)) return EditorialResult.Forbidden;
        if (expectedVersion != Version) return EditorialResult.Conflict;
        if (Draft is null || Draft.RevisionId != draftRevisionId) return EditorialResult.Invalid;

        Version++;
        Published = new PublishedKnowledge(Id, Draft.RevisionId, InstallationId,
            Draft.Question, Draft.Answer, Draft.Title, Draft.PublicUri, Version, now);
        Draft = null;
        return EditorialResult.Published;
    }

    public EditorialResult Withdraw(EditorialPrincipal actor, Guid publishedRevisionId, long expectedVersion)
    {
        if (!CanPublish(actor)) return EditorialResult.Forbidden;
        if (expectedVersion != Version) return EditorialResult.Conflict;
        if (Published is null || Published.RevisionId != publishedRevisionId)
            return EditorialResult.Invalid;
        Published = null;
        Version++;
        return EditorialResult.Withdrawn;
    }

    public PublishedKnowledge? CurrentPublication() => Published;

    private bool CanEdit(EditorialPrincipal? actor) => actor is not null &&
        actor.InstallationId == InstallationId && actor.HasArticleWriteAuthority &&
        string.Equals(actor.WriterNode, WriterNode, StringComparison.Ordinal) &&
        actor.Role is EditorialRoles.Editor or EditorialRoles.Admin or EditorialRoles.SuperAdmin;

    private bool CanPublish(EditorialPrincipal actor) => CanEdit(actor) &&
        actor.Role is EditorialRoles.Admin or EditorialRoles.SuperAdmin;

    private static bool ValidDraft(KnowledgeDraft d) =>
        d.RevisionId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(d.Question) && d.Question.Length <= 500 &&
        !string.IsNullOrWhiteSpace(d.Answer) && d.Answer.Length <= 10000 &&
        !string.IsNullOrWhiteSpace(d.Title) && d.Title.Length <= 200 &&
        d.PublicUri is { IsAbsoluteUri: true, Scheme: "https" } &&
        d.PublicUri.UserInfo.Length == 0 && d.PublicUri.HostNameType != UriHostNameType.Unknown;
}
