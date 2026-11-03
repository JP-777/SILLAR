using Npgsql;
using static Sillar.Host.CommerceBridge.Tests.ComposicionVerticalTests;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>
/// P1-POST-G6-R10 (Claude A): trazabilidad de las evidencias físicas.
/// O-1, archivos únicos por instancia y ejecución; O-2, una dueña única por base temporal, sin
/// limpieza cruzada entre instancias del mismo tipo; O-3, el contador de sentencias ve las fallidas.
/// </summary>
public sealed class R10TrazabilidadTests
{
    private static readonly List<string> Registro = [];

    private static void Anotar(string linea)
    {
        lock (Registro)
        {
            if (Registro.Count == 0) Registro.Add($"# ejecucion={Ejecucion}");
            Registro.Add(linea);
            Escribir("r10-trazabilidad.txt", Registro);
        }
    }

    // -----------------------------------------------------------------
    // O-3 · clasificador, sin base
    // -----------------------------------------------------------------

    [Fact]
    public void R10_O3_El_clasificador_separa_exitosas_fallidas_y_mutantes()
    {
        // Entradas como las deja SentenciasDelHost: sin «app=…|db=…|», con las continuaciones unidas.
        string[] entradas =
        [
            "LOG:  statement: DISCARD ALL",
            "LOG:  execute <unnamed>: SELECT 1",
            // Falla al analizar (protocolo extendido): no hay «execute», solo ERROR y STATEMENT.
            "ERROR:  relation \"tabla_r10\" does not exist at character 15",
            "STATEMENT:  SELECT * FROM tabla_r10",
            // Falla al ejecutar: «execute», DETAIL, ERROR y STATEMENT.
            "LOG:  execute <unnamed>: SELECT 1 / $1",
            "DETAIL:  parameters: $1 = '0'",
            "ERROR:  division by zero",
            "STATEMENT:  SELECT 1 / $1",
            // Escritura que falla: cuenta como fallida y como mutante.
            "ERROR:  relation \"tabla_r10\" does not exist at character 13",
            "STATEMENT:  INSERT INTO tabla_r10 VALUES (1)",
            "LOG:  statement: UPDATE x SET y = 1",
            "FATAL:  terminating connection due to administrator command",
        ];

        var c = ClasificadorDeRegistroPg.Clasificar(entradas);

        Assert.Equal(["SELECT 1", "UPDATE x SET y = 1"], c.Exitosas);
        Assert.Equal(["SELECT * FROM tabla_r10", "SELECT 1 / $1", "INSERT INTO tabla_r10 VALUES (1)", "(sin sentencia registrada)"], c.Fallidas);
        Assert.Equal(4, c.Errores.Count);
        Assert.Equal(["UPDATE x SET y = 1", "INSERT INTO tabla_r10 VALUES (1)"], c.Mutantes);
        Assert.Equal("exitosas=2 · fallidas=4 · mutantes=2", c.Resumen);
        Anotar($"O-3 sin base · {c.Resumen}");
    }

    [Fact]
    public void R10_O3_Sin_errores_el_clasificador_da_lo_mismo_que_el_contador_de_R9()
    {
        string[] entradas = ["LOG:  statement: DISCARD ALL", "LOG:  execute <unnamed>: SELECT a FROM b", "DETAIL:  parameters: $1 = '1'", "LOG:  statement: SELECT 2"];
        var c = ClasificadorDeRegistroPg.Clasificar(entradas);
        Assert.Equal(["SELECT a FROM b", "SELECT 2"], c.Exitosas);
        Assert.Empty(c.Fallidas);
        Assert.Empty(c.Mutantes);
    }

    // -----------------------------------------------------------------
    // O-1 · identificadores y archivos, sin base
    // -----------------------------------------------------------------

    [Fact]
    public void R10_O1_Dos_instancias_del_mismo_tipo_tienen_duena_base_y_archivos_distintos()
    {
        var a = new InstalacionFisicaR8();
        var b = new InstalacionFisicaR8();
        var c = new InstalacionRealR7();
        var d = new InstalacionRealR7();

        Assert.NotEqual(a.Fixture, b.Fixture);
        Assert.NotEqual(a.Base, b.Base);
        Assert.NotEqual(c.Fixture, d.Fixture);
        Assert.NotEqual(c.Base, d.Base);
        Assert.Matches("^r8a-[0-9a-f]{8}$", a.Fixture);
        Assert.Matches("^r7-[0-9a-f]{8}$", c.Fixture);
        Assert.NotEqual(ArchivoDe(a.Fixture, "instalacion-fisica.txt"), ArchivoDe(b.Fixture, "instalacion-fisica.txt"));
        Assert.NotEqual(ArchivoDe(c.Fixture, "instalacion-real.txt"), ArchivoDe(d.Fixture, "instalacion-real.txt"));
        Assert.StartsWith($"# ejecucion={Ejecucion} fixture={a.Fixture} base={a.Base}", Cabecera(a.Fixture, a.Base), StringComparison.Ordinal);
        Anotar($"O-1 · r8a {a.Fixture}/{a.Base} ≠ {b.Fixture}/{b.Base} · r7 {c.Fixture}/{c.Base} ≠ {d.Fixture}/{d.Base}");
    }

    [Theory]
    [InlineData("sillar_verify_1762155000000_4242", "sillar_verify_1762155000000_4242")]
    [InlineData("local-x", "local-x")]
    [InlineData("../fuera", null)]
    [InlineData("con espacio", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void R10_O1_El_identificador_de_ejecucion_solo_admite_un_nombre_seguro(string? valor, string? esperado)
    {
        var leido = LeerEjecucion(valor);
        if (esperado is null) Assert.Matches("^local-[0-9]{8}T[0-9]{9}Z-p[0-9]+$", leido);
        else Assert.Equal(esperado, leido);
    }

    // -----------------------------------------------------------------
    // O-2 y O-3 · contra la instancia física
    // -----------------------------------------------------------------

    /// <summary>La instancia física, pasando por la barrera R8-01 completa (sin red y con identidad).</summary>
    private static async Task<(string Entrada, IdentidadEsperadaPg Esperada, CadenaVerificadaPg Verificada)?> Instancia()
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(cadena))
        {
            return null;
        }

        var esperada = AislamientoPostgresTemporal.LeerIdentidad(Environment.GetEnvironmentVariable(AislamientoPostgresTemporal.VariableIdentidad));
        return (cadena, esperada, await AislamientoPostgresTemporal.VerificarAsync(cadena, esperada, TestContext.Current.CancellationToken));
    }

    private static async Task<string> RegistroDesde(string servidor, long inicio, string marca)
    {
        await InstalacionRealR7.Consultar(servidor, $"select '{marca}'");
        var texto = "";
        for (var i = 0; i < 100 && !texto.Contains(marca, StringComparison.Ordinal); i++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
            texto = (await InstalacionRealR7.Consultar(servidor,
                $"select pg_read_file('{InstalacionFisicaR8.Registro}', {inicio}, (pg_stat_file('{InstalacionFisicaR8.Registro}')).size - {inicio})"))[0];
        }

        Assert.Contains(marca, texto, StringComparison.Ordinal);
        return texto;
    }

    /// <summary>Entradas de una aplicación y una base, como las agrupa SentenciasDelHost.</summary>
    private static List<string> Entradas(string texto, string prefijo)
    {
        var entradas = new List<(bool Nuestra, string Texto)>();
        foreach (var linea in texto.Split('\n'))
        {
            if (linea.StartsWith("app=", StringComparison.Ordinal)) entradas.Add((linea.StartsWith(prefijo, StringComparison.Ordinal), linea));
            else if (entradas.Count > 0) entradas[^1] = (entradas[^1].Nuestra, entradas[^1].Texto + "\n" + (linea.StartsWith('\t') ? linea[1..] : linea));
        }

        return [.. entradas.Where(e => e.Nuestra).Select(e => e.Texto[prefijo.Length..].TrimEnd())];
    }

    [Fact]
    public async Task R10_O2_Una_instancia_no_puede_destruir_ni_reclamar_la_base_de_otra_del_mismo_tipo()
    {
        if (await Instancia() is not { } instancia)
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay instancia PostgreSQL desechable para R10.");
            return;
        }

        var (entrada, esperada, verificada) = instancia;

        var servidor = new NpgsqlConnectionStringBuilder(verificada.Cadena) { ApplicationName = "r10-observacion" }.ConnectionString;
        var a = new InstalacionFisicaR8();
        var b = new InstalacionFisicaR8();
        var nombre = a.Base;
        const string Existe = "select count(*) from pg_database where datname = '{0}'";
        var inicio = long.Parse((await InstalacionRealR7.Consultar(servidor, $"select (pg_stat_file('{InstalacionFisicaR8.Registro}')).size"))[0]);

        await AislamientoPostgresTemporal.CrearBasePropiaAsync(entrada, esperada, nombre, a.Fixture, TestContext.Current.CancellationToken);
        try
        {
            Assert.True(AislamientoPostgresTemporal.EsPropia(nombre, a.Fixture));
            Assert.False(AislamientoPostgresTemporal.EsPropia(nombre, b.Fixture));

            // La otra instancia, del mismo tipo, intenta destruirla: no conecta y no destruye nada.
            await AislamientoPostgresTemporal.EliminarBasePropiaAsync(entrada, esperada, nombre, b.Fixture, TestContext.Current.CancellationToken);
            Assert.Equal("1", (await InstalacionRealR7.Consultar(servidor, string.Format(System.Globalization.CultureInfo.InvariantCulture, Existe, nombre)))[0]);

            // Y tampoco puede reclamarla creándola otra vez: se rechaza antes de conectar.
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AislamientoPostgresTemporal.CrearBasePropiaAsync(entrada, esperada, nombre, b.Fixture, TestContext.Current.CancellationToken));
            Assert.Contains($"ya es de {a.Fixture}", e.Message, StringComparison.Ordinal);
            Assert.True(AislamientoPostgresTemporal.EsPropia(nombre, a.Fixture));

            // Una dueña con forma inválida no crea nada.
            var vacia = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AislamientoPostgresTemporal.CrearBasePropiaAsync(entrada, esperada, b.Base, "R8A Mayúsculas", TestContext.Current.CancellationToken));
            Assert.Contains("no es una dueña válida", vacia.Message, StringComparison.Ordinal);
            Assert.Equal("0", (await InstalacionRealR7.Consultar(servidor, string.Format(System.Globalization.CultureInfo.InvariantCulture, Existe, b.Base)))[0]);

            var texto = await RegistroDesde(servidor, inicio, $"r10-marca-{Guid.NewGuid():N}");
            // b nunca abrió una conexión: ni de bases ni de nada.
            Assert.DoesNotContain($"app={b.Fixture}-bases|", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("app=R8A Mayúsculas", texto, StringComparison.Ordinal);
            Anotar($"O-2 · {a.Fixture} creó {nombre}; {b.Fixture} (mismo tipo) intentó destruirla → NO eliminada, sin conexión; intentó reclamarla → rechazada: {e.Message}");
        }
        finally
        {
            await AislamientoPostgresTemporal.EliminarBasePropiaAsync(entrada, esperada, nombre, a.Fixture, TestContext.Current.CancellationToken);
        }

        Assert.Equal("0", (await InstalacionRealR7.Consultar(servidor, string.Format(System.Globalization.CultureInfo.InvariantCulture, Existe, nombre)))[0]);
        Assert.False(AislamientoPostgresTemporal.EsPropia(nombre, a.Fixture));
        Anotar($"O-2 · {a.Fixture} destruyó su propia base {nombre} tras reverificar la identidad");
    }

    [Fact]
    public async Task R10_O3_Contra_PostgreSQL_las_sentencias_fallidas_se_ven_y_el_contador_antiguo_no_las_veia()
    {
        if (await Instancia() is not { } instancia)
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay instancia PostgreSQL desechable para R10.");
            return;
        }

        var (entrada, esperada, verificada) = instancia;

        var duena = $"r10-{Guid.NewGuid():N}"[..12];
        var nombre = $"sillar_r8a_{Guid.NewGuid():N}";
        var servidor = new NpgsqlConnectionStringBuilder(verificada.Cadena) { ApplicationName = "r10-observacion" }.ConnectionString;
        const string App = "r10-contador";
        await AislamientoPostgresTemporal.CrearBasePropiaAsync(entrada, esperada, nombre, duena, TestContext.Current.CancellationToken);
        try
        {
            var inicio = long.Parse((await InstalacionRealR7.Consultar(servidor, $"select (pg_stat_file('{InstalacionFisicaR8.Registro}')).size"))[0]);
            var cadena = new NpgsqlConnectionStringBuilder(verificada.Cadena) { Database = nombre, ApplicationName = App, Pooling = false }.ConnectionString;
            await using (var c = new NpgsqlConnection(cadena))
            {
                await c.OpenAsync(TestContext.Current.CancellationToken);
                async Task Enviar(string sql, bool falla)
                {
                    await using var cmd = new NpgsqlCommand(sql, c);
                    if (sql.Contains("@p", StringComparison.Ordinal)) cmd.Parameters.AddWithValue("p", 0);
                    if (falla) await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken));
                    else await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);
                }

                await Enviar("SELECT 1", falla: false);
                await Enviar("SELECT * FROM tabla_inexistente_r10", falla: true);
                await Enviar("SELECT 1 / @p", falla: true);
                await Enviar("INSERT INTO tabla_inexistente_r10 VALUES (1)", falla: true);
            }

            var texto = await RegistroDesde(servidor, inicio, $"r10-marca-{Guid.NewGuid():N}");
            var entradas = Entradas(texto, $"app={App}|db={nombre}|");
            var nuevo = ClasificadorDeRegistroPg.Clasificar(entradas);
            // El contador de R9 antes de R10: solo «statement:»/«execute», sin mirar los errores.
            var antiguo = entradas.Where(e => e.Contains("statement: ", StringComparison.Ordinal) || e.Contains("execute ", StringComparison.Ordinal)).ToList();

            // Npgsql añade sus lecturas de arranque (versión, tipos): exitosas, ninguna mutante.
            Assert.Contains("SELECT 1", nuevo.Exitosas);
            Assert.DoesNotContain(nuevo.Exitosas, e => e.Contains("tabla_inexistente_r10", StringComparison.Ordinal) || e.StartsWith("SELECT 1 / ", StringComparison.Ordinal));
            Assert.DoesNotContain(nuevo.Exitosas, ClasificadorDeRegistroPg.EsMutante);
            Assert.Equal(3, nuevo.Fallidas.Count);
            Assert.Contains("SELECT * FROM tabla_inexistente_r10", nuevo.Fallidas);
            Assert.Contains("INSERT INTO tabla_inexistente_r10 VALUES (1)", nuevo.Fallidas);
            Assert.Single(nuevo.Fallidas, f => f.StartsWith("SELECT 1 / $1", StringComparison.Ordinal));
            Assert.Equal(["INSERT INTO tabla_inexistente_r10 VALUES (1)"], nuevo.Mutantes);
            Assert.True(antiguo.Count < nuevo.Intentadas.Count, $"el contador antiguo vio {antiguo.Count} de {nuevo.Intentadas.Count}");
            Assert.DoesNotContain(antiguo, e => e.Contains("tabla_inexistente_r10", StringComparison.Ordinal));
            Anotar($"O-3 contra PostgreSQL · {nuevo.Resumen} · errores=[{string.Join(" | ", nuevo.Errores)}] · contador antiguo={antiguo.Count} de {nuevo.Intentadas.Count} intentadas, sin ver ninguna de tabla_inexistente_r10");
        }
        finally
        {
            await AislamientoPostgresTemporal.EliminarBasePropiaAsync(entrada, esperada, nombre, duena, TestContext.Current.CancellationToken);
        }
    }
}
