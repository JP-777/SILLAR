using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sillar.Shared.Configuration;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Data;

public sealed class ServiceOrdersDbContextFactory : IDesignTimeDbContextFactory<ServiceOrdersDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Default";

    public ServiceOrdersDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<ServiceOrdersDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable(
                ServiceOrdersDbContext.MigrationsHistoryTable,
                ServiceOrdersDbContext.Schema))
            .Options;

        return new ServiceOrdersDbContext(
            options,
            new NodeIdentity(NodeIdentity.DefaultCode),
            TimeProvider.System);
    }
}
