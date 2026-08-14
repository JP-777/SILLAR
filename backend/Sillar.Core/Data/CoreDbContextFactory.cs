using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sillar.Shared.Configuration;

namespace Sillar.Core.Data;

/// <summary>
/// Construye el contexto para las herramientas de línea de comandos de EF Core.
/// </summary>
/// <remarks>
/// Con esta fábrica, <c>dotnet ef</c> no necesita arrancar el host: crear una
/// migración o aplicarla no depende de que haya módulos activos ni de que la
/// instalación esté completa.
///
/// La cadena de conexión sale de <c>.env</c>, el mismo archivo que usa Docker
/// Compose, para no mantener la credencial en dos sitios.
/// </remarks>
public sealed class CoreDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Default";

    /// <inheritdoc />
    public CoreDbContext CreateDbContext(string[] args)
    {
        var envFile = DotEnv.Load();

        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringVariable}'. " +
                (envFile is null
                    ? "No se encontró ningún archivo .env: copia .env.example como .env en la raíz del repositorio."
                    : $"Se leyó '{envFile}', pero no define esa clave."));
        }

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                CoreDbContext.MigrationsHistoryTable,
                CoreDbContext.Schema))
            .Options;

        return new CoreDbContext(options);
    }
}
