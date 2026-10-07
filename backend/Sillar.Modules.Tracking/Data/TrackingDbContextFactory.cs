using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sillar.Shared.Configuration;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Tracking.Data;

public sealed class TrackingDbContextFactory : IDesignTimeDbContextFactory<TrackingDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Default";

    public TrackingDbContext CreateDbContext(string[] args)
    {
        var envFile = DotEnv.Load();
        var connection = Environment.GetEnvironmentVariable(ConnectionStringVariable);

        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringVariable}'. " +
                (envFile is null
                    ? "No se encontró .env en la raíz del repositorio."
                    : $"Se leyó '{envFile}', pero no define esa clave."));
        }

        var options = new DbContextOptionsBuilder<TrackingDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable(
                TrackingDbContext.MigrationsHistoryTable,
                TrackingDbContext.Schema))
            .Options;

        return new TrackingDbContext(
            options,
            new NodeIdentity(NodeIdentity.DefaultCode),
            TimeProvider.System);
    }
}
