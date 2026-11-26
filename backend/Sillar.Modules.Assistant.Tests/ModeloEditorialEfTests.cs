using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Sillar.Modules.Assistant.Data;
using Sillar.Modules.Assistant.Data.Entities;

namespace Sillar.Modules.Assistant.Tests;

/// <summary>Inspección de metadatos EF; NO abre PostgreSQL ni crea tablas.</summary>
public class ModeloEditorialEfTests
{
    private static AssistantDbContext Contexto()
        => new(new DbContextOptionsBuilder<AssistantDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void SchemaPropioDelAsistente()
    {
        using var db = Contexto();
        Assert.Equal("assistant", db.Model.GetDefaultSchema());
    }

    [Fact]
    public void ArticulosEnTablaPropia()
    {
        using var db = Contexto();
        var entity = db.Model.FindEntityType(typeof(EditorialArticleRow));
        Assert.NotNull(entity);
        Assert.Equal("knowledge_articles", entity.GetTableName());
        Assert.Equal("assistant", entity.GetSchema());
    }

    [Fact]
    public void RevisionesEnTablaPropia()
    {
        using var db = Contexto();
        var entity = db.Model.FindEntityType(typeof(EditorialRevisionRow));
        Assert.NotNull(entity);
        Assert.Equal("knowledge_revisions", entity.GetTableName());
    }

    [Fact]
    public void VersionEditorialEsTokenDeConcurrencia()
    {
        using var db = Contexto();
        var entity = db.Model.FindEntityType(typeof(EditorialArticleRow))!;
        Assert.True(entity.FindProperty(nameof(EditorialArticleRow.Version))!.IsConcurrencyToken);
    }

    [Fact]
    public void EscrituraExigeNodoNoNulo()
    {
        using var db = Contexto();
        var entity = db.Model.FindEntityType(typeof(EditorialArticleRow))!;
        Assert.False(entity.FindProperty(nameof(EditorialArticleRow.WriterNode))!.IsNullable);
        Assert.Equal(128, entity.FindProperty(nameof(EditorialArticleRow.WriterNode))!.GetMaxLength());
    }

    [Fact]
    public void PreguntaEstaAcotadaAQuinientos()
    {
        using var db = Contexto();
        var entity = db.Model.FindEntityType(typeof(EditorialRevisionRow))!;
        Assert.Equal(500, entity.FindProperty(nameof(EditorialRevisionRow.Question))!.GetMaxLength());
    }

    [Fact]
    public void RespuestaEstaAcotadaADiezMil()
    {
        using var db = Contexto();
        var entity = db.Model.FindEntityType(typeof(EditorialRevisionRow))!;
        Assert.Equal(10000, entity.FindProperty(nameof(EditorialRevisionRow.Answer))!.GetMaxLength());
    }

    [Fact]
    public void TituloYEnlaceAcotados()
    {
        using var db = Contexto();
        var entity = db.Model.FindEntityType(typeof(EditorialRevisionRow))!;
        Assert.Equal(200, entity.FindProperty(nameof(EditorialRevisionRow.Title))!.GetMaxLength());
        Assert.Equal(2048, entity.FindProperty(nameof(EditorialRevisionRow.PublicUrl))!.GetMaxLength());
    }

    [Fact]
    public void ClaveDeRevisionPerteneceAInstalacionYArticulo()
    {
        using var db = Contexto();
        var revision = db.Model.FindEntityType(typeof(EditorialRevisionRow))!;
        var fk = Assert.Single(revision.GetForeignKeys());
        Assert.Equal(new[] { "InstallationId", "ArticleId" },
            fk.Properties.Select(x => x.Name).ToArray());
        Assert.Equal(typeof(EditorialArticleRow), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Fact]
    public void ArticuloTieneClaveAlternativaPorInstalacion()
    {
        using var db = Contexto();
        var article = db.Model.FindEntityType(typeof(EditorialArticleRow))!;
        Assert.Contains(article.GetKeys(), key => key.Properties.Select(x => x.Name)
            .SequenceEqual(new[] { "InstallationId", "Id" }));
    }

    [Fact]
    public void RevisionRequiereTodosLosCamposEditoriales()
    {
        using var db = Contexto();
        var revision = db.Model.FindEntityType(typeof(EditorialRevisionRow))!;
        foreach (var name in new[] { "Question", "Answer", "Title", "PublicUrl" })
            Assert.False(revision.FindProperty(name)!.IsNullable);
    }

    [Fact]
    public void IndiceFaqAntecedePreguntaConInstalacion()
    {
        using var db = Contexto();
        var revision = db.Model.FindEntityType(typeof(EditorialRevisionRow))!;
        Assert.Contains(revision.GetIndexes(), index =>
            index.Properties.Select(x => x.Name).SequenceEqual(
                new[] { nameof(EditorialRevisionRow.InstallationId), nameof(EditorialRevisionRow.QuestionKey) }));
    }

    [Fact]
    public void NoExisteTablaDeOtroModuloEnElModelo()
    {
        using var db = Contexto();
        Assert.Equal(2, db.Model.GetEntityTypes().Count());
        Assert.All(db.Model.GetEntityTypes(), entity =>
            Assert.Equal("assistant", entity.GetSchema()));
    }
}
