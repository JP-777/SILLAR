using Microsoft.EntityFrameworkCore;

namespace Sillar.Shared.Data.Modularity;

/// <summary>
/// La configuración de conexión de un <c>DbContext</c> de módulo, escrita una
/// sola vez.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué hace falta.</b> Hasta ahora cada módulo configuraba su tabla de
/// historial dentro de <c>RegisterServices</c>. El instalador necesita construir
/// ese mismo contexto <b>fuera</b> del contenedor —en modo instalación los
/// servicios de los módulos no se registran—, y si lo configurase por su cuenta
/// habría dos sitios diciendo dónde está el historial. El día que uno cambiara
/// y el otro no, EF buscaría el historial en otra tabla, creería que no hay
/// nada aplicado e intentaría crear el esquema entero encima del existente.
/// </para>
/// <para>
/// Así que los dos caminos —el contenedor y el instalador— pasan por aquí.
/// </para>
/// </remarks>
public static class PersistenciaDeModulo
{
    /// <summary>Npgsql con el historial de migraciones dentro del schema del módulo.</summary>
    public static DbContextOptionsBuilder Configurar(
        DbContextOptionsBuilder options,
        string connectionString,
        string schema,
        string migrationsHistoryTable)
        => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(migrationsHistoryTable, schema));

    /// <summary>Las mismas opciones, ya construidas, para un contexto de vida corta.</summary>
    public static DbContextOptions<TContext> Opciones<TContext>(
        string connectionString,
        string schema,
        string migrationsHistoryTable)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        Configurar(builder, connectionString, schema, migrationsHistoryTable);
        return builder.Options;
    }
}

/// <summary>
/// Implementación de <see cref="IModuleMigrations"/> sobre un <c>DbContext</c>,
/// para que cada módulo solo tenga que decir cómo se construye el suyo.
/// </summary>
/// <remarks>
/// Cada llamada crea y desecha su propio contexto: el instalador trabaja fuera
/// del contenedor y no debe dejar conexiones colgando.
/// </remarks>
/// <typeparam name="TContext">El contexto del módulo.</typeparam>
/// <param name="crear">Cómo construye el módulo su contexto a partir de una cadena.</param>
/// <param name="migrationsHistoryTable">Su tabla de historial.</param>
public sealed class MigracionesDeContexto<TContext>(
    Func<string, TContext> crear,
    string migrationsHistoryTable) : IModuleMigrations
    where TContext : DbContext
{
    /// <inheritdoc />
    public string MigrationsHistoryTable => migrationsHistoryTable;

    /// <inheritdoc />
    public IReadOnlyList<string> KnownMigrations(string connectionString)
    {
        using var contexto = crear(connectionString);
        return [.. contexto.Database.GetMigrations()];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> AppliedMigrationsAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using var contexto = crear(connectionString);
        return [.. await contexto.Database.GetAppliedMigrationsAsync(cancellationToken)];
    }

    /// <inheritdoc />
    public async Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var contexto = crear(connectionString);
        await contexto.Database.MigrateAsync(cancellationToken);
    }
}
