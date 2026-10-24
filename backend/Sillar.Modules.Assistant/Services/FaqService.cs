using Sillar.Modules.Assistant.Domain;

namespace Sillar.Modules.Assistant.Services;

/// <summary>
/// FAQ determinista: no contiene LLM, no interroga módulos comerciales,
/// no asume que un índice o una copia prueben vigencia.
/// </summary>
public sealed class FaqService(ILocalKnowledgeAuthority source)
{
    private const int MaxCandidates = 20;

    public FaqResult Answer(Guid installationId, string? question, DateTimeOffset now)
    {
        // La fuente y sus colecciones son una frontera de confianza: también
        // pueden fallar al enumerar o devolver evidencia incompleta.
        try { return AnswerVerified(installationId, question, now); }
        catch { return FaqResult.Abstain(FaqOutcome.Unavailable); }
    }

    private FaqResult AnswerVerified(Guid installationId, string? question, DateTimeOffset now)
    {
        if (installationId == Guid.Empty || string.IsNullOrWhiteSpace(question) || question.Length > 500)
            return FaqResult.Abstain(FaqOutcome.Insufficient);

        var normalized = Normalize(question);
        if (normalized.Length == 0) return FaqResult.Abstain(FaqOutcome.Insufficient);

        IReadOnlyList<Guid> candidates;
        try { candidates = source.FindCandidateArticleIds(installationId, normalized, MaxCandidates); }
        catch { return FaqResult.Abstain(FaqOutcome.Unavailable); }

        if (candidates is null || candidates.Count > MaxCandidates)
            return FaqResult.Abstain(FaqOutcome.Unavailable);

        PublishedKnowledge? selected = null;
        var seen = new HashSet<Guid>();
        foreach (var candidateId in candidates)
        {
            if (candidateId == Guid.Empty || !seen.Add(candidateId)) continue;
            KnowledgeRead current;
            try { current = source.ReadCurrent(installationId, candidateId); }
            catch { return FaqResult.Abstain(FaqOutcome.Unavailable); }

            if (current.Status == KnowledgeReadStatus.Indeterminate)
                return FaqResult.Abstain(FaqOutcome.Unavailable);
            if (!Eligible(current, installationId, candidateId, now)) continue;
            var publication = current.Publication!;
            if (Normalize(publication.Question) != normalized) continue;

            // Dos respuestas vigentes exactas diferentes son ambiguas, no elegimos al azar.
            if (selected is not null) return FaqResult.Abstain(FaqOutcome.Insufficient);
            selected = publication;
        }

        if (selected is null) return FaqResult.Abstain(FaqOutcome.Insufficient);

        try
        {
            if (!source.IsAllowedPublicUri(installationId, selected.PublicUri))
                return FaqResult.Abstain(FaqOutcome.Unavailable);
        }
        catch { return FaqResult.Abstain(FaqOutcome.Unavailable); }

        // La política URL también consulta a la fuente: puede observar o provocar
        // cambios editoriales. Revalidar DESPUÉS y no invocar más callbacks antes
        // de construir la respuesta. No garantiza revocación posterior a esta lectura.
        KnowledgeRead last;
        try { last = source.ReadCurrent(installationId, selected.ArticleId); }
        catch { return FaqResult.Abstain(FaqOutcome.Unavailable); }
        if (!Eligible(last, installationId, selected.ArticleId, now) ||
            last.Publication != selected)
            return FaqResult.Abstain(FaqOutcome.Unavailable);

        return new FaqResult(FaqOutcome.Answered, selected.Answer, selected.ArticleId,
            selected.RevisionId, selected.Title, selected.PublicUri.AbsoluteUri);
    }

    private static bool Eligible(KnowledgeRead read, Guid installationId, Guid articleId, DateTimeOffset now)
    {
        if (read.Status != KnowledgeReadStatus.Authoritative || !read.IsLocalAuthority ||
            !read.IsCurrentPublished || read.IsWithdrawn || read.Publication is null ||
            (read.ExpiresAt is { } until && until <= now)) return false;
        var p = read.Publication;
        return p.InstallationId == installationId && p.ArticleId == articleId &&
            p.RevisionId != Guid.Empty && p.ArticleVersion > 0 && p.PublishedAt <= now &&
            !string.IsNullOrWhiteSpace(p.Title) && !string.IsNullOrWhiteSpace(p.Question) &&
            !string.IsNullOrWhiteSpace(p.Answer) &&
            p.PublicUri is { IsAbsoluteUri: true, Scheme: "https" } &&
            p.PublicUri.UserInfo.Length == 0;
    }

    public static string Normalize(string text) => string.Join(" ",
        text.Trim().ToLowerInvariant().Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
