using Sillar.Modules.Assistant.Domain;

namespace Sillar.Modules.Assistant.Services;

/// <summary>
/// Implementación local VOLÁTIL de referencia para integrar dominio y FAQ durante desarrollo.
/// No implementa persistencia, autorizaciones CORE, auditoría CORE ni garantía multinodo.
/// Nunca se registra automáticamente en el host ni se usa como autoridad de producción.
/// </summary>
public sealed class LocalEditorialKnowledgeStore : ILocalKnowledgeAuthority
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, KnowledgeArticle> _articles = new();
    private readonly Dictionary<Guid, HashSet<string>> _hosts = new();

    /// <summary>Exige dominios públicos permitidos por instalación, sin comodines ni valor implícito.</summary>
    public LocalEditorialKnowledgeStore(IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> allowedHosts)
    {
        ArgumentNullException.ThrowIfNull(allowedHosts);
        foreach (var (installation, hosts) in allowedHosts)
        {
            if (installation == Guid.Empty || hosts is null || hosts.Count == 0)
                throw new ArgumentException("Cada instalación requiere dominios explícitos.", nameof(allowedHosts));
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var host in hosts)
            {
                if (string.IsNullOrWhiteSpace(host) || host.Trim() != host ||
                    host.Contains('/') || host.Contains(':') || host.Contains('*') ||
                    Uri.CheckHostName(host) == UriHostNameType.Unknown)
                    throw new ArgumentException("Dominio público inválido.", nameof(allowedHosts));
                set.Add(host);
            }
            _hosts.Add(installation, set);
        }
    }

    /// <summary>La identidad de actor debe provenir de un adaptador de sesión confiable, nunca del cuerpo HTTP.</summary>
    public EditorialResult Create(EditorialPrincipal? actor, Guid articleId)
    {
        if (actor is null || !Authorized(actor) || articleId == Guid.Empty ||
            !_hosts.ContainsKey(actor.InstallationId)) return EditorialResult.Forbidden;
        lock (_gate)
        {
            if (_articles.ContainsKey(articleId)) return EditorialResult.Conflict;
            _articles.Add(articleId, new KnowledgeArticle(articleId, actor.InstallationId, actor.WriterNode));
            return EditorialResult.Saved;
        }
    }

    public EditorialResult SaveDraft(EditorialPrincipal actor, Guid articleId,
        KnowledgeDraft draft, long expectedVersion)
    {
        lock (_gate)
        {
            if (!_articles.TryGetValue(articleId, out var article) || actor is null ||
                !Authorized(actor) || actor.InstallationId != article.InstallationId ||
                !string.Equals(actor.WriterNode, article.WriterNode, StringComparison.Ordinal))
                return EditorialResult.Forbidden;
            if (!IsAllowedPublicUri(article.InstallationId, draft?.PublicUri)) return EditorialResult.Invalid;
            return article.SaveDraft(actor, draft, expectedVersion);
        }
    }

    public EditorialResult Publish(EditorialPrincipal actor, Guid articleId,
        Guid revisionId, long expectedVersion, DateTimeOffset now)
    {
        lock (_gate)
        {
            return _articles.TryGetValue(articleId, out var article)
                ? article.Publish(actor, revisionId, expectedVersion, now)
                : EditorialResult.Forbidden;
        }
    }

    public EditorialResult Withdraw(EditorialPrincipal actor, Guid articleId,
        Guid revisionId, long expectedVersion)
    {
        lock (_gate)
        {
            return _articles.TryGetValue(articleId, out var article)
                ? article.Withdraw(actor, revisionId, expectedVersion)
                : EditorialResult.Forbidden;
        }
    }

    public long? CurrentVersion(Guid installationId, Guid articleId)
    {
        lock (_gate)
        {
            return _articles.TryGetValue(articleId, out var article) &&
                   article.InstallationId == installationId ? article.Version : null;
        }
    }

    public IReadOnlyList<Guid> FindCandidateArticleIds(Guid installationId, string normalizedQuestion, int maxCandidates)
    {
        if (installationId == Guid.Empty || string.IsNullOrWhiteSpace(normalizedQuestion) ||
            maxCandidates is < 1 or > 20) return [];
        lock (_gate)
        {
            // Sin índice externo: búsqueda exacta reproducible y acotada sobre publicaciones vigentes.
            // No devolver más de maxCandidates: el motor FAQ abstiene ante ambigüedad.
            return _articles.Values
                .Where(a => a.InstallationId == installationId && a.Published is not null &&
                    FaqService.Normalize(a.Published.Question) == normalizedQuestion)
                .OrderBy(a => a.Id)
                .Take(maxCandidates)
                .Select(a => a.Id)
                .ToArray();
        }
    }

    public KnowledgeRead ReadCurrent(Guid installationId, Guid articleId)
    {
        lock (_gate)
        {
            if (!_hosts.ContainsKey(installationId) ||
                !_articles.TryGetValue(articleId, out var article) ||
                article.InstallationId != installationId || article.Published is null)
                return new KnowledgeRead(KnowledgeReadStatus.NotFound, null, true, false, false, null);
            return new KnowledgeRead(KnowledgeReadStatus.Authoritative,
                article.Published, true, true, false, null);
        }
    }

    public bool IsAllowedPublicUri(Guid installationId, Uri? uri) =>
        uri is { IsAbsoluteUri: true, Scheme: "https" } &&
        uri.UserInfo.Length == 0 &&
        uri.Port == 443 &&
        _hosts.TryGetValue(installationId, out var allowed) &&
        allowed.Contains(uri.IdnHost);

    private static bool Authorized(EditorialPrincipal actor) =>
        actor.InstallationId != Guid.Empty &&
        actor.HasArticleWriteAuthority &&
        !string.IsNullOrWhiteSpace(actor.WriterNode) &&
        actor.Role is EditorialRoles.Editor or EditorialRoles.Admin or EditorialRoles.SuperAdmin;
}
