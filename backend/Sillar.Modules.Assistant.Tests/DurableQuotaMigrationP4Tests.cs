using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Sillar.Modules.Assistant.Data;
using Sillar.Shared.Data.Modularity;
using Xunit;

namespace Sillar.Modules.Assistant.Tests;

public sealed class DurableQuotaMigrationP4Tests
{
    private static AssistantDbContext OnlyMetadata() => new(
        PersistenciaDeModulo.Opciones<AssistantDbContext>(
            "Host=127.0.0.1;Port=1;Database=no_abrir;Username=nadie;Password=prohibido;Timeout=1",
            AssistantDbContext.Schema, AssistantDbContext.MigrationsHistoryTable));

    [Fact]
    public void MigrationInModuleAndSnapshotUnchanged()
    {
        using var db = OnlyMetadata();
        var migrations = db.GetService<IMigrationsAssembly>();
        Assert.Contains("20261020120000_AssistantInitial", migrations.Migrations.Keys);
        Assert.Contains("20261020170000_AssistantQuotaLedger", migrations.Migrations.Keys);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void SqlOwnsOnlyAssistantAndHasDurabilityBarriers()
    {
        using var db = OnlyMetadata();
        var assembly = db.GetService<IMigrationsAssembly>();
        var mig = assembly.CreateMigration(
            assembly.Migrations["20261020170000_AssistantQuotaLedger"],
            "Npgsql.EntityFrameworkCore.PostgreSQL");
        var up = Assert.Single(mig.UpOperations.OfType<SqlOperation>());
        var sql = up.Sql;
        Assert.Contains("CREATE TABLE assistant.quota_accounts", sql);
        Assert.Contains("CREATE TABLE assistant.quota_reservations", sql);
        Assert.Contains("uq_assistant_quota_idempotency", sql);
        Assert.Contains("FOREIGN KEY (installation_id)", sql);
        Assert.Contains("CHECK (running_requests <= concurrency_limit)", sql);
        Assert.False(sql.Contains("core.", StringComparison.OrdinalIgnoreCase));
        Assert.False(sql.Contains("sales.", StringComparison.OrdinalIgnoreCase));
        Assert.False(sql.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase));
        Assert.False(sql.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase));
    }
}
