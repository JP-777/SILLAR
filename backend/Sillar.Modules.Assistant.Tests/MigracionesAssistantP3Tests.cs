using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sillar.Modules.Assistant.Data;
using Sillar.Shared.Data.Modularity;
using Xunit;

namespace Sillar.Modules.Assistant.Tests;

public sealed class MigracionesAssistantP3Tests
{
    private const string CadenaSoloModelo = "Host=127.0.0.1;Port=1;Database=prohibida;Username=nadie;Password=no-conectar;Timeout=1";

    private static AssistantDbContext CrearModelo() => new(
        PersistenciaDeModulo.Opciones<AssistantDbContext>(
            CadenaSoloModelo, AssistantDbContext.Schema, AssistantDbContext.MigrationsHistoryTable));

    [Fact]
    public void MigracionYBaseline_ExistenSinConectarseAPostgreSql()
    {
        using var contexto = CrearModelo();
        var assembly = contexto.GetService<IMigrationsAssembly>();
        Assert.NotNull(assembly.ModelSnapshot);
        Assert.Contains("20261020120000_AssistantInitial", assembly.Migrations.Keys);
        Assert.Equal("assistant", contexto.Model.GetDefaultSchema());
        Assert.Equal("__migrations", AssistantDbContext.MigrationsHistoryTable);
    }

    [Fact]
    public void ModeloActual_CoincideConSnapshot_ParaMigracionesFuturas()
    {
        using var contexto = CrearModelo();
        Assert.False(contexto.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Migracion_InicialSoloDefineTablasDeAssistant()
    {
        using var contexto = CrearModelo();
        var migration = contexto.GetService<IMigrationsAssembly>().CreateMigration(
            contexto.GetService<IMigrationsAssembly>().Migrations["20261020120000_AssistantInitial"],
            "Npgsql.EntityFrameworkCore.PostgreSQL");
        var tablas = migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation>().ToArray();
        Assert.Equal(2, tablas.Length);
        Assert.All(tablas, t => Assert.Equal("assistant", t.Schema));
        Assert.DoesNotContain(migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>(),
            op => op.Sql.Contains("core.", StringComparison.OrdinalIgnoreCase) ||
                  op.Sql.Contains("sales.", StringComparison.OrdinalIgnoreCase));
    }
}
