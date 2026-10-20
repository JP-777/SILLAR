using Sillar.Modules.Assistant.Data.Entities;
using Sillar.Modules.Assistant.Data.Repositories;
using Sillar.Modules.Assistant.Domain;

namespace Sillar.Modules.Assistant.Tests;

public sealed class PoliticaPersistenciaEditorialTests
{
    private readonly Guid _site = Guid.NewGuid();
    private readonly Guid _foreign = Guid.NewGuid();

    private EditorialPersistencePolicy Policy() => new(
        new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [_site] = new[] { "sillar.example" }
        });

    private EditorialPrincipal Actor(string role = EditorialRoles.Admin,
        bool writable = true, string node = "node-a") =>
        new(_site, role, node, writable);

    private EditorialArticleRow Article(long version = 0) => new()
    {
        Id = Guid.NewGuid(), InstallationId = _site, WriterNode = "node-a", Version = version
    };

    private KnowledgeDraft Draft(Uri? uri = null) => new(
        Guid.NewGuid(), "¿Horario?", "Abierto de lunes a viernes.", "Horarios",
        uri ?? new Uri("https://sillar.example/ayuda/horarios"));

    [Fact]
    public void RequiereDominioPorInstalacion()
    {
        Assert.Throws<ArgumentException>(() => new EditorialPersistencePolicy(
            new Dictionary<Guid, IReadOnlyCollection<string>> { [_site] = Array.Empty<string>() }));
    }

    [Fact]
    public void ProhibeDominiosConComodines()
    {
        Assert.Throws<ArgumentException>(() => new EditorialPersistencePolicy(
            new Dictionary<Guid, IReadOnlyCollection<string>> { [_site] = new[] { "*.example.com" } }));
    }

    [Fact]
    public void PermiteEditorConAutoridadExplicita()
    {
        Assert.True(Policy().CanCreate(Actor(EditorialRoles.Editor)));
    }

    [Fact]
    public void NiegaActorSinPermisoDeArticulo()
    {
        Assert.False(Policy().CanCreate(Actor(writable: false)));
    }

    [Fact]
    public void NiegaRolDesconocido()
    {
        Assert.False(Policy().CanCreate(Actor("visitor")));
    }

    [Fact]
    public void NiegaInstalacionAjena()
    {
        var actor = new EditorialPrincipal(_foreign, EditorialRoles.Admin, "node-a", true);
        Assert.False(Policy().CanEdit(actor, Article()));
    }

    [Fact]
    public void NiegaEscritorDiferente()
    {
        Assert.False(Policy().CanEdit(Actor(node: "node-b"), Article()));
    }

    [Fact]
    public void NiegaPublicacionAEditor()
    {
        Assert.False(Policy().CanPublish(Actor(EditorialRoles.Editor), Article()));
    }

    [Fact]
    public void AutorizaPublicacionAAdministrador()
    {
        Assert.True(Policy().CanPublish(Actor(), Article()));
    }

    [Fact]
    public void RechazaHttpAunqueElDominioCoincida()
    {
        Assert.False(Policy().IsAllowedPublicUri(_site, new Uri("http://sillar.example/faq")));
    }

    [Fact]
    public void RechazaAutoridadConCredenciales()
    {
        Assert.False(Policy().IsAllowedPublicUri(_site,
            new Uri("https://usuario:clave@sillar.example/faq")));
    }

    [Fact]
    public void RechazaPuertoNoAutorizado()
    {
        Assert.False(Policy().IsAllowedPublicUri(_site,
            new Uri("https://sillar.example:9443/faq")));
    }

    [Fact]
    public void AceptaUrlPublicaDeInstalacion()
    {
        Assert.True(Policy().IsValidDraft(_site, Draft()));
    }

    [Fact]
    public void RechazaRevisionVacia()
    {
        var draft = Draft() with { RevisionId = Guid.Empty };
        Assert.False(Policy().IsValidDraft(_site, draft));
    }

    [Fact]
    public void RechazaRespuestaMasLargaQueElModelo()
    {
        var draft = Draft() with { Answer = new string('x', 10001) };
        Assert.False(Policy().IsValidDraft(_site, draft));
    }

    [Fact]
    public void RechazaVersionDesactualizada()
    {
        Assert.False(EditorialPersistencePolicy.MatchesVersion(Article(4), 3));
    }

    [Fact]
    public void AdmiteVersionExacta()
    {
        Assert.True(EditorialPersistencePolicy.MatchesVersion(Article(4), 4));
    }

    [Fact]
    public void ImpideDesbordamientoVersion()
    {
        Assert.False(EditorialPersistencePolicy.MatchesVersion(Article(long.MaxValue), long.MaxValue));
    }
}
