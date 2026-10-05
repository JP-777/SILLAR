using Microsoft.EntityFrameworkCore.Design;
using Sillar.Shared.Configuration;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Modules.Services.Data;

/// <summary>Construye el contexto para las herramientas de línea de comandos de EF Core.</summary>
/// <remarks>
/// La cadena sale de <c>.env</c> o del entorno, como en CORE. Sin ella falla: una
/// credencial escrita aquí acabaría en el repositorio (CLAUDE.md, «Entorno»).
/// </remarks>
public sealed class ServicesDbContextFactory : IDesignTimeDbContextFactory<ServicesDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Default";

    /// <inheritdoc />
    public ServicesDbContext CreateDbContext(string[] args)
    {
        var envFile = DotEnv.Load();
        var connection = Environment.GetEnvironmentVariable(ConnectionStringVariable);

        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión '{ConnectionStringVariable}'. " +
                (envFile is null
                    ? "No se encontró ningún archivo .env: copia .env.example como .env en la raíz del repositorio."
                    : $"Se leyó '{envFile}', pero no define esa clave."));
        }

        return new ServicesDbContext(PersistenciaDeModulo.Opciones<ServicesDbContext>(connection,
            ServicesDbContext.Schema, ServicesDbContext.MigrationsHistoryTable));
    }
}
