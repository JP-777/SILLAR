using Npgsql;
using Sillar.Shared.Configuration;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Core.Setup;

/// <summary>Qué es la base a la que apunta la conexión, a efectos de instalar.</summary>
internal enum EstadoDelDestino
{
    /// <summary>No hay objetos de aplicación. Se puede empezar.</summary>
    Pristino,

    /// <summary>
    /// Lo que hay pertenece solo a SILLAR: una instalación que empezó y no
    /// terminó. Se puede continuar.
    /// </summary>
    Reanudable,

    /// <summary>Hay algo que no es de SILLAR. No se toca.</summary>
    NoSeguro
}

/// <summary>El veredicto sobre el destino, con lo que lo justifica.</summary>
/// <param name="Estado">Qué se decidió.</param>
/// <param name="Destino">Base, servidor y puerto. Nunca la contraseña.</param>
/// <param name="Evidencias">Lo que se encontró y no es de SILLAR. Vacío si nada.</param>
internal sealed record DiagnosticoDelDestino(
    EstadoDelDestino Estado,
    DestinoDeConexion Destino,
    IReadOnlyList<string> Evidencias);

/// <summary>Lo que se sabe de un schema de SILLAR que ya existe en la base.</summary>
/// <param name="TieneHistorial">Si tiene su tabla de historial de migraciones.</param>
/// <param name="Objetos">Objetos de aplicación dentro, sin contar el historial.</param>
/// <param name="Aplicadas">Migraciones que su historial da por aplicadas.</param>
/// <param name="Conocidas">Migraciones que este binario conoce para ese módulo.</param>
internal sealed record SchemaDeSillarObservado(
    bool TieneHistorial,
    IReadOnlyList<string> Objetos,
    IReadOnlyList<string> Aplicadas,
    IReadOnlyList<string> Conocidas);

/// <summary>Lo observado en la base, antes de decidir nada.</summary>
/// <param name="Schemas">Los schemas que no son internos de PostgreSQL.</param>
/// <param name="ObjetosEnPublic">Objetos de aplicación en <c>public</c>.</param>
/// <param name="DeSillar">Los schemas de SILLAR que existen, por nombre.</param>
internal sealed record ObservacionDelDestino(
    IReadOnlyList<string> Schemas,
    IReadOnlyList<string> ObjetosEnPublic,
    IReadOnlyDictionary<string, SchemaDeSillarObservado> DeSillar);

/// <summary>
/// Decide si el instalador puede aplicar migraciones en la base a la que apunta
/// la conexión.
/// </summary>
/// <remarks>
/// <para>
/// <b>De dónde sale.</b> El instalador aplica las migraciones de los módulos
/// desplegados; la activación solo comprueba. Pero aplicarlas en la base
/// equivocada crea el esquema de CORE donde no debe, y eso ya no lo deshace
/// ningún mensaje — es la lección del pendiente del 42P01: <i>una conexión
/// potencialmente equivocada no se arregla creando tablas sin comprobar el
/// destino</i>. Así que antes de aplicar nada se mira qué hay.
/// </para>
/// <para>
/// <b>Tres estados y no dos</b>, porque «vacía» no basta: una instalación que
/// migró dos módulos y falló en el tercero no está vacía, y tiene que poder
/// continuar. Lo que se exige es que <b>todo lo que hay sea de SILLAR</b>.
/// </para>
/// <para>
/// <b>Qué es «de SILLAR», sin ninguna lista escrita a mano.</b> Los schemas
/// que declaran los módulos desplegados que tienen migraciones
/// (<c>IModule.Schema</c> + <c>IModuleMigrations</c>). Dentro de cada uno, su
/// historial tiene que existir si hay objetos, y no puede contener migraciones
/// que este binario no conozca: eso lo dice la infraestructura de migraciones
/// de cada módulo, que es donde está la autoridad (ADR-009).
/// </para>
/// <para>
/// <b>Y la única interpretación que hay que tener presente: las extensiones.</b>
/// Catálogo y CRM ejecutan <c>CREATE EXTENSION IF NOT EXISTS pg_trgm</c> y
/// <c>unaccent</c> sin schema, así que sus objetos caen en <c>public</c>. Una
/// instalación a medias deja, por tanto, objetos de SILLAR en <c>public</c>.
/// Para no confundirla con una base ajena, los objetos que pertenecen a una
/// extensión no cuentan como objetos de aplicación: lo dice PostgreSQL, con
/// <c>pg_depend.deptype = 'e'</c>, no una lista nuestra. Cualquier tabla,
/// vista, secuencia, función o tipo de <c>public</c> que no sea de una
/// extensión sí cuenta, y basta para negarse.
/// </para>
/// </remarks>
internal static class DestinoDeInstalacion
{
    /// <summary>
    /// La decisión, separada de la lectura de la base para poder provocarla.
    /// </summary>
    /// <param name="observado">Lo que se leyó.</param>
    /// <param name="schemasDeSillar">Los schemas que declaran los módulos con migraciones.</param>
    /// <param name="destino">A qué base y servidor apunta la conexión.</param>
    public static DiagnosticoDelDestino Clasificar(
        ObservacionDelDestino observado,
        IReadOnlySet<string> schemasDeSillar,
        DestinoDeConexion destino)
    {
        var evidencias = new List<string>();

        foreach (var schema in observado.Schemas)
        {
            if (schema != "public" && !schemasDeSillar.Contains(schema))
            {
                evidencias.Add($"el schema «{schema}» no pertenece a ningún módulo desplegado");
            }
        }

        foreach (var objeto in observado.ObjetosEnPublic)
        {
            evidencias.Add($"«public.{objeto}» no es de SILLAR");
        }

        foreach (var (schema, visto) in observado.DeSillar.OrderBy(par => par.Key, StringComparer.Ordinal))
        {
            // Un schema con nuestro nombre pero sin nuestro historial y con
            // cosas dentro no es una instalación a medias: es de otro, o se
            // creó a mano. Migrar encima mezclaría lo suyo con lo nuestro.
            if (!visto.TieneHistorial && visto.Objetos.Count > 0)
            {
                evidencias.Add(
                    $"el schema «{schema}» existe y tiene objetos ({string.Join(", ", visto.Objetos.Take(5))}), " +
                    "pero no el historial de migraciones de SILLAR");
            }

            var desconocidas = visto.Aplicadas.Except(visto.Conocidas, StringComparer.Ordinal).ToList();

            if (desconocidas.Count > 0)
            {
                evidencias.Add(
                    $"el historial de «{schema}» tiene migraciones que este binario no conoce " +
                    $"({string.Join(", ", desconocidas)}): es de otra versión o de otra aplicación");
            }
        }

        if (evidencias.Count > 0)
        {
            return new DiagnosticoDelDestino(EstadoDelDestino.NoSeguro, destino, evidencias);
        }

        return new DiagnosticoDelDestino(
            observado.DeSillar.Count == 0 ? EstadoDelDestino.Pristino : EstadoDelDestino.Reanudable,
            destino,
            []);
    }

    /// <summary>Lee la base y decide.</summary>
    /// <param name="connectionString">La conexión real, la misma con la que se migraría.</param>
    /// <param name="modulos">Los módulos con migraciones, con su schema.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public static async Task<DiagnosticoDelDestino> InspeccionarAsync(
        string connectionString,
        IReadOnlyList<(string Schema, IModuleMigrations Migraciones)> modulos,
        CancellationToken cancellationToken)
    {
        var destino = DestinoDeConexion.DeCadena(connectionString);
        var schemasDeSillar = modulos.Select(m => m.Schema).ToHashSet(StringComparer.Ordinal);

        await using var conexion = new NpgsqlConnection(connectionString);
        await conexion.OpenAsync(cancellationToken);

        var schemas = await SchemasAsync(conexion, cancellationToken);
        var enPublic = schemas.Contains("public")
            ? await ObjetosDeAplicacionAsync(conexion, "public", cancellationToken)
            : [];

        var deSillar = new Dictionary<string, SchemaDeSillarObservado>(StringComparer.Ordinal);

        foreach (var (schema, migraciones) in modulos)
        {
            if (!schemas.Contains(schema))
            {
                continue;
            }

            var tieneHistorial = await ExisteAsync(conexion, schema, migraciones.MigrationsHistoryTable, cancellationToken);
            var objetos = (await ObjetosDeAplicacionAsync(conexion, schema, cancellationToken))
                .Where(objeto => objeto != $"{migraciones.MigrationsHistoryTable} (tabla)")
                .ToList();

            // La autoridad sobre qué migraciones son de este módulo es su propia
            // infraestructura de EF: se le pregunta a ella.
            var aplicadas = tieneHistorial
                ? await migraciones.AppliedMigrationsAsync(connectionString, cancellationToken)
                : [];

            deSillar[schema] = new SchemaDeSillarObservado(
                tieneHistorial,
                objetos,
                aplicadas,
                migraciones.KnownMigrations(connectionString));
        }

        return Clasificar(new ObservacionDelDestino(schemas, enPublic, deSillar), schemasDeSillar, destino);
    }

    private static async Task<List<string>> SchemasAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        const string sql = """
            SELECT nspname FROM pg_namespace
            WHERE nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
              AND nspname NOT LIKE 'pg\_temp\_%'
              AND nspname NOT LIKE 'pg\_toast\_temp\_%'
            ORDER BY nspname
            """;

        return await ListaAsync(conexion, sql, null, ct);
    }

    /// <summary>
    /// Tablas, vistas, secuencias, tipos y funciones de un schema que <b>no</b>
    /// pertenecen a ninguna extensión.
    /// </summary>
    private static async Task<List<string>> ObjetosDeAplicacionAsync(
        NpgsqlConnection conexion,
        string schema,
        CancellationToken ct)
    {
        const string sql = """
            SELECT format('%s (%s)', c.relname, CASE c.relkind
                       WHEN 'r' THEN 'tabla' WHEN 'p' THEN 'tabla'
                       WHEN 'v' THEN 'vista' WHEN 'm' THEN 'vista materializada'
                       WHEN 'S' THEN 'secuencia' WHEN 'f' THEN 'tabla externa'
                       WHEN 'c' THEN 'tipo compuesto' END)
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = @schema
              AND c.relkind IN ('r', 'p', 'v', 'm', 'S', 'f', 'c')
              AND NOT EXISTS (SELECT 1 FROM pg_depend d
                              WHERE d.classid = 'pg_class'::regclass AND d.objid = c.oid AND d.deptype = 'e')
            UNION ALL
            SELECT format('%s() (función)', p.proname)
            FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = @schema
              AND NOT EXISTS (SELECT 1 FROM pg_depend d
                              WHERE d.classid = 'pg_proc'::regclass AND d.objid = p.oid AND d.deptype = 'e')
            UNION ALL
            SELECT format('%s (tipo)', t.typname)
            FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
            WHERE n.nspname = @schema
              AND t.typtype IN ('e', 'd', 'r', 'm')
              AND NOT EXISTS (SELECT 1 FROM pg_depend d
                              WHERE d.classid = 'pg_type'::regclass AND d.objid = t.oid AND d.deptype = 'e')
            ORDER BY 1
            """;

        return await ListaAsync(conexion, sql, schema, ct);
    }

    private static async Task<bool> ExisteAsync(NpgsqlConnection conexion, string schema, string tabla, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(
            "SELECT to_regclass(format('%I.%I', @schema, @tabla)) IS NOT NULL", conexion);
        comando.Parameters.AddWithValue("schema", schema);
        comando.Parameters.AddWithValue("tabla", tabla);
        return (bool)(await comando.ExecuteScalarAsync(ct))!;
    }

    private static async Task<List<string>> ListaAsync(NpgsqlConnection conexion, string sql, string? schema, CancellationToken ct)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);

        if (schema is not null)
        {
            comando.Parameters.AddWithValue("schema", schema);
        }

        var lista = new List<string>();
        await using var lector = await comando.ExecuteReaderAsync(ct);

        while (await lector.ReadAsync(ct))
        {
            lista.Add(lector.GetString(0));
        }

        return lista;
    }
}
