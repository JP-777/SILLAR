using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Modules.Reports.Data;
using Sillar.Shared.Configuration;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Modules.Reports.Tests;

public sealed class ReportsPersistenciaTests
{
    [Fact]
    public async Task La_puerta_instala_el_historial_real_sin_tablas_de_negocio_ni_claves_ajenas()
    {
        DotEnv.Load();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException("Falta la conexión efímera de la puerta.");
        var expected = Environment.GetEnvironmentVariable("SILLAR_VERIFY_DATABASE");
        Assert.Matches(@"^sillar_verify_\d+_\d+$", expected ?? "");
        Assert.Equal(expected, new NpgsqlConnectionStringBuilder(connection).Database);
        var ct = TestContext.Current.CancellationToken;
        await using var db = new ReportsDbContext(PersistenciaDeModulo.Opciones<ReportsDbContext>(
            connection, ReportsDbContext.Schema, ReportsDbContext.MigrationsHistoryTable));

        // No migra aquí: retirar Reports de la etapa 4 debe volver roja esta prueba.
        var module = new ReportsModule();
        var known = module.KnownMigrations(connection);
        Assert.NotEmpty(known);
        Assert.Equal(known, await module.AppliedMigrationsAsync(connection, ct));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(ct));
        Assert.Empty(db.Model.GetEntityTypes());

        await using var sql = new NpgsqlConnection(connection);
        await sql.OpenAsync(ct);
        await using var tables = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema='reporting' ORDER BY table_name", sql);
        await using (var reader = await tables.ExecuteReaderAsync(ct))
        {
            var actual = new List<string>();
            while (await reader.ReadAsync(ct)) actual.Add(reader.GetString(0));
            Assert.Equal(["__migrations"], actual);
        }
        await using var foreignKeys = new NpgsqlCommand("""
            SELECT count(*) FROM pg_constraint c
            JOIN pg_class d ON d.oid=c.conrelid JOIN pg_namespace dn ON dn.oid=d.relnamespace
            JOIN pg_class r ON r.oid=c.confrelid JOIN pg_namespace rn ON rn.oid=r.relnamespace
            WHERE c.contype='f' AND (dn.nspname='reporting' OR rn.nspname='reporting')
            """, sql);
        Assert.Equal(0L, await foreignKeys.ExecuteScalarAsync(ct));
    }
}
