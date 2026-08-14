using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Sillar.Core.Data;

/// <summary>Registro del contexto de datos de CORE.</summary>
public static class CoreDataServiceExtensions
{
    /// <summary>Añade <see cref="CoreDbContext"/> al contenedor.</summary>
    public static IServiceCollection AddCoreData(this IServiceCollection services, string connectionString)
        => services.AddDbContext<CoreDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(
                CoreDbContext.MigrationsHistoryTable,
                CoreDbContext.Schema)));

    /// <summary>
    /// Construye las opciones del contexto sin pasar por el contenedor.
    /// </summary>
    /// <remarks>
    /// El host necesita leer las activaciones <b>antes</b> de construir la
    /// aplicación, porque de ellas depende qué servicios se registran. Este
    /// método le da un contexto de vida corta con la misma configuración que el
    /// del contenedor, para que no haya dos formas distintas de conectarse.
    /// </remarks>
    public static DbContextOptions<CoreDbContext> BuildOptions(string connectionString)
        => new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(
                    CoreDbContext.MigrationsHistoryTable,
                    CoreDbContext.Schema))
            .Options;
}
