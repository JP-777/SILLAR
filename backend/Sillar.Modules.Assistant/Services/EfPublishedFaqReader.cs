using System.Data;
using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Assistant.Data;
using Sillar.Modules.Assistant.Data.Repositories;
using Sillar.Modules.Assistant.Domain;

namespace Sillar.Modules.Assistant.Services;

/// <summary>Consulta asíncrona de FAQ publicada, aislada por instalación y sin motores externos.</summary>
public interface IAsyncPublishedFaqReader
{
    Task<FaqResult> AnswerAsync(Guid installationId, string? question,
        DateTimeOffset now, CancellationToken ct);
}

/// <summary>
/// Lee únicamente revisiones vigentes apuntadas por sus artículos. Una clave normalizada
/// sirve para localizar candidatos, pero solo una relectura de la autoridad local permite citar.
/// La garantía física de concurrencia y los cambios de esquema requieren validación posterior.
/// </summary>
public sealed class EfPublishedFaqReader(
    AssistantDbContext db, EfEditorialArticleStore editorial, EditorialPersistencePolicy policy)
    : IAsyncPublishedFaqReader
{
    private const int MaxCandidates = 20;

    /// <summary>
    /// Una sola fotografia transaccional PostgreSQL para candidatos y revalidacion.
    /// Esto evita lecturas mixtas en una solicitud; no acredita revocacion instantanea
    /// despues de finalizar la transaccion ni reemplaza pruebas fisicas de PostgreSQL.
    /// </summary>
    public static IsolationLevel RequiredReadIsolation => IsolationLevel.RepeatableRead;

    public async Task<FaqResult> AnswerAsync(Guid installationId, string? question,
        DateTimeOffset now, CancellationToken ct)
    {
        if (!policy.HasInstallation(installationId) || string.IsNullOrWhiteSpace(question) ||
            question.Length > 500)
            return FaqResult.Abstain(FaqOutcome.Insufficient);

        var key = FaqService.Normalize(question);
        if (key.Length == 0) return FaqResult.Abstain(FaqOutcome.Insufficient);
        try
        {
            // Todas las consultas EF de esta solicitud (incluido el repositorio editorial
            // scoped que comparte este DbContext) observan la misma fotografia PostgreSQL.
            // Si no puede abrirse la transaccion, fallar cerrado: nunca degradar a ReadCommitted.
            await using var snapshot = await db.Database.BeginTransactionAsync(
                RequiredReadIsolation, ct);

            // Igualdad bajo colación binaria explícita: nunca usar core.es_ci para identidad.
            var candidates = await (
                from article in db.Articles.AsNoTracking()
                join revision in db.Revisions.AsNoTracking()
                    on article.CurrentPublishedRevisionId equals (Guid?)revision.Id
                where article.InstallationId == installationId &&
                    revision.InstallationId == installationId && revision.QuestionKey == key
                orderby article.Id
                select article.Id
            ).Take(MaxCandidates + 1).ToListAsync(ct);

            if (candidates.Count > MaxCandidates)
                return FaqResult.Abstain(FaqOutcome.Unavailable);

            PublishedKnowledge? selected = null;
            foreach (var id in candidates)
            {
                var read = await editorial.ReadCurrentAsync(installationId, id, ct);
                if (!Eligible(read, installationId, id, key, now))
                    return FaqResult.Abstain(FaqOutcome.Unavailable);
                if (selected is not null)
                    return FaqResult.Abstain(FaqOutcome.Insufficient);
                selected = read.Publication;
            }
            if (selected is null) return FaqResult.Abstain(FaqOutcome.Insufficient);

            // La consulta índice no es autoridad. Releer después de todas las comprobaciones.
            var last = await editorial.ReadCurrentAsync(installationId, selected.ArticleId, ct);
            if (!Eligible(last, installationId, selected.ArticleId, key, now) ||
                last.Publication != selected)
                return FaqResult.Abstain(FaqOutcome.Unavailable);

            // La evidencia se verifico dentro de una unica fotografia consistente.
            // Una publicacion retirada DESPUES de este snapshot exige una nueva solicitud;
            // no anunciar una garantia de revocacion instantanea al momento de enviar HTTP.
            await snapshot.CommitAsync(ct);
            return new FaqResult(FaqOutcome.Answered, selected.Answer, selected.ArticleId,
                selected.RevisionId, selected.Title, selected.PublicUri.AbsoluteUri);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return FaqResult.Abstain(FaqOutcome.Unavailable);
        }
    }

    /// <summary>Valida evidencia local y fuente pública antes de usarla como respuesta.</summary>
    public bool Eligible(KnowledgeRead read, Guid installationId, Guid articleId,
        string normalizedQuestion, DateTimeOffset now)
    {
        if (read.Status != KnowledgeReadStatus.Authoritative || !read.IsLocalAuthority ||
            !read.IsCurrentPublished || read.IsWithdrawn ||
            (read.ExpiresAt is { } until && until <= now) ||
            read.Publication is not { } p) return false;
        return p.InstallationId == installationId && p.ArticleId == articleId &&
            p.RevisionId != Guid.Empty && p.ArticleVersion > 0 && p.PublishedAt <= now &&
            !string.IsNullOrWhiteSpace(p.Answer) && !string.IsNullOrWhiteSpace(p.Title) &&
            FaqService.Normalize(p.Question) == normalizedQuestion &&
            policy.IsAllowedPublicUri(installationId, p.PublicUri);
    }
}
