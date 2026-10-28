using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

/// <summary>
/// Intercalaciones deterministas con el almacén editorial real en memoria.
/// No prueban concurrencia PostgreSQL, revocación tras la última lectura ni M16.
/// </summary>
public sealed class RevalidacionFinalFaqTests
{
    private readonly Guid installation = Guid.NewGuid();
    private readonly Guid article = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private EditorialPrincipal Actor => new(installation, EditorialRoles.Admin, "principal", true);
    private static KnowledgeDraft Draft(string answer = "De lunes a viernes.") => new(
        Guid.NewGuid(), "¿Horario?", answer, "Horario", new Uri("https://example.com/horario"));

    private (LocalEditorialKnowledgeStore Store, KnowledgeDraft Published) PublishedStore()
    {
        var store = new LocalEditorialKnowledgeStore(new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [installation] = ["example.com"]
        });
        var draft = Draft();
        Assert.Equal(EditorialResult.Saved, store.Create(Actor, article));
        Assert.Equal(EditorialResult.Saved, store.SaveDraft(Actor, article, draft, 0));
        Assert.Equal(EditorialResult.Published, store.Publish(Actor, article, draft.RevisionId, 1, Now));
        return (store, draft);
    }

    [Fact]
    public void RetiradaDurantePoliticaDeUrlSuprimeRespuestaYCita()
    {
        var (store, publication) = PublishedStore();
        var source = new InterleavedSource(store, () =>
            Assert.Equal(EditorialResult.Withdrawn,
                store.Withdraw(Actor, article, publication.RevisionId, 2)));
        var result = new FaqService(source).Answer(installation, publication.Question, Now);
        AssertAbstention(result);
        Assert.Equal("read", source.Operations[^1]);
        Assert.Equal(FaqOutcome.Insufficient, new FaqService(store).Answer(installation, publication.Question, Now).Outcome);
    }

    [Fact]
    public void SustitucionDurantePoliticaDeUrlNoEmiteRevisionAnteriorNiMezclaContenido()
    {
        var (store, publication) = PublishedStore();
        var replacement = Draft("Solo sábados.");
        var source = new InterleavedSource(store, () =>
        {
            Assert.Equal(EditorialResult.Saved, store.SaveDraft(Actor, article, replacement, 2));
            Assert.Equal(EditorialResult.Published, store.Publish(Actor, article, replacement.RevisionId, 3, Now));
        });
        AssertAbstention(new FaqService(source).Answer(installation, publication.Question, Now));
        var next = new FaqService(store).Answer(installation, publication.Question, Now);
        Assert.Equal(FaqOutcome.Answered, next.Outcome);
        Assert.Equal(replacement.RevisionId, next.RevisionId);
        Assert.Equal(replacement.Answer, next.Answer);
    }

    [Fact]
    public void NuevoBorradorDurantePoliticaDeUrlConservaSoloPublicacionVigente()
    {
        var (store, publication) = PublishedStore();
        var draft = Draft("Contenido privado sin publicar.");
        var source = new InterleavedSource(store, () =>
            Assert.Equal(EditorialResult.Saved, store.SaveDraft(Actor, article, draft, 2)));
        var result = new FaqService(source).Answer(installation, publication.Question, Now);
        Assert.Equal(FaqOutcome.Answered, result.Outcome);
        Assert.Equal(publication.RevisionId, result.RevisionId);
        Assert.Equal(publication.Answer, result.Answer);
        Assert.DoesNotContain(draft.Answer, result.Answer);
        Assert.Equal(new[] { "candidates", "read", "url", "read" }, source.Operations);
    }

    [Fact]
    public void PerdidaDeAutoridadDurantePoliticaDeUrlObligaAAbstenerse()
    {
        var (store, publication) = PublishedStore();
        var authority = true;
        var source = new InterleavedSource(store, () => authority = false, () => authority);
        AssertAbstention(new FaqService(source).Answer(installation, publication.Question, Now));
    }

    private static void AssertAbstention(FaqResult result)
    {
        Assert.Equal(FaqOutcome.Unavailable, result.Outcome);
        Assert.Equal(FaqResult.Abstain(FaqOutcome.Unavailable), result);
    }

    private sealed class InterleavedSource(LocalEditorialKnowledgeStore store, Action duringUrl,
        Func<bool>? hasAuthority = null) : ILocalKnowledgeAuthority
    {
        public List<string> Operations { get; } = [];
        public IReadOnlyList<Guid> FindCandidateArticleIds(Guid id, string question, int limit)
        {
            Operations.Add("candidates");
            return store.FindCandidateArticleIds(id, question, limit);
        }
        public KnowledgeRead ReadCurrent(Guid id, Guid articleId)
        {
            Operations.Add("read");
            var read = store.ReadCurrent(id, articleId);
            return hasAuthority is not null && !hasAuthority()
                ? read with { IsLocalAuthority = false } : read;
        }
        public bool IsAllowedPublicUri(Guid id, Uri uri)
        {
            Operations.Add("url");
            var allowed = store.IsAllowedPublicUri(id, uri);
            duringUrl();
            return allowed;
        }
    }
}
