using System.Data.Common;
using Sillar.Shared.Data.Modularity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Sillar.Core.Data;

/// <summary>Registro del contexto de datos de CORE.</summary>
public static class CoreDataServiceExtensions
{
    /// <summary>Añade <see cref="CoreDbContext"/> al contenedor.</summary>
    public static IServiceCollection AddCoreData(this IServiceCollection services, string connectionString)
        => services.AddDbContext<CoreDbContext>(options => PersistenciaDeModulo.Configurar(
            options, connectionString, CoreDbContext.Schema, CoreDbContext.MigrationsHistoryTable));

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
        => PersistenciaDeModulo.Opciones<CoreDbContext>(
            connectionString, CoreDbContext.Schema, CoreDbContext.MigrationsHistoryTable);

    /// <summary>
    /// Las mismas opciones sobre una conexión <b>ajena y ya abierta</b>, para leer
    /// dentro de la transacción de quien la abrió.
    /// </summary>
    /// <remarks>
    /// El contexto no es dueño de la conexión (<c>contextOwnsConnection: false</c>): al
    /// desecharse no la cierra ni la libera, y como ya está abierta tampoco la abre. Es la
    /// única conexión que ese contexto puede usar: no hay cadena con la que pedir otra.
    /// </remarks>
    internal static DbContextOptions<CoreDbContext> BuildOptions(DbConnection borrowedConnection)
        => new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(
                borrowedConnection,
                contextOwnsConnection: false,
                npgsql => npgsql.MigrationsHistoryTable(CoreDbContext.MigrationsHistoryTable, CoreDbContext.Schema))
            .Options;
}
