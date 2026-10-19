using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public class ConsultaEditorialAutorizadaTests
{
    private readonly Guid _installation = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();

    private LocalEditorialKnowledgeStore Source() => new(
        new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [_installation] = ["ejemplo.com"], [_other] = ["otro.com"]
        });

    private EditorialPrincipal Principal(Guid? installation = null, string node = "nodo-1",
        string role = EditorialRoles.Admin, bool authority = true) =>
        new(installation ?? _installation, role, node, authority);

    [Fact]
    public void EditorAutorizadoVeBorradorSinPublicarlo()
    {
        var store = Source();
        var id = Guid.NewGuid();
        var actor = Principal(role: EditorialRoles.Editor);
        var draft = new KnowledgeDraft(Guid.NewGuid(), "Horario", "Abrimos temprano", "Horarios",
            new Uri("https://ejemplo.com/horarios"));
        Assert.Equal(EditorialResult.Saved, store.Create(actor, id));
        Assert.Equal(EditorialResult.Saved, store.SaveDraft(actor, id, draft, 0));
        var state = store.Inspect(actor, id);
        Assert.Equal(EditorialLookupStatus.Found, state.Status);
        var article = state.Article!;
        Assert.Equal(draft, article.Draft);
        Assert.Null(article.Published);
        Assert.Equal(1, article.Version);
    }

    [Fact]
    public void OtroInquilinoNoVeBorradores()
    {
        var store = Source(); var id = Guid.NewGuid();
        store.Create(Principal(), id);
        Assert.Equal(EditorialLookupStatus.NotFound,
            store.Inspect(Principal(installation: _other), id).Status);
    }

    [Fact]
    public void OtroEscritorNoVeBorradores()
    {
        var store = Source(); var id = Guid.NewGuid();
        store.Create(Principal(), id);
        Assert.Equal(EditorialLookupStatus.NotFound,
            store.Inspect(Principal(node: "otro-nodo"), id).Status);
    }

    [Fact]
    public void SinPermisosNoVeEstadoEditorial()
    {
        var store = Source(); var id = Guid.NewGuid();
        store.Create(Principal(), id);
        Assert.Equal(EditorialLookupStatus.Forbidden,
            store.Inspect(Principal(authority: false), id).Status);
        Assert.Null(store.Inspect(Principal(authority: false), id).Article);
    }

    [Fact]
    public void VersionYPublicacionCoincidenConFuenteFaq()
    {
        var store = Source(); var id = Guid.NewGuid(); var actor = Principal();
        var draft = new KnowledgeDraft(Guid.NewGuid(), "Horario", "Abrimos temprano", "Horarios",
            new Uri("https://ejemplo.com/horarios"));
        store.Create(actor, id);
        store.SaveDraft(actor, id, draft, 0);
        Assert.Equal(EditorialResult.Published,
            store.Publish(actor, id, draft.RevisionId, 1, DateTimeOffset.UtcNow));
        var state = store.Inspect(actor, id).Article!;
        Assert.Null(state.Draft);
        Assert.Equal(draft.RevisionId, state.Published!.RevisionId);
        Assert.Equal(2, state.Version);
        Assert.Equal(state.Published, store.ReadCurrent(_installation, id).Publication);
    }
}
