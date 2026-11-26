using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public class RepositorioEditorialLocalTests
{
    private readonly Guid _instalacion = Guid.NewGuid();
    private readonly Guid _otra = Guid.NewGuid();
    private static readonly DateTimeOffset Hoy = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

    private LocalEditorialKnowledgeStore Store() => new(
        new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [_instalacion] = ["ejemplo.com"],
            [_otra] = ["otra.com"]
        });

    private EditorialPrincipal Actor(string role = EditorialRoles.Admin, Guid? instalacion = null,
        string nodo = "principal", bool autoridad = true) =>
        new(instalacion ?? _instalacion, role, nodo, autoridad);

    private static KnowledgeDraft Borrador(string respuesta = "Abrimos a las nueve.", string url = "https://ejemplo.com/horarios") =>
        new(Guid.NewGuid(), "¿Cuál es el horario?", respuesta, "Horario", new Uri(url));

    [Fact]
    public void ConsultaRealDespuesDePublicar()
    {
        var store = Store(); var id = Guid.NewGuid(); var b = Borrador();
        Assert.Equal(EditorialResult.Saved, store.Create(Actor(), id));
        Assert.Equal(EditorialResult.Saved, store.SaveDraft(Actor(), id, b, 0));
        Assert.Equal(EditorialResult.Published, store.Publish(Actor(), id, b.RevisionId, 1, Hoy));
        var answer = new FaqService(store).Answer(_instalacion, b.Question, Hoy);
        Assert.Equal(FaqOutcome.Answered, answer.Outcome);
        Assert.Equal("Abrimos a las nueve.", answer.Answer);
        Assert.Equal(b.RevisionId, answer.RevisionId);
    }

    [Fact]
    public void BorradorNoSeExponeAntesDePublicar()
    {
        var store = Store(); var id = Guid.NewGuid(); var b = Borrador();
        store.Create(Actor(), id);
        store.SaveDraft(Actor(), id, b, 0);
        Assert.Equal(FaqOutcome.Insufficient, new FaqService(store).Answer(_instalacion, b.Question, Hoy).Outcome);
    }

    [Fact]
    public void RetiradaInvalidaInmediatamenteFuenteLocal()
    {
        var store = Store(); var id = Guid.NewGuid(); var b = Borrador();
        store.Create(Actor(), id); store.SaveDraft(Actor(), id, b, 0);
        store.Publish(Actor(), id, b.RevisionId, 1, Hoy);
        Assert.Equal(EditorialResult.Withdrawn, store.Withdraw(Actor(), id, b.RevisionId, 2));
        Assert.Equal(FaqOutcome.Insufficient, new FaqService(store).Answer(_instalacion, b.Question, Hoy).Outcome);
    }

    [Fact]
    public void RevisionPublicaNoCambiaAlGuardarOtroBorrador()
    {
        var store = Store(); var id = Guid.NewGuid(); var b = Borrador();
        store.Create(Actor(), id); store.SaveDraft(Actor(), id, b, 0);
        store.Publish(Actor(), id, b.RevisionId, 1, Hoy);
        Assert.Equal(EditorialResult.Saved, store.SaveDraft(Actor(), id, Borrador("Nuevos horarios"), 2));
        Assert.Equal(b.RevisionId, new FaqService(store).Answer(_instalacion, b.Question, Hoy).RevisionId);
    }

    [Fact]
    public void BloqueaEscrituraDeOtraInstalacion()
    {
        var store = Store(); var id = Guid.NewGuid(); var b = Borrador();
        store.Create(Actor(), id);
        Assert.Equal(EditorialResult.Forbidden, store.SaveDraft(Actor(instalacion: _otra), id, b, 0));
    }

    [Fact]
    public void EditorNoPublica()
    {
        var store = Store(); var id = Guid.NewGuid(); var b = Borrador();
        store.Create(Actor(), id); store.SaveDraft(Actor(), id, b, 0);
        Assert.Equal(EditorialResult.Forbidden, store.Publish(Actor(EditorialRoles.Editor), id, b.RevisionId, 1, Hoy));
    }

    [Fact]
    public void SuperAdminSinAutoridadNoCrea()
    {
        var store = Store();
        Assert.Equal(EditorialResult.Forbidden,
            store.Create(Actor(EditorialRoles.SuperAdmin, autoridad: false), Guid.NewGuid()));
    }

    [Fact]
    public void UrlConSubdominioImpostorNoPasa()
    {
        var store = Store(); var id = Guid.NewGuid();
        store.Create(Actor(), id);
        Assert.Equal(EditorialResult.Invalid, store.SaveDraft(Actor(), id,
            Borrador(url: "https://ejemplo.com.malicioso.net/horarios"), 0));
    }

    [Fact]
    public void NoPermiteConflictoDeVersion()
    {
        var store = Store(); var id = Guid.NewGuid(); var b = Borrador();
        store.Create(Actor(), id);
        Assert.Equal(EditorialResult.Saved, store.SaveDraft(Actor(), id, b, 0));
        Assert.Equal(EditorialResult.Conflict, store.SaveDraft(Actor(), id, Borrador(), 0));
    }

    [Fact]
    public async Task SoloUnaPublicacionConcurrenteAvanzaLaVersion()
    {
        var store = Store(); var id = Guid.NewGuid(); var b = Borrador();
        store.Create(Actor(), id); store.SaveDraft(Actor(), id, b, 0);
        var tasks = Enumerable.Range(0, 24).Select(_ => Task.Run(() =>
            store.Publish(Actor(), id, b.RevisionId, 1, Hoy))).ToArray();
        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, results.Count(x => x == EditorialResult.Published));
        Assert.Equal(23, results.Count(x => x == EditorialResult.Conflict));
        Assert.Equal<long?>(2L, store.CurrentVersion(_instalacion, id));
    }

    [Fact]
    public void DosArticulosVigentesConMismaPreguntaSeAbstienen()
    {
        var store = Store();
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var id = Guid.NewGuid(); var b = Borrador();
            store.Create(Actor(), id); store.SaveDraft(Actor(), id, b, 0);
            store.Publish(Actor(), id, b.RevisionId, 1, Hoy);
        }
        Assert.Equal(FaqOutcome.Insufficient,
            new FaqService(store).Answer(_instalacion, "¿Cuál es el horario?", Hoy).Outcome);
    }

    [Fact]
    public void AislamientoDeLecturaEntreInstalaciones()
    {
        var store = Store(); var id = Guid.NewGuid(); var b = Borrador();
        store.Create(Actor(), id); store.SaveDraft(Actor(), id, b, 0);
        store.Publish(Actor(), id, b.RevisionId, 1, Hoy);
        Assert.Equal(KnowledgeReadStatus.NotFound, store.ReadCurrent(_otra, id).Status);
        Assert.Equal(FaqOutcome.Insufficient,
            new FaqService(store).Answer(_otra, b.Question, Hoy).Outcome);
    }
}
