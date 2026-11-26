using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public sealed class IdentidadRevisionLocalTests
{
    private readonly Guid installation = Guid.NewGuid();
    private EditorialPrincipal Actor => new(installation, EditorialRoles.Admin, "principal", true);
    private static KnowledgeDraft Draft() => new(Guid.NewGuid(), "¿Horario?", "De lunes a viernes.",
        "Horario", new Uri("https://example.com/horario"));

    [Fact]
    public void ReutilizarRevisionActualNoCambiaContenidoNiVersion()
    {
        var article = new KnowledgeArticle(Guid.NewGuid(), installation, "principal");
        var draft = Draft();
        Assert.Equal(EditorialResult.Saved, article.SaveDraft(Actor, draft, 0));
        Assert.Equal(EditorialResult.Conflict,
            article.SaveDraft(Actor, draft with { Answer = "Otro contenido" }, 1));
        Assert.Equal(1, article.Version);
        Assert.Equal(draft, article.Draft);
    }

    [Fact]
    public void RevisionSustituidaTampocoPuedeReutilizarse()
    {
        var article = new KnowledgeArticle(Guid.NewGuid(), installation, "principal");
        var first = Draft();
        var second = Draft();
        article.SaveDraft(Actor, first, 0);
        article.SaveDraft(Actor, second, 1);
        Assert.Equal(EditorialResult.Conflict, article.SaveDraft(Actor, first, 2));
        Assert.Equal(second, article.Draft);
        Assert.Equal(2, article.Version);
    }

    [Fact]
    public void RetirarPublicacionNoLiberaIdentidadDeRevision()
    {
        var article = new KnowledgeArticle(Guid.NewGuid(), installation, "principal");
        var draft = Draft();
        article.SaveDraft(Actor, draft, 0);
        article.Publish(Actor, draft.RevisionId, 1, DateTimeOffset.UtcNow);
        article.Withdraw(Actor, draft.RevisionId, 2);
        Assert.Equal(EditorialResult.Conflict, article.SaveDraft(Actor, draft, 3));
        Assert.Null(article.Draft);
        Assert.Null(article.Published);
        Assert.Equal(3, article.Version);
    }

    private LocalEditorialKnowledgeStore Store() => new(
        new Dictionary<Guid, IReadOnlyCollection<string>> { [installation] = ["example.com"] });

    [Fact]
    public async Task DosArticulosConcurrentesNoCompartenIdentidadDeRevision()
    {
        var store = Store();
        var articles = new[] { Guid.NewGuid(), Guid.NewGuid() };
        foreach (var id in articles) store.Create(Actor, id);
        var draft = Draft();
        var results = await Task.WhenAll(articles.Select(id => Task.Run(() =>
            store.SaveDraft(Actor, id, draft, 0))));
        Assert.Single(results, r => r == EditorialResult.Saved);
        Assert.Single(results, r => r == EditorialResult.Conflict);
        Assert.Equal(1, articles.Sum(id => store.CurrentVersion(installation, id)!.Value));
    }

    [Fact]
    public void GuardadoRechazadoNoConsumeIdentidadDeRevision()
    {
        var store = Store();
        var id = Guid.NewGuid();
        store.Create(Actor, id);
        var draft = Draft();
        Assert.Equal(EditorialResult.Conflict, store.SaveDraft(Actor, id, draft, 1));
        Assert.Equal(EditorialResult.Saved, store.SaveDraft(Actor, id, draft, 0));
    }

    [Fact]
    public void ColisionNoRevelaRevisionASinAutoridad()
    {
        var store = Store();
        var id = Guid.NewGuid();
        store.Create(Actor, id);
        var draft = Draft();
        store.SaveDraft(Actor, id, draft, 0);
        Assert.Equal(EditorialResult.Forbidden,
            store.SaveDraft(Actor with { HasArticleWriteAuthority = false }, id, draft, 1));
    }
}
