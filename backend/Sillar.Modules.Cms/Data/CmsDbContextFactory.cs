using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sillar.Shared.Configuration;

namespace Sillar.Modules.Cms.Data;

/// <summary>Construye el contexto para las herramientas de EF Core.</summary>
public sealed class CmsDbContextFactory : IDesignTimeDbContextFactory<CmsDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Default";

    public CmsDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<CmsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                CmsDbContext.MigrationsHistoryTable,
                CmsDbContext.Schema))
            .Options;

        return new CmsDbContext(options);
    }
}
