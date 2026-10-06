using Npgsql;
using Sillar.Core;
using Sillar.Modules.Catalog;
using Sillar.Modules.Crm;
using Sillar.Shared.Configuration;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Modules.B2B.Tests;

/// <summary>
/// Una base propia por prueba: se crea vacía, se instala lo que la prueba
/// pida, y se destruye al terminar aunque falle.
/// </summary>
/// <remarks>
/// Se llama <c>sillar_b2b_prueba_*</c> y vive en el mismo servidor que la
/// cadena del entorno, pero <b>no es la base de la cadena</b>: estas pruebas
/// desinstalan catálogo y clientes, y hacerlo en la efímera de la puerta
/// rompería a las demás suites. Es el patrón de
/// <c>Sillar.Core.Tests/BaseDePrueba.ConBaseVaciaAsync</c>.
/// </remarks>
internal static class BaseEfimera
{
    public static async Task ConBaseAsync(Func<string, Task> cuerpo)
    {
        DotEnv.Load();
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay servidor donde crear una base de prueba.");
        }

        var nombre = $"sillar_b2b_prueba_{Guid.NewGuid():N}"[..40];
        var admin = new NpgsqlConnectionStringBuilder(cadena) { Database = "postgres", Pooling = false }.ConnectionString;
        var propia = new NpgsqlConnectionStringBuilder(cadena) { Database = nombre, Pooling = false }.ConnectionString;

        await EjecutarAsync(admin, $"CREATE DATABASE \"{nombre}\"");

        try
        {
            await cuerpo(propia);
        }
        finally
        {
            await EjecutarAsync(admin, $"DROP DATABASE IF EXISTS \"{nombre}\" WITH (FORCE)");
        }
    }

    public static async Task InstalarAsync(string cadena, params IModuleMigrations[] modulos)
    {
        foreach (var modulo in modulos)
        {
            await modulo.ApplyMigrationsAsync(cadena, CancellationToken.None);
        }
    }

    public static Task InstalarBaseDeM07Async(string cadena)
        => InstalarAsync(cadena, new CoreModule(), new CatalogModule(), new CrmModule());

    public static async Task EjecutarAsync(string cadena, string sql)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    public static async Task<T> EscalarAsync<T>(string cadena, string sql)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return (T)(await comando.ExecuteScalarAsync())!;
    }

    public static Task<bool> ExisteSchemaAsync(string cadena, string schema)
        => EscalarAsync<bool>(cadena, $"SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = '{schema}')");

    /// <summary>Las FK de b2b hacia otros schemas, por nombre.</summary>
    public static async Task<string[]> FkCruzadasDeB2bAsync(string cadena)
    {
        var lista = await EscalarAsync<string>(cadena, """
            SELECT coalesce(string_agg(c.conname, ',' ORDER BY c.conname), '')
              FROM pg_constraint c
              JOIN pg_namespace n  ON n.oid = c.connamespace
              JOIN pg_class r      ON r.oid = c.confrelid
              JOIN pg_namespace rn ON rn.oid = r.relnamespace
             WHERE c.contype = 'f' AND n.nspname = 'b2b' AND rn.nspname <> 'b2b'
            """);
        return lista.Length == 0 ? [] : lista.Split(',');
    }

    /// <summary>El texto de <c>database/modules/&lt;modulo&gt;/&lt;archivo&gt;</c> del árbol.</summary>
    public static string Script(string modulo, string archivo)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var ruta = Path.Combine(dir.FullName, "database", "modules", modulo, archivo);
            if (File.Exists(ruta))
            {
                return File.ReadAllText(ruta);
            }
        }

        throw new FileNotFoundException($"No se encontró database/modules/{modulo}/{archivo} subiendo desde {AppContext.BaseDirectory}.");
    }
}
