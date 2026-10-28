using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public class AssistantFaqEndpointTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 19, 18, 0, 0, TimeSpan.Zero);
    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();

    private sealed class Ambito(Guid installationId, bool enabled = true, bool throws = false)
        : ITrustedAssistantInstallationScope
    {
        public bool TryGetEnabledInstallation(out Guid id)
        {
            if (throws) throw new InvalidOperationException("identidad externa inaccesible");
            id = installationId;
            return enabled;
        }
    }

    private sealed class Reloj : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private LocalEditorialKnowledgeStore Store() => new(
        new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [_a] = ["empresa.test"],
            [_b] = ["otro.test"]
        });

    private static void Publish(LocalEditorialKnowledgeStore store, Guid installation, string host,
        string answer = "Atendemos desde las nueve.")
    {
        var actor = new EditorialPrincipal(installation, EditorialRoles.Admin, "nodo-local", true);
        var article = Guid.NewGuid();
        var draft = new KnowledgeDraft(Guid.NewGuid(), "¿Cuál es el horario?", answer,
            "Horario público", new Uri($"https://{host}/horario"));
        Assert.Equal(EditorialResult.Saved, store.Create(actor, article));
        Assert.Equal(EditorialResult.Saved, store.SaveDraft(actor, article, draft, 0));
        Assert.Equal(EditorialResult.Published, store.Publish(actor, article, draft.RevisionId, 1, Ahora));
    }

    private static object Reply(LocalEditorialKnowledgeStore store, AssistantFaqRequest? question,
        ITrustedAssistantInstallationScope scope) =>
        AssistantFaqEndpoints.HandleFaq(question, scope, new FaqService(store), new Reloj());

    [Fact]
    public void ContestaConFuentePublicaVigente()
    {
        var store = Store(); Publish(store, _a, "empresa.test");
        var result = Assert.IsType<Ok<AssistantFaqReply>>(
            Reply(store, new("¿Cuál es el horario?"), new Ambito(_a)));
        var reply = result.Value!;
        Assert.Equal("answered", reply.Status);
        Assert.Equal("Atendemos desde las nueve.", reply.Answer);
        Assert.Equal("Horario público", reply.SourceTitle);
        Assert.Equal("https://empresa.test/horario", reply.SourceUrl);
    }

    [Fact]
    public void SinArticuloNoInventaRespuestasNiCitas()
    {
        var result = Assert.IsType<Ok<AssistantFaqReply>>(
            Reply(Store(), new("¿Cuál es el horario?"), new Ambito(_a)));
        var reply = result.Value!;
        Assert.Equal("insufficient", reply.Status);
        Assert.Null(reply.SourceTitle);
        Assert.Null(reply.SourceUrl);
    }

    [Fact]
    public void NoFiltraFuentesDeOtraInstalacion()
    {
        var store = Store(); Publish(store, _a, "empresa.test");
        var result = Assert.IsType<Ok<AssistantFaqReply>>(
            Reply(store, new("¿Cuál es el horario?"), new Ambito(_b)));
        Assert.Equal("insufficient", result.Value!.Status);
    }

    [Fact]
    public void InstalacionDesactivadaRecibeNotFound()
    {
        Assert.IsType<NotFound>(Reply(Store(), new("¿Cuál es el horario?"), new Ambito(_a, enabled: false)));
    }

    [Fact]
    public void InstalacionVaciaRecibeNotFound()
    {
        Assert.IsType<NotFound>(Reply(Store(), new("¿Cuál es el horario?"), new Ambito(Guid.Empty)));
    }

    [Fact]
    public void FalloDeAutoridadNoHabilitaConsulta()
    {
        Assert.IsType<NotFound>(Reply(Store(), new("¿Cuál es el horario?"), new Ambito(_a, throws: true)));
    }

    [Fact]
    public void RechazaCuerpoNulo()
    {
        Assert.IsType<ValidationProblem>(Reply(Store(), null, new Ambito(_a)));
    }

    [Fact]
    public void RechazaPreguntaVacia()
    {
        Assert.IsType<ValidationProblem>(Reply(Store(), new(""), new Ambito(_a)));
    }

    [Fact]
    public void RechazaSoloEspacios()
    {
        Assert.IsType<ValidationProblem>(Reply(Store(), new("   "), new Ambito(_a)));
    }

    [Fact]
    public void RechazaPreguntaDemasiadoLarga()
    {
        Assert.IsType<ValidationProblem>(Reply(Store(), new(new string('x', 501)), new Ambito(_a)));
    }

    [Fact]
    public void InstalacionInactivaNoRevelaValidacionDeCuerpo()
    {
        Assert.IsType<NotFound>(Reply(Store(), null, new Ambito(_a, enabled: false)));
    }

    [Fact]
    public void NoTomaLaInstalacionDelJsonDelVisitante()
    {
        var store = Store(); Publish(store, _a, "empresa.test");
        var body = JsonSerializer.Deserialize<AssistantFaqRequest>(
            "{\"Question\":\"¿Cuál es el horario?\",\"InstallationId\":\"" + _a + "\"}");
        var result = Assert.IsType<Ok<AssistantFaqReply>>(Reply(store, body, new Ambito(_b)));
        Assert.Equal("insufficient", result.Value!.Status);
    }

    [Fact]
    public void DosFuentesConLaMismaPreguntaObliganAAbstencion()
    {
        var store = Store();
        Publish(store, _a, "empresa.test", "Primera versión.");
        Publish(store, _a, "empresa.test", "Otra respuesta.");
        var result = Assert.IsType<Ok<AssistantFaqReply>>(
            Reply(store, new("¿Cuál es el horario?"), new Ambito(_a)));
        Assert.Equal("insufficient", result.Value!.Status);
    }

    [Fact]
    public void RespuestaPublicaNoContieneDatosEditorialesInternos()
    {
        var fields = typeof(AssistantFaqReply).GetProperties().Select(x => x.Name).ToArray();
        Assert.Equal(new[] { "Status", "Answer", "SourceTitle", "SourceUrl" }, fields);
        var requestFields = typeof(AssistantFaqRequest).GetProperties().Select(x => x.Name).ToArray();
        Assert.Equal(new[] { "Question" }, requestFields);
    }
}
