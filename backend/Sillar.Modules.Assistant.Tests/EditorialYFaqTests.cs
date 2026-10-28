using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public class EditorialYFaqTests
{
    private static readonly Guid Installation = Guid.NewGuid();
    private static readonly Guid OtherInstallation = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static EditorialPrincipal Actor(string role = EditorialRoles.Admin, Guid? installation = null,
        string node = "principal", bool authority = true) =>
        new(installation ?? Installation, role, node, authority);

    private static KnowledgeDraft Draft(string question = "¿Dónde están?", string answer = "En Arequipa.") =>
        new(Guid.NewGuid(), question, answer, "Ubicación", new Uri("https://ejemplo.com/ubicacion"));

    private static KnowledgeArticle Article() => new(Guid.NewGuid(), Installation, "principal");

    [Fact]
    public void EditorPuedePrepararPeroNoPublicar()
    {
        var a = Article(); var d = Draft();
        Assert.Equal(EditorialResult.Saved, a.SaveDraft(Actor(EditorialRoles.Editor), d, 0));
        Assert.Equal(EditorialResult.Forbidden, a.Publish(Actor(EditorialRoles.Editor), d.RevisionId, 1, Now));
        Assert.Null(a.Published);
    }

    [Fact]
    public void AdministradorPublicaSoloRevisionEsperada()
    {
        var a = Article(); var d = Draft();
        Assert.Equal(EditorialResult.Saved, a.SaveDraft(Actor(), d, 0));
        Assert.Equal(EditorialResult.Conflict, a.Publish(Actor(), d.RevisionId, 0, Now));
        Assert.Equal(EditorialResult.Published, a.Publish(Actor(), d.RevisionId, 1, Now));
        Assert.Equal(d.RevisionId, a.Published!.RevisionId);
    }

    [Fact]
    public void CrearNuevoBorradorNoDespublicaRevisionAnterior()
    {
        var a = Article(); var d1 = Draft();
        a.SaveDraft(Actor(), d1, 0); a.Publish(Actor(), d1.RevisionId, 1, Now);
        var d2 = Draft("¿Horario?", "De lunes a viernes.");
        a.SaveDraft(Actor(), d2, 2);
        Assert.Equal(d1.RevisionId, a.Published!.RevisionId);
    }

    [Fact]
    public void RetirarInvalidaPublicacion()
    {
        var a = Article(); var d = Draft();
        a.SaveDraft(Actor(), d, 0); a.Publish(Actor(), d.RevisionId, 1, Now);
        Assert.Equal(EditorialResult.Withdrawn, a.Withdraw(Actor(), d.RevisionId, 2));
        Assert.Null(a.CurrentPublication());
    }

    [Fact]
    public void NoSePuedePublicarDesdeOtraInstalacion()
    {
        var a = Article(); var d = Draft();
        a.SaveDraft(Actor(), d, 0);
        Assert.Equal(EditorialResult.Forbidden, a.Publish(Actor(installation: OtherInstallation), d.RevisionId, 1, Now));
    }

    [Fact]
    public void SuperAdminSinAutoridadDeArticuloTampocoPublica()
    {
        var a = Article(); var d = Draft();
        a.SaveDraft(Actor(), d, 0);
        Assert.Equal(EditorialResult.Forbidden,
            a.Publish(Actor(EditorialRoles.SuperAdmin, authority: false), d.RevisionId, 1, Now));
    }

    [Fact]
    public void OtroNodoNoPuedeEditar()
    {
        var a = Article();
        Assert.Equal(EditorialResult.Forbidden, a.SaveDraft(Actor(node: "secundario"), Draft(), 0));
    }

    [Fact]
    public void UrlNoHttpsSeRechaza()
    {
        var a = Article(); var d = Draft() with { PublicUri = new Uri("http://ejemplo.com") };
        Assert.Equal(EditorialResult.Invalid, a.SaveDraft(Actor(), d, 0));
    }

    [Fact]
    public void FaqVacíaSeAbstiene()
    {
        Assert.Equal(FaqOutcome.Insufficient, new FaqService(new Fake()).Answer(Installation, "¿Dónde están?", Now).Outcome);
    }

    [Fact]
    public void FaqPublicadaAutoritativaRespondeConReferencia()
    {
        var a = Article(); var d = Draft();
        a.SaveDraft(Actor(), d, 0); a.Publish(Actor(), d.RevisionId, 1, Now);
        var fake = new Fake(a.Published!);
        var answer = new FaqService(fake).Answer(Installation, "  ¿DÓNDE   ESTÁN? ", Now);
        Assert.Equal(FaqOutcome.Answered, answer.Outcome);
        Assert.Equal("En Arequipa.", answer.Answer);
        Assert.Equal(d.RevisionId, answer.RevisionId);
    }

    [Fact]
    public void FaqDeOtraInstalacionNoResponde()
    {
        var a = Article(); var d = Draft();
        a.SaveDraft(Actor(), d, 0); a.Publish(Actor(), d.RevisionId, 1, Now);
        Assert.NotEqual(FaqOutcome.Answered, new FaqService(new Fake(a.Published!))
            .Answer(OtherInstallation, d.Question, Now).Outcome);
    }

    [Fact]
    public void FaqRechazaEnlacePublicoFueraDelDominioAutorizado()
    {
        var a = Article(); var d = Draft();
        a.SaveDraft(Actor(), d, 0); a.Publish(Actor(), d.RevisionId, 1, Now);
        var fake = new Fake(a.Published!) { PermitLinks = false };
        Assert.Equal(FaqOutcome.Unavailable,
            new FaqService(fake).Answer(Installation, d.Question, Now).Outcome);
    }

    [Fact]
    public void FaqConFuenteRetiradaSeAbstiene()
    {
        var a = Article(); var d = Draft();
        a.SaveDraft(Actor(), d, 0); a.Publish(Actor(), d.RevisionId, 1, Now);
        var fake = new Fake(a.Published!) { Withdrawn = true };
        Assert.Equal(FaqOutcome.Insufficient, new FaqService(fake).Answer(Installation, d.Question, Now).Outcome);
    }

    [Fact]
    public void FaqConFrescuraIndeterminadaSeAbstiene()
    {
        var a = Article(); var d = Draft();
        a.SaveDraft(Actor(), d, 0); a.Publish(Actor(), d.RevisionId, 1, Now);
        var fake = new Fake(a.Published!) { Indeterminate = true };
        Assert.Equal(FaqOutcome.Unavailable, new FaqService(fake).Answer(Installation, d.Question, Now).Outcome);
    }

    [Fact]
    public void FaqDetectaRetiroAntesDeEmitirRespuesta()
    {
        var a = Article(); var d = Draft();
        a.SaveDraft(Actor(), d, 0); a.Publish(Actor(), d.RevisionId, 1, Now);
        var fake = new Fake(a.Published!) { WithdrawAfterFirstRead = true };
        Assert.Equal(FaqOutcome.Unavailable, new FaqService(fake).Answer(Installation, d.Question, Now).Outcome);
    }

    [Fact]
    public void FaqNoEligeArbitrariamenteEntreDosFuentesExactas()
    {
        var a = Article(); var d = Draft();
        a.SaveDraft(Actor(), d, 0); a.Publish(Actor(), d.RevisionId, 1, Now);
        var b = Article(); var d2 = Draft(answer: "En otra dirección.");
        b.SaveDraft(Actor(), d2, 0); b.Publish(Actor(), d2.RevisionId, 1, Now);
        Assert.Equal(FaqOutcome.Insufficient,
            new FaqService(new Fake(a.Published!, b.Published!)).Answer(Installation, d.Question, Now).Outcome);
    }

    private sealed class Fake(params PublishedKnowledge[] publications) : ILocalKnowledgeAuthority
    {
        public bool PermitLinks { get; set; } = true;
        public bool Withdrawn { get; set; }
        public bool Indeterminate { get; set; }
        public bool WithdrawAfterFirstRead { get; set; }
        private int _reads;

        public IReadOnlyList<Guid> FindCandidateArticleIds(Guid installationId, string normalizedQuestion, int maxCandidates) =>
            publications.Where(x => x.InstallationId == installationId &&
                FaqService.Normalize(x.Question) == normalizedQuestion)
                .Select(x => x.ArticleId).Take(maxCandidates).ToArray();

        public bool IsAllowedPublicUri(Guid installationId, Uri uri) => PermitLinks &&
            installationId == Installation && uri.Host == "ejemplo.com";

        public KnowledgeRead ReadCurrent(Guid installationId, Guid articleId)
        {
            var item = publications.FirstOrDefault(x => x.ArticleId == articleId && x.InstallationId == installationId);
            _reads++;
            return new KnowledgeRead(
                Indeterminate ? KnowledgeReadStatus.Indeterminate :
                item is null ? KnowledgeReadStatus.NotFound : KnowledgeReadStatus.Authoritative,
                item, true, true, Withdrawn || (WithdrawAfterFirstRead && _reads >= 2), null);
        }
    }
}
