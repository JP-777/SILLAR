using Sillar.Modules.Assistant.Data.Entities;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Data.Repositories;

/// <summary>
/// Política explícita de la instalación; sus datos han de provenir de configuración confiable
/// del servidor. No deriva dominios, roles ni nodos del cuerpo de una petición pública.
/// </summary>
public sealed class EditorialPersistencePolicy
{
    private readonly IReadOnlyDictionary<Guid, HashSet<string>> _hosts;

    public EditorialPersistencePolicy(IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> allowedHosts)
    {
        ArgumentNullException.ThrowIfNull(allowedHosts);
        var hostsByInstallation = new Dictionary<Guid, HashSet<string>>();
        foreach (var (installationId, hosts) in allowedHosts)
        {
            if (installationId == Guid.Empty || hosts is null || hosts.Count == 0)
                throw new ArgumentException("Toda instalación necesita dominios públicos explícitos.", nameof(allowedHosts));
            var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var host in hosts)
            {
                if (string.IsNullOrWhiteSpace(host) || host != host.Trim() ||
                    host.Contains('/') || host.Contains(':') || host.Contains('*') ||
                    Uri.CheckHostName(host) == UriHostNameType.Unknown)
                    throw new ArgumentException("Dominio público no válido.", nameof(allowedHosts));
                // La configuración y los enlaces deben comparar la misma forma IDN.
                normalized.Add(new UriBuilder(Uri.UriSchemeHttps, host).Uri.IdnHost);
            }
            hostsByInstallation.Add(installationId, normalized);
        }
        _hosts = hostsByInstallation;
    }

    public bool HasInstallation(Guid installationId) =>
        installationId != Guid.Empty && _hosts.ContainsKey(installationId);

    public bool CanCreate(EditorialPrincipal? actor) =>
        actor is not null && HasInstallation(actor.InstallationId) &&
        actor.HasArticleWriteAuthority && !string.IsNullOrWhiteSpace(actor.WriterNode) &&
        actor.WriterNode.Length <= 128 &&
        actor.Role is EditorialRoles.Editor or EditorialRoles.Admin or EditorialRoles.SuperAdmin;

    public bool CanEdit(EditorialPrincipal? actor, EditorialArticleRow article) =>
        CanCreate(actor) && actor!.InstallationId == article.InstallationId &&
        string.Equals(actor.WriterNode, article.WriterNode, StringComparison.Ordinal);

    public bool CanPublish(EditorialPrincipal? actor, EditorialArticleRow article) =>
        CanEdit(actor, article) &&
        actor!.Role is EditorialRoles.Admin or EditorialRoles.SuperAdmin;

    public static bool MatchesVersion(EditorialArticleRow article, long expectedVersion) =>
        expectedVersion >= 0 && article.Version == expectedVersion && article.Version < long.MaxValue;

    /// <summary>
    /// Un puntero por sí solo no convierte una revisión en fuente vigente. La versión
    /// de publicación debe pertenecer a la historia de la raíz y las marcas de
    /// tiempo no pueden situar la publicación antes de crear la revisión.
    /// </summary>
    public static bool IsCurrentPublishedRevision(EditorialArticleRow article,
        EditorialRevisionRow revision) =>
        article.Id != Guid.Empty && article.InstallationId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(article.WriterNode) &&
        article.CurrentPublishedRevisionId == revision.Id &&
        revision.Id != Guid.Empty && revision.ArticleId == article.Id &&
        revision.InstallationId == article.InstallationId &&
        revision.PublishedAt is { } publishedAt &&
        revision.PublishedArticleVersion is { } publishedVersion &&
        publishedVersion > 0 && publishedVersion <= article.Version &&
        article.CreatedAt <= revision.CreatedAt && revision.CreatedAt <= publishedAt &&
        !string.IsNullOrWhiteSpace(revision.Question) &&
        !string.IsNullOrWhiteSpace(revision.QuestionKey) &&
        revision.QuestionKey == FaqService.Normalize(revision.Question);

    public bool IsValidDraft(Guid installationId, KnowledgeDraft? draft) =>
        draft is not null && draft.RevisionId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(draft.Question) && draft.Question.Length <= 500 &&
        !string.IsNullOrWhiteSpace(draft.Answer) && draft.Answer.Length <= 10000 &&
        !string.IsNullOrWhiteSpace(draft.Title) && draft.Title.Length <= 200 &&
        IsAllowedPublicUri(installationId, draft.PublicUri) &&
        draft.PublicUri.AbsoluteUri.Length <= 2048;

    public bool IsAllowedPublicUri(Guid installationId, Uri? uri) =>
        uri is { IsAbsoluteUri: true, Scheme: "https" } &&
        uri.UserInfo.Length == 0 && uri.Port == 443 &&
        _hosts.TryGetValue(installationId, out var domains) && domains.Contains(uri.IdnHost);
}
