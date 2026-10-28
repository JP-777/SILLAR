using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Sillar.Modules.Assistant.Data;
using Sillar.Modules.Assistant.Data.Entities;
using Sillar.Modules.Assistant.Data.Repositories;
using Sillar.Modules.Assistant.Domain;

namespace Sillar.Modules.Assistant.Tests;

/// <summary>Pruebas de metadatos y compuertas previas a la base. NO son integración PostgreSQL.</summary>
public sealed class RepositorioEditorialEfSinBaseTests
{
    private static AssistantDbContext Contexto() => new(
        new DbContextOptionsBuilder<AssistantDbContext>()
            .UseNpgsql("Host=localhost;Database=no_connectar;Username=unused;Password=unused")
            .Options);

    private static EditorialPersistencePolicy Policy() => new(
        new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")] = new[] { "sillar.example" }
        });

    [Fact]
    public void VersionDePublicacionSeConservaEnRevision()
    {
        using var db = Contexto();
        var entity = db.Model.FindEntityType(typeof(EditorialRevisionRow))!;
        Assert.Equal("published_article_version",
            entity.FindProperty(nameof(EditorialRevisionRow.PublishedArticleVersion))!.GetColumnName());
    }

    [Fact]
    public void FechaYVersionDePublicacionPuedenSerNulasAntesDePublicar()
    {
        using var db = Contexto();
        var entity = db.Model.FindEntityType(typeof(EditorialRevisionRow))!;
        Assert.True(entity.FindProperty(nameof(EditorialRevisionRow.PublishedAt))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(EditorialRevisionRow.PublishedArticleVersion))!.IsNullable);
    }

    [Fact]
    public void ClaveCompuestaDeRevisionMantieneAislamiento()
    {
        using var db = Contexto();
        var entity = db.Model.FindEntityType(typeof(EditorialRevisionRow))!;
        var foreignKey = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(new[] { "InstallationId", "ArticleId" },
            foreignKey.Properties.Select(x => x.Name).ToArray());
    }

    [Fact]
    public async Task NiegaAltaSinIdentidadConfiableSinAbrirConexion()
    {
        await using var db = Contexto();
        var store = new EfEditorialArticleStore(db, Policy(), TimeProvider.System);
        Assert.Equal(EditorialResult.Forbidden, await store.CreateAsync(null, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NiegaBorradorSinAutoridadSinAbrirConexion()
    {
        await using var db = Contexto();
        var store = new EfEditorialArticleStore(db, Policy(), TimeProvider.System);
        Assert.Equal(EditorialResult.Forbidden,
            await store.SaveDraftAsync(null, Guid.NewGuid(), null, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NiegaPublicacionSinAutoridadSinAbrirConexion()
    {
        await using var db = Contexto();
        var store = new EfEditorialArticleStore(db, Policy(), TimeProvider.System);
        Assert.Equal(EditorialResult.Forbidden,
            await store.PublishAsync(null, Guid.NewGuid(), Guid.NewGuid(), 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NiegaRetiradaSinAutoridadSinAbrirConexion()
    {
        await using var db = Contexto();
        var store = new EfEditorialArticleStore(db, Policy(), TimeProvider.System);
        Assert.Equal(EditorialResult.Forbidden,
            await store.WithdrawAsync(null, Guid.NewGuid(), Guid.NewGuid(), 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SinInstalacionAutorizadaLaLecturaNoAccedeALaBase()
    {
        await using var db = Contexto();
        var store = new EfEditorialArticleStore(db, Policy(), TimeProvider.System);
        var result = await store.ReadCurrentAsync(Guid.Empty, Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.Equal(KnowledgeReadStatus.NotFound, result.Status);
    }
}
