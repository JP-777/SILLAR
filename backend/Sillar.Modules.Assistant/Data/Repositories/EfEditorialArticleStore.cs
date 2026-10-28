using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Assistant.Data.Entities;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Data.Repositories;

/// <summary>
/// Adaptador EF de la autoridad editorial local de M12. No se registra en el host.
/// Cada instancia requiere un DbContext scoped, sin reutilizarlo tras una excepción de escritura.
/// Las mutaciones usan SaveChanges, cuya unidad transaccional incluye raíz y revisión.
/// La seguridad multinodo y la auditoría CORE todavía no están acreditadas.
/// </summary>
public sealed class EfEditorialArticleStore(
    AssistantDbContext db, EditorialPersistencePolicy policy, TimeProvider clock)
{
    public async Task<EditorialResult> CreateAsync(
        EditorialPrincipal? actor, Guid articleId, CancellationToken ct = default)
    {
        if (!policy.CanCreate(actor)) return EditorialResult.Forbidden;
        if (articleId == Guid.Empty) return EditorialResult.Invalid;
        if (await db.Articles.AsNoTracking().AnyAsync(x => x.Id == articleId, ct))
            return EditorialResult.Conflict;
        db.Articles.Add(new EditorialArticleRow
        {
            Id = articleId,
            InstallationId = actor!.InstallationId,
            WriterNode = actor.WriterNode,
            Version = 0,
            CreatedAt = clock.GetUtcNow()
        });
        try
        {
            await db.SaveChangesAsync(ct);
            return EditorialResult.Saved;
        }
        catch (DbUpdateException ex) when (EditorialDatabaseConflict.IsArticlePrimaryKeyCollision(ex))
        {
            // Al perder la carrera de inserción, no conservar una entidad Added rastreada.
            // Otras violaciones de unicidad, conexión o integridad se propagan intactas.
            db.ChangeTracker.Clear();
            return EditorialResult.Conflict;
        }
    }

    public async Task<EditorialResult> SaveDraftAsync(
        EditorialPrincipal? actor, Guid articleId, KnowledgeDraft? draft,
        long expectedVersion, CancellationToken ct = default)
    {
        if (!policy.CanCreate(actor)) return EditorialResult.Forbidden;
        if (articleId == Guid.Empty || !policy.IsValidDraft(actor!.InstallationId, draft))
            return EditorialResult.Invalid;
        var article = await FindWritableArticleAsync(actor, articleId, ct);
        if (article is null) return EditorialResult.Forbidden;
        if (!policy.CanEdit(actor, article)) return EditorialResult.Forbidden;
        if (!EditorialPersistencePolicy.MatchesVersion(article, expectedVersion))
            return EditorialResult.Conflict;
        if (await db.Revisions.AsNoTracking().AnyAsync(x => x.Id == draft!.RevisionId, ct))
            return EditorialResult.Conflict;

        db.Revisions.Add(new EditorialRevisionRow
        {
            Id = draft!.RevisionId,
            InstallationId = article.InstallationId,
            ArticleId = article.Id,
            Question = draft.Question,
            QuestionKey = FaqService.Normalize(draft.Question),
            Answer = draft.Answer,
            Title = draft.Title,
            PublicUrl = draft.PublicUri.AbsoluteUri,
            CreatedAt = clock.GetUtcNow()
        });
        article.CurrentDraftRevisionId = draft.RevisionId;
        article.Version++;
        return await CommitAsync(EditorialResult.Saved, ct);
    }

    public async Task<EditorialResult> PublishAsync(
        EditorialPrincipal? actor, Guid articleId, Guid revisionId,
        long expectedVersion, CancellationToken ct = default)
    {
        if (!policy.CanCreate(actor)) return EditorialResult.Forbidden;
        if (articleId == Guid.Empty || revisionId == Guid.Empty) return EditorialResult.Invalid;
        var article = await FindWritableArticleAsync(actor!, articleId, ct);
        if (article is null || !policy.CanPublish(actor, article)) return EditorialResult.Forbidden;
        if (!EditorialPersistencePolicy.MatchesVersion(article, expectedVersion))
            return EditorialResult.Conflict;
        if (article.CurrentDraftRevisionId != revisionId) return EditorialResult.Invalid;

        var revision = await db.Revisions.SingleOrDefaultAsync(x =>
            x.Id == revisionId && x.ArticleId == article.Id &&
            x.InstallationId == article.InstallationId, ct);
        if (revision is null || revision.PublishedAt is not null ||
            revision.PublishedArticleVersion is not null ||
            !Uri.TryCreate(revision.PublicUrl, UriKind.Absolute, out var uri) ||
            !policy.IsAllowedPublicUri(article.InstallationId, uri))
            return EditorialResult.Invalid;

        var publicationVersion = checked(article.Version + 1);
        revision.PublishedAt = clock.GetUtcNow();
        revision.PublishedArticleVersion = publicationVersion;
        article.CurrentPublishedRevisionId = revision.Id;
        article.CurrentDraftRevisionId = null;
        article.Version = publicationVersion;
        return await CommitAsync(EditorialResult.Published, ct);
    }

    public async Task<EditorialResult> WithdrawAsync(
        EditorialPrincipal? actor, Guid articleId, Guid revisionId,
        long expectedVersion, CancellationToken ct = default)
    {
        if (!policy.CanCreate(actor)) return EditorialResult.Forbidden;
        if (articleId == Guid.Empty || revisionId == Guid.Empty) return EditorialResult.Invalid;
        var article = await FindWritableArticleAsync(actor!, articleId, ct);
        if (article is null || !policy.CanPublish(actor, article)) return EditorialResult.Forbidden;
        if (!EditorialPersistencePolicy.MatchesVersion(article, expectedVersion))
            return EditorialResult.Conflict;
        if (article.CurrentPublishedRevisionId != revisionId) return EditorialResult.Invalid;

        article.CurrentPublishedRevisionId = null;
        article.Version++;
        return await CommitAsync(EditorialResult.Withdrawn, ct);
    }

    /// <summary>Lectura privada: no exponer sin contexto de instalación y autor CORE confiables.</summary>
    public async Task<EditorialArticleLookup> InspectAsync(
        EditorialPrincipal? actor, Guid articleId, CancellationToken ct = default)
    {
        if (!policy.CanCreate(actor))
            return new(EditorialLookupStatus.Forbidden, null);
        var article = await db.Articles.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == articleId && x.InstallationId == actor!.InstallationId, ct);
        if (article is null || !policy.CanEdit(actor, article))
            return new(EditorialLookupStatus.NotFound, null);

        KnowledgeDraft? draft = null;
        if (article.CurrentDraftRevisionId is { } draftId)
        {
            var revision = await db.Revisions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == draftId && x.ArticleId == article.Id &&
                x.InstallationId == article.InstallationId, ct);
            if (revision is null || !Uri.TryCreate(revision.PublicUrl, UriKind.Absolute, out var uri))
                return new(EditorialLookupStatus.NotFound, null);
            draft = new KnowledgeDraft(revision.Id, revision.Question, revision.Answer, revision.Title, uri);
        }

        var pub = await CurrentPublicationAsync(article, ct);
        if (article.CurrentPublishedRevisionId is not null && pub.Status != KnowledgeReadStatus.Authoritative)
            return new(EditorialLookupStatus.NotFound, null);
        return new(EditorialLookupStatus.Found,
            new EditorialArticleSnapshot(article.Id, article.InstallationId,
                article.WriterNode, article.Version, draft, pub.Publication));
    }

    /// <summary>Lectura para el futuro adaptador FAQ: ausencia de revisión válida implica incertidumbre.</summary>
    public async Task<KnowledgeRead> ReadCurrentAsync(
        Guid installationId, Guid articleId, CancellationToken ct = default)
    {
        if (!policy.HasInstallation(installationId) || articleId == Guid.Empty)
            return Missing();
        var article = await db.Articles.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == articleId && x.InstallationId == installationId, ct);
        return article is null ? Missing() : await CurrentPublicationAsync(article, ct);
    }

    private async Task<KnowledgeRead> CurrentPublicationAsync(
        EditorialArticleRow article, CancellationToken ct)
    {
        if (article.CurrentPublishedRevisionId is not { } revisionId)
            return Missing();
        var revision = await db.Revisions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == revisionId && x.ArticleId == article.Id &&
            x.InstallationId == article.InstallationId, ct);
        if (revision is null || !EditorialPersistencePolicy.IsCurrentPublishedRevision(article, revision) ||
            !Uri.TryCreate(revision.PublicUrl, UriKind.Absolute, out var uri) ||
            !policy.IsAllowedPublicUri(article.InstallationId, uri) ||
            string.IsNullOrWhiteSpace(revision.Question) ||
            string.IsNullOrWhiteSpace(revision.Answer) ||
            string.IsNullOrWhiteSpace(revision.Title))
            return new(KnowledgeReadStatus.Indeterminate, null, false, false, false, null);

        return new(KnowledgeReadStatus.Authoritative,
            new PublishedKnowledge(article.Id, revision.Id, article.InstallationId,
                revision.Question, revision.Answer, revision.Title, uri,
                revision.PublishedArticleVersion!.Value, revision.PublishedAt!.Value),
            true, true, false, null);
    }

    private Task<EditorialArticleRow?> FindWritableArticleAsync(
        EditorialPrincipal actor, Guid articleId, CancellationToken ct)
        => db.Articles.SingleOrDefaultAsync(x =>
            x.Id == articleId && x.InstallationId == actor.InstallationId, ct);

    private async Task<EditorialResult> CommitAsync(EditorialResult success, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return success;
        }
        catch (DbUpdateConcurrencyException)
        {
            // No permitir reintentar accidentalmente con entradas ya rastreadas.
            db.ChangeTracker.Clear();
            return EditorialResult.Conflict;
        }
        catch (DbUpdateException ex) when (
            success == EditorialResult.Saved &&
            EditorialDatabaseConflict.IsRevisionPrimaryKeyCollision(ex))
        {
            // Dos guardados pueden superar AnyAsync antes de la inserción.
            // Solo la PK física de revisiones es un conflicto; NO ocultar FKs,
            // checks, otras unicidades, fallos de conexión ni errores generales.
            db.ChangeTracker.Clear();
            return EditorialResult.Conflict;
        }
    }

    private static KnowledgeRead Missing() =>
        new(KnowledgeReadStatus.NotFound, null, true, false, false, null);
}
