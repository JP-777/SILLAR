using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sillar.Shared.Configuration;

namespace Sillar.Modules.Reports.Data;

/// <summary>Construye el contexto para las herramientas de EF Core.</summary>
public sealed class ReportsDbContextFactory : IDesignTimeDbContextFactory<ReportsDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Default";

    public ReportsDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<ReportsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                ReportsDbContext.MigrationsHistoryTable,
                ReportsDbContext.Schema))
            .Options;

        return new ReportsDbContext(options);
    }
}
