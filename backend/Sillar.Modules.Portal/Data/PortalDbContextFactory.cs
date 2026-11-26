using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sillar.Shared.Configuration;

namespace Sillar.Modules.Portal.Data;

/// <summary>Construye el contexto para las herramientas de EF Core.</summary>
public sealed class PortalDbContextFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Default";

    public PortalDbContext CreateDbContext(string[] args)
    {
        var envFile = DotEnv.Load();
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringVariable}'. " +
                (envFile is null
                    ? "No se encontró ningún archivo .env: copia .env.example como .env en la raíz."
                    : $"Se leyó '{envFile}', pero no define esa clave."));
        }

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                PortalDbContext.MigrationsHistoryTable,
                PortalDbContext.Schema))
            .Options;

        return new PortalDbContext(options);
    }
}
