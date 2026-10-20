using Microsoft.AspNetCore.Http.HttpResults;
using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

/// <summary>Frontera administrativa sin host, red ni base de datos.</summary>
public sealed class AdministracionEditorialHttpTests
{
    private static readonly Guid Instalacion = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Articulo = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Revision = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static EditorialPrincipal Actor(string role = EditorialRoles.Admin) =>
        new(Instalacion, role, "escritor-1", true);

    private static AssistantDraftRequest Borrador(long version = 0) =>
        new("¿Hay horarios?", "De lunes a viernes.", "Horarios", "https://sillar.example/horarios", version);

    [Fact]
    public async Task SinIdentidadNoCreaNiConsultaServicios()
    {
        var service = new ServicioFalso();
        var response = await AssistantEditorialEndpoints.Create(new AmbitoFalso(null), service,
            TestContext.Current.CancellationToken);
        Assert.IsType<NotFound>(response);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task ExcepcionEnAutoridadNiegaSinAccederADatos()
    {
        var service = new ServicioFalso();
        var response = await AssistantEditorialEndpoints.Create(new AmbitoRoto(), service,
            TestContext.Current.CancellationToken);
        Assert.IsType<NotFound>(response);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task EditorPuedeCrearConIdentificadorGeneradoEnServidor()
    {
        var service = new ServicioFalso();
        var response = await AssistantEditorialEndpoints.Create(new AmbitoFalso(Actor(EditorialRoles.Editor)),
            service, TestContext.Current.CancellationToken);
        var created = Assert.IsType<Created<AssistantEditorialWriteReply>>(response);
        Assert.Equal("saved", created.Value!.Outcome);
        Assert.NotEqual(Guid.Empty, created.Value.ArticleId);
        Assert.Equal(created.Value.ArticleId, service.LastArticle);
        Assert.Equal(Instalacion, service.LastActor!.InstallationId);
    }

    [Fact]
    public async Task UsuarioSinAutoridadDeEscrituraNoPuedeCrear()
    {
        var service = new ServicioFalso();
        var actor = Actor() with { HasArticleWriteAuthority = false };
        var response = await AssistantEditorialEndpoints.Create(new AmbitoFalso(actor), service,
            TestContext.Current.CancellationToken);
        Assert.IsType<NotFound>(response);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task BorradorVacioNoEjecutaRepositorio()
    {
        var service = new ServicioFalso();
        var response = await AssistantEditorialEndpoints.SaveDraft(Articulo,
            Borrador() with { Question = " " }, new AmbitoFalso(Actor()), service,
            TestContext.Current.CancellationToken);
        Assert.IsType<ValidationProblem>(response);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task UrlNoHttpsNoSeAcepta()
    {
        var service = new ServicioFalso();
        var response = await AssistantEditorialEndpoints.SaveDraft(Articulo,
            Borrador() with { PublicUrl = "http://sillar.example/horarios" },
            new AmbitoFalso(Actor()), service, TestContext.Current.CancellationToken);
        Assert.IsType<ValidationProblem>(response);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task UrlConCredencialesNoSeAcepta()
    {
        var service = new ServicioFalso();
        var response = await AssistantEditorialEndpoints.SaveDraft(Articulo,
            Borrador() with { PublicUrl = "https://u:p@sillar.example/horarios" },
            new AmbitoFalso(Actor()), service, TestContext.Current.CancellationToken);
        Assert.IsType<ValidationProblem>(response);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task VersionNegativaNoCreaRevision()
    {
        var service = new ServicioFalso();
        var response = await AssistantEditorialEndpoints.SaveDraft(Articulo, Borrador(-1),
            new AmbitoFalso(Actor()), service, TestContext.Current.CancellationToken);
        Assert.IsType<ValidationProblem>(response);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task RevisionSeGeneraEnServidorYPreservaVersionEsperada()
    {
        var service = new ServicioFalso();
        var response = await AssistantEditorialEndpoints.SaveDraft(Articulo, Borrador(4),
            new AmbitoFalso(Actor(EditorialRoles.Editor)), service,
            TestContext.Current.CancellationToken);
        var ok = Assert.IsType<Ok<AssistantEditorialWriteReply>>(response);
        Assert.Equal(5, ok.Value!.Version);
        Assert.NotEqual(Guid.Empty, ok.Value.RevisionId);
        Assert.Equal(ok.Value.RevisionId, service.LastDraft!.RevisionId);
        Assert.Equal(4, service.LastExpectedVersion);
        Assert.Equal(Instalacion, service.LastActor!.InstallationId);
    }

    [Fact]
    public async Task EditorNoPuedePublicarAunqueServicioLoPermita()
    {
        var service = new ServicioFalso { WriteOutcome = EditorialResult.Published };
        var response = await AssistantEditorialEndpoints.Publish(Articulo,
            new AssistantPublicationRequest(Revision, 3),
            new AmbitoFalso(Actor(EditorialRoles.Editor)), service,
            TestContext.Current.CancellationToken);
        Assert.IsType<NotFound>(response);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task AdminPuedePublicarRevisionEsperada()
    {
        var service = new ServicioFalso { WriteOutcome = EditorialResult.Published };
        var response = await AssistantEditorialEndpoints.Publish(Articulo,
            new AssistantPublicationRequest(Revision, 3), new AmbitoFalso(Actor()), service,
            TestContext.Current.CancellationToken);
        var ok = Assert.IsType<Ok<AssistantEditorialWriteReply>>(response);
        Assert.Equal("published", ok.Value!.Outcome);
        Assert.Equal(4, ok.Value.Version);
        Assert.Equal(Revision, service.LastRevision);
    }

    [Fact]
    public async Task SuperAdminPuedeRetirar()
    {
        var service = new ServicioFalso { WriteOutcome = EditorialResult.Withdrawn };
        var response = await AssistantEditorialEndpoints.Withdraw(Articulo,
            new AssistantPublicationRequest(Revision, 5),
            new AmbitoFalso(Actor(EditorialRoles.SuperAdmin)), service,
            TestContext.Current.CancellationToken);
        var ok = Assert.IsType<Ok<AssistantEditorialWriteReply>>(response);
        Assert.Equal("withdrawn", ok.Value!.Outcome);
        Assert.Equal(6, ok.Value.Version);
    }

    [Fact]
    public async Task RevisionVaciaNoPublica()
    {
        var service = new ServicioFalso();
        var response = await AssistantEditorialEndpoints.Publish(Articulo,
            new AssistantPublicationRequest(Guid.Empty, 0), new AmbitoFalso(Actor()), service,
            TestContext.Current.CancellationToken);
        Assert.IsType<ValidationProblem>(response);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task ConflictoDeVersionNoApareceComoExito()
    {
        var service = new ServicioFalso { WriteOutcome = EditorialResult.Conflict };
        var response = await AssistantEditorialEndpoints.SaveDraft(Articulo, Borrador(),
            new AmbitoFalso(Actor()), service, TestContext.Current.CancellationToken);
        var problem = Assert.IsType<ProblemHttpResult>(response);
        Assert.Equal(409, problem.StatusCode);
    }

    [Fact]
    public async Task ResultadoNoConfirmadoNuncaSeDeclaraPublicado()
    {
        var service = new ServicioFalso { WriteOutcome = EditorialResult.Unchanged };
        var response = await AssistantEditorialEndpoints.Publish(Articulo,
            new AssistantPublicationRequest(Revision, 2), new AmbitoFalso(Actor()), service,
            TestContext.Current.CancellationToken);
        var problem = Assert.IsType<ProblemHttpResult>(response);
        Assert.Equal(503, problem.StatusCode);
    }

    [Fact]
    public async Task InspeccionRetornaSoloDatosDeAplicacion()
    {
        var service = new ServicioFalso
        {
            Lookup = new(EditorialLookupStatus.Found,
                new EditorialArticleSnapshot(Articulo, Instalacion, "nodo-no-publicar", 6,
                    new KnowledgeDraft(Revision, "q", "a", "t", new Uri("https://sillar.example/faq")),
                    null))
        };
        var response = await AssistantEditorialEndpoints.Inspect(Articulo,
            new AmbitoFalso(Actor()), service, TestContext.Current.CancellationToken);
        var ok = Assert.IsType<Ok<AssistantEditorialArticleReply>>(response);
        Assert.Equal(6, ok.Value!.Version);
        Assert.Equal(Revision, ok.Value.Draft!.RevisionId);
        Assert.DoesNotContain(typeof(AssistantEditorialArticleReply).GetProperties(),
            p => p.Name is "InstallationId" or "WriterNode" or "Role");
    }

    [Fact]
    public async Task SinArticuloAutorizadoInspeccionResponde404()
    {
        var service = new ServicioFalso();
        var response = await AssistantEditorialEndpoints.Inspect(Articulo,
            new AmbitoFalso(Actor()), service, TestContext.Current.CancellationToken);
        Assert.IsType<NotFound>(response);
    }

    [Fact]
    public async Task UnRolDesconocidoNoEntraEnAdministracion()
    {
        var service = new ServicioFalso();
        var response = await AssistantEditorialEndpoints.SaveDraft(Articulo, Borrador(),
            new AmbitoFalso(Actor("invitado")), service, TestContext.Current.CancellationToken);
        Assert.IsType<NotFound>(response);
        Assert.Equal(0, service.Calls);
    }

    private sealed class AmbitoFalso(EditorialPrincipal? principal) : ITrustedEditorialAdminScope
    {
        public bool TryGetPrincipal(out EditorialPrincipal? actor)
        {
            actor = principal;
            return principal is not null;
        }
    }

    private sealed class AmbitoRoto : ITrustedEditorialAdminScope
    {
        public bool TryGetPrincipal(out EditorialPrincipal? actor)
        {
            actor = null;
            throw new InvalidOperationException("Fallo de autoridad CORE");
        }
    }

    private sealed class ServicioFalso : IAssistantEditorialAdministration
    {
        public int Calls { get; private set; }
        public EditorialPrincipal? LastActor { get; private set; }
        public Guid LastArticle { get; private set; }
        public Guid LastRevision { get; private set; }
        public KnowledgeDraft? LastDraft { get; private set; }
        public long LastExpectedVersion { get; private set; }
        public EditorialResult WriteOutcome { get; set; } = EditorialResult.Saved;
        public EditorialArticleLookup Lookup { get; set; } = new(EditorialLookupStatus.NotFound, null);

        private void Record(EditorialPrincipal actor, Guid articleId)
        {
            Calls++;
            LastActor = actor;
            LastArticle = articleId;
        }

        public Task<EditorialResult> CreateAsync(EditorialPrincipal actor, Guid articleId, CancellationToken ct)
        {
            Record(actor, articleId);
            return Task.FromResult(WriteOutcome);
        }

        public Task<EditorialResult> SaveDraftAsync(EditorialPrincipal actor, Guid articleId,
            KnowledgeDraft draft, long expectedVersion, CancellationToken ct)
        {
            Record(actor, articleId);
            LastDraft = draft;
            LastExpectedVersion = expectedVersion;
            return Task.FromResult(WriteOutcome);
        }

        public Task<EditorialResult> PublishAsync(EditorialPrincipal actor, Guid articleId,
            Guid revisionId, long expectedVersion, CancellationToken ct)
        {
            Record(actor, articleId);
            LastRevision = revisionId;
            LastExpectedVersion = expectedVersion;
            return Task.FromResult(WriteOutcome);
        }

        public Task<EditorialResult> WithdrawAsync(EditorialPrincipal actor, Guid articleId,
            Guid revisionId, long expectedVersion, CancellationToken ct)
        {
            Record(actor, articleId);
            LastRevision = revisionId;
            LastExpectedVersion = expectedVersion;
            return Task.FromResult(WriteOutcome);
        }

        public Task<EditorialArticleLookup> InspectAsync(EditorialPrincipal actor,
            Guid articleId, CancellationToken ct)
        {
            Record(actor, articleId);
            return Task.FromResult(Lookup);
        }
    }
}
