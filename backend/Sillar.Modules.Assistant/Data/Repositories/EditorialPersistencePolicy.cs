using Sillar.Modules.Assistant.Data.Entities;
using Sillar.Modules.Assistant.Domain;

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
                normalized.Add(host);
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
