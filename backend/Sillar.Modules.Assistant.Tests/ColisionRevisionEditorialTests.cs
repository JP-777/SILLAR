using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Sillar.Modules.Assistant.Data;
using Sillar.Modules.Assistant.Data.Entities;
using Sillar.Modules.Assistant.Data.Repositories;

namespace Sillar.Modules.Assistant.Tests;

/// <summary>Clasificación de la PK de revisiones; sin DB ni carrera física PostgreSQL.</summary>
public sealed class ColisionRevisionEditorialTests
{
    private static DbUpdateException ErrorPostgres(
        string sqlState = PostgresErrorCodes.UniqueViolation,
        string? schema = AssistantDbContext.Schema,
        string? table = "knowledge_revisions",
        string? constraint = EditorialDatabaseConflict.RevisionPrimaryKeyConstraintName)
        => new("inserción", new PostgresException("restricción", "ERROR", "ERROR", sqlState,
            schemaName: schema, tableName: table, constraintName: constraint));

    [Fact]
    public void ClavePrimariaDeRevisionEsConflicto()
        => Assert.True(EditorialDatabaseConflict.IsRevisionPrimaryKeyCollision(ErrorPostgres()));

    [Fact]
    public void RestriccionAjenaNoSeReclasifica()
        => Assert.False(EditorialDatabaseConflict.IsRevisionPrimaryKeyCollision(
            ErrorPostgres(constraint: "uq_otra_restriccion")));

    [Fact]
    public void TablaAjenaNoSeReclasifica()
        => Assert.False(EditorialDatabaseConflict.IsRevisionPrimaryKeyCollision(
            ErrorPostgres(table: "knowledge_articles")));

    [Fact]
    public void EsquemaAjenoNoSeReclasifica()
        => Assert.False(EditorialDatabaseConflict.IsRevisionPrimaryKeyCollision(
            ErrorPostgres(schema: "sales")));

    [Fact]
    public void ViolacionCheckNoSeReclasifica()
        => Assert.False(EditorialDatabaseConflict.IsRevisionPrimaryKeyCollision(
            ErrorPostgres(sqlState: PostgresErrorCodes.CheckViolation)));

    [Fact]
    public void RestriccionAusenteNoSeAdivina()
        => Assert.False(EditorialDatabaseConflict.IsRevisionPrimaryKeyCollision(
            ErrorPostgres(constraint: null)));

    [Fact]
    public void EsquemaAusenteNoSeAdivina()
        => Assert.False(EditorialDatabaseConflict.IsRevisionPrimaryKeyCollision(
            ErrorPostgres(schema: null)));

    [Fact]
    public void ExcepcionNoPostgresNoEsConflicto()
        => Assert.False(EditorialDatabaseConflict.IsRevisionPrimaryKeyCollision(
            new DbUpdateException("fallo de red", new InvalidOperationException("sin conexión"))));

    [Fact]
    public void ColisionDeRevisionNoSeConfundeConAltaDeArticulo()
        => Assert.False(EditorialDatabaseConflict.IsArticlePrimaryKeyCollision(ErrorPostgres()));

    [Fact]
    public void ColisionDeArticuloNoSeConfundeConRevision()
        => Assert.False(EditorialDatabaseConflict.IsRevisionPrimaryKeyCollision(
            ErrorPostgres(table: "knowledge_articles",
                constraint: EditorialDatabaseConflict.ArticlePrimaryKeyConstraintName)));

    [Fact]
    public void ElNombreFisicoDeLaPkDeRevisionesCoincideConElClasificador()
    {
        using var db = new AssistantDbContext(new DbContextOptionsBuilder<AssistantDbContext>()
            .UseNpgsql("Host=localhost;Database=sin_conectar;Username=unused;Password=unused")
            .Options);
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(EditorialRevisionRow))!;
        Assert.Equal(EditorialDatabaseConflict.RevisionPrimaryKeyConstraintName,
            entity.FindPrimaryKey()!.GetName());
    }

    [Fact]
    public void LosNombresDeLasDosClavesFisicasSonDistintos()
        => Assert.NotEqual(EditorialDatabaseConflict.ArticlePrimaryKeyConstraintName,
            EditorialDatabaseConflict.RevisionPrimaryKeyConstraintName);
}
