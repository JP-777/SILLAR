using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sillar.Shared.Configuration;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Crm.Data;

/// <summary>
/// Construye el contexto para las herramientas de línea de comandos de EF Core.
/// </summary>
/// <remarks>
/// Con esta fábrica, <c>dotnet ef</c> no necesita arrancar el host: crear una
/// migración o aplicarla no depende de que el módulo esté activo.
///
/// La cadena de conexión sale de <c>.env</c>, el mismo archivo que usa Docker
/// Compose, para no mantener la credencial en dos sitios.
/// </remarks>
public sealed class CrmDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Default";

    /// <inheritdoc />
    public CrmDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                CrmDbContext.MigrationsHistoryTable,
                CrmDbContext.Schema))
            .Options;

        // En tiempo de diseño el nodo no importa: no se escribe ninguna fila.
        return new CrmDbContext(options, new NodeIdentity(NodeIdentity.DefaultCode), TimeProvider.System);
    }
}
