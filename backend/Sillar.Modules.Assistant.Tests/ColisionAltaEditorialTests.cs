using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Sillar.Modules.Assistant.Data;
using Sillar.Modules.Assistant.Data.Entities;
using Sillar.Modules.Assistant.Data.Repositories;

namespace Sillar.Modules.Assistant.Tests;

/// <summary>Clasificación focal del error SQL. Sin conexión ni carrera real PostgreSQL.</summary>
public sealed class ColisionAltaEditorialTests
{
    private static DbUpdateException ErrorPostgres(string sqlState = PostgresErrorCodes.UniqueViolation,
        string? schema = AssistantDbContext.Schema, string? table = "knowledge_articles",
        string? constraint = EditorialDatabaseConflict.ArticlePrimaryKeyConstraintName)
        => new("inserción", new PostgresException("restricción", "ERROR", "ERROR", sqlState,
            schemaName: schema, tableName: table, constraintName: constraint));

    [Fact]
    public void LaClavePrimariaDelArticuloEsConflicto()
    {
        Assert.True(EditorialDatabaseConflict.IsArticlePrimaryKeyCollision(ErrorPostgres()));
    }

    [Fact]
    public void LaClaveDeOtraRestriccionNoSeOculta()
    {
        Assert.False(EditorialDatabaseConflict.IsArticlePrimaryKeyCollision(
            ErrorPostgres(constraint: "ak_assistant_knowledge_articles_installation_id_id")));
    }

    [Fact]
    public void OtraTablaNoEsColisionDeArticulo()
    {
        Assert.False(EditorialDatabaseConflict.IsArticlePrimaryKeyCollision(
            ErrorPostgres(table: "knowledge_revisions")));
    }

    [Fact]
    public void OtroEsquemaNoEsColisionDeArticulo()
    {
        Assert.False(EditorialDatabaseConflict.IsArticlePrimaryKeyCollision(
            ErrorPostgres(schema: "sales")));
    }

    [Fact]
    public void UnaViolacionCheckNoEsConflictoDeClave()
    {
        Assert.False(EditorialDatabaseConflict.IsArticlePrimaryKeyCollision(
            ErrorPostgres(sqlState: PostgresErrorCodes.CheckViolation)));
    }

    [Fact]
    public void SinNombreDeRestriccionNoSeAdivinaLaCausa()
    {
        Assert.False(EditorialDatabaseConflict.IsArticlePrimaryKeyCollision(
            ErrorPostgres(constraint: null)));
    }

    [Fact]
    public void SinEsquemaNoSeAdivinaLaCausa()
    {
        Assert.False(EditorialDatabaseConflict.IsArticlePrimaryKeyCollision(
            ErrorPostgres(schema: null)));
    }

    [Fact]
    public void ErrorNoPostgresSePropagaSinReclasificacion()
    {
        var other = new DbUpdateException("desconocido", new InvalidOperationException("conexión"));
        Assert.False(EditorialDatabaseConflict.IsArticlePrimaryKeyCollision(other));
    }

    [Fact]
    public void NombreFisicoDeClaveCoincideConElClasificador()
    {
        using var db = new AssistantDbContext(new DbContextOptionsBuilder<AssistantDbContext>()
            .UseNpgsql("Host=localhost;Database=sin_conectar;Username=unused;Password=unused")
            .Options);
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(EditorialArticleRow))!;
        Assert.Equal(EditorialDatabaseConflict.ArticlePrimaryKeyConstraintName,
            entity.FindPrimaryKey()!.GetName());
    }
}
