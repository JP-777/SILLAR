using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sillar.Shared.Configuration;

namespace Sillar.Modules.B2B.Data;

/// <summary>Construye el contexto para las herramientas de EF Core.</summary>
public sealed class B2bDbContextFactory : IDesignTimeDbContextFactory<B2bDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Default";

    public B2bDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<B2bDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                B2bDbContext.MigrationsHistoryTable,
                B2bDbContext.Schema))
            .Options;

        return new B2bDbContext(options);
    }
}
