using Npgsql;
using static Sillar.Host.CommerceBridge.Tests.ComposicionVerticalTests;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>
/// R8-01: la barrera de la fixture física rechaza cadenas incompletas, puertos ajenos e
/// identidades distintas antes de cualquier SQL mutante. Las pruebas sin red escuchan en un
/// puerto propio y comprueban que nadie intentó conectarse a él.
/// </summary>
public sealed class R8_01AislamientoTests
{
    private const string Clave = "ClaveDePrueba01";
    private static readonly List<string> Registro = [];

    private static void Anotar(string linea)
    {
        lock (Registro)
        {
            Registro.Add(linea);
            Escribir("r8-01-rechazos.txt", Registro);
        }
    }

    private static IdentidadEsperadaPg Esperada(int puerto) => new("contenedor-de-prueba", "7700000000000000001", "r8a_desechable", puerto);

    public static TheoryData<string> Cadenas => new(
        "truncada en el primer ;", "sin Port", "Port 5432 explícito", "puerto distinto del contenedor",
        "sin Username", "sin Password", "Password vacío", "sin Database", "Database distinta de postgres",
        "servidor no local", "localhost en vez de 127.0.0.1", "Port repetido", "alias Server", "clave no admitida",
        "valor entre comillas", "valor con blanco final");

    private static (string Cadena, int PuertoEsperado) Construir(string caso, int p) => caso switch
    {
        "truncada en el primer ;" => ("Host=127.0.0.1", p),
        "sin Port" => ($"Host=127.0.0.1;Database=postgres;Username=r8a;Password={Clave}", p),
        "Port 5432 explícito" => ($"Host=127.0.0.1;Port=5432;Database=postgres;Username=r8a;Password={Clave}", p),
        "puerto distinto del contenedor" => ($"Host=127.0.0.1;Port={p};Database=postgres;Username=r8a;Password={Clave}", p == 65535 ? p - 1 : p + 1),
        "sin Username" => ($"Host=127.0.0.1;Port={p};Database=postgres;Password={Clave}", p),
        "sin Password" => ($"Host=127.0.0.1;Port={p};Database=postgres;Username=r8a", p),
        "Password vacío" => ($"Host=127.0.0.1;Port={p};Database=postgres;Username=r8a;Password=", p),
        "sin Database" => ($"Host=127.0.0.1;Port={p};Username=r8a;Password={Clave}", p),
        "Database distinta de postgres" => ($"Host=127.0.0.1;Port={p};Database=sillar;Username=r8a;Password={Clave}", p),
        "servidor no local" => ($"Host=db.ejemplo.test;Port={p};Database=postgres;Username=r8a;Password={Clave}", p),
        "localhost en vez de 127.0.0.1" => ($"Host=localhost;Port={p};Database=postgres;Username=r8a;Password={Clave}", p),
        "Port repetido" => ($"Host=127.0.0.1;Port={p};Port={p};Database=postgres;Username=r8a;Password={Clave}", p),
        "alias Server" => ($"Server=127.0.0.1;Host=127.0.0.1;Port={p};Database=postgres;Username=r8a;Password={Clave}", p),
        "clave no admitida" => ($"Host=127.0.0.1;Port={p};Database=postgres;Username=r8a;Password={Clave};Include Error Detail=true", p),
        "valor entre comillas" => ($"Host=127.0.0.1;Port={p};Database=postgres;Username=r8a;Password='{Clave}'", p),
        _ => ($"Host=127.0.0.1;Port={p};Database=postgres;Username=r8a ;Password={Clave}", p),
    };

    [Theory]
    [MemberData(nameof(Cadenas))]
    public async Task R8_01_Una_cadena_incompleta_o_ajena_se_rechaza_sin_intentar_conectar(string caso)
    {
        using var escucha = new AislamientoPostgresTemporal.Escucha();
        var (cadena, puertoEsperado) = Construir(caso, escucha.Puerto);

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => AislamientoPostgresTemporal.VerificarAsync(cadena, Esperada(puertoEsperado), TestContext.Current.CancellationToken));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.StartsWith("R8-01: ", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Clave, e.Message, StringComparison.Ordinal);
        Assert.False(escucha.AlguienConecto);
        Anotar($"cadena · {caso} → rechazada sin conectar (escucha 127.0.0.1:{escucha.Puerto} sin conexiones): {e.Message}");
    }

    [Fact]
    public void R8_01_La_cadena_completa_y_del_puerto_esperado_pasa_la_validacion_sin_red()
    {
        var v = AislamientoPostgresTemporal.ValidarCadena($"Host=127.0.0.1;Port=55800;Database=postgres;Username=r8a;Password={Clave};Timeout=5", Esperada(55800));
        Assert.Equal(("127.0.0.1", 55800, "r8a"), (v.Host, v.Puerto, v.Usuario));
        Anotar("control · cadena completa del puerto esperado → aceptada por la validación sin red");
    }

    public static TheoryData<string> Identidades => new(
        "sin archivo", "archivo inexistente", "sin system_identifier", "system_identifier no numérico",
        "sin cluster_name", "sin contenedor", "puerto 5432", "campo repetido");

    [Theory]
    [MemberData(nameof(Identidades))]
    public void R8_01_Sin_una_identidad_esperada_completa_no_se_abre_ninguna_conexion(string caso)
    {
        string[] Lineas(params string[] fuera) =>
            [.. new[] { "contenedor=abc", "system_identifier=7700000000000000001", "cluster_name=r8a_desechable", "puerto=55800" }
                .Where(l => !fuera.Any(f => l.StartsWith(f + "=", StringComparison.Ordinal)))];

        var e = Assert.Throws<InvalidOperationException>(() => caso switch
        {
            "sin archivo" => AislamientoPostgresTemporal.LeerIdentidad(null),
            "archivo inexistente" => AislamientoPostgresTemporal.LeerIdentidad(Path.Combine(Path.GetTempPath(), $"r8-01-{Guid.NewGuid():N}.txt")),
            "sin system_identifier" => AislamientoPostgresTemporal.ParsearIdentidad(Lineas("system_identifier")),
            "system_identifier no numérico" => AislamientoPostgresTemporal.ParsearIdentidad([.. Lineas("system_identifier"), "system_identifier=abc"]),
            "sin cluster_name" => AislamientoPostgresTemporal.ParsearIdentidad(Lineas("cluster_name")),
            "sin contenedor" => AislamientoPostgresTemporal.ParsearIdentidad(Lineas("contenedor")),
            "puerto 5432" => AislamientoPostgresTemporal.ParsearIdentidad([.. Lineas("puerto"), "puerto=5432"]),
            _ => AislamientoPostgresTemporal.ParsearIdentidad([.. Lineas(), "puerto=55800"]),
        });
        Assert.StartsWith("R8-01: ", e.Message, StringComparison.Ordinal);
        Anotar($"identidad · {caso} → rechazada: {e.Message}");
    }

    [Theory]
    [InlineData("7700000000000000002", "r8a_desechable")]
    [InlineData("7700000000000000001", "otro_cluster")]
    public void R8_01_Una_identidad_real_distinta_de_la_esperada_se_rechaza(string sysid, string cluster)
    {
        var e = Assert.Throws<InvalidOperationException>(() => AislamientoPostgresTemporal.CompararIdentidad(Esperada(55800), sysid, cluster));
        Assert.Contains("No se ejecuta ningún SQL mutante", e.Message, StringComparison.Ordinal);
        AislamientoPostgresTemporal.CompararIdentidad(Esperada(55800), "7700000000000000001", "r8a_desechable");
        Anotar($"comparación · real {sysid}/{cluster} frente a esperada 7700000000000000001/r8a_desechable → rechazada; la igual se acepta");
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("template1")]
    [InlineData("sillar")]
    [InlineData("sillar_r7_no-hex")]
    [InlineData("sillar_r8a_0123")]
    public async Task R8_01_Solo_se_crea_una_base_con_nombre_temporal_propio_y_sin_conectar_si_no_lo_es(string nombre)
    {
        using var escucha = new AislamientoPostgresTemporal.Escucha();
        var cadena = $"Host=127.0.0.1;Port={escucha.Puerto};Database=postgres;Username=r8a;Password={Clave}";
        var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AislamientoPostgresTemporal.CrearBasePropiaAsync(cadena, Esperada(escucha.Puerto), nombre, "prueba", TestContext.Current.CancellationToken));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.StartsWith("R8-01: ", e.Message, StringComparison.Ordinal);
        Assert.False(escucha.AlguienConecto);
        Anotar($"creación · «{nombre}» → rechazada sin conectar: {e.Message}");
    }

    [Fact]
    public async Task R8_01_Una_base_que_la_fixture_no_creo_no_se_destruye_ni_se_conecta()
    {
        using var escucha = new AislamientoPostgresTemporal.Escucha();
        var cadena = $"Host=127.0.0.1;Port={escucha.Puerto};Database=postgres;Username=r8a;Password={Clave}";
        var ajena = $"sillar_r7_{Guid.NewGuid():N}";
        await AislamientoPostgresTemporal.EliminarBasePropiaAsync(cadena, Esperada(escucha.Puerto), ajena, "prueba", TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(AislamientoPostgresTemporal.EsPropia(ajena, "prueba"));
        Assert.False(escucha.AlguienConecto);
        Anotar($"destrucción · {ajena} (no creada por la fixture) → no se destruye y no se conecta");
    }

    /// <summary>
    /// Contra la instancia real: con su cadena verdadera pero una identidad esperada distinta, la
    /// barrera solo lee su identidad y rechaza; no aparece ninguna base ni ninguna sentencia más.
    /// </summary>
    [Fact]
    public async Task R8_01_Contra_la_instancia_real_una_identidad_esperada_distinta_se_rechaza_sin_SQL_mutante()
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(cadena))
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay instancia PostgreSQL desechable para R8-01.");
        }

        var real = AislamientoPostgresTemporal.LeerIdentidad(Environment.GetEnvironmentVariable(AislamientoPostgresTemporal.VariableIdentidad));
        var verificada = await AislamientoPostgresTemporal.VerificarAsync(cadena, real, TestContext.Current.CancellationToken);
        var lectura = new NpgsqlConnectionStringBuilder(verificada.Cadena) { ApplicationName = "r8-01-observacion" }.ConnectionString;
        // Sin las bases que crean y borran en paralelo las otras fixtures (sillar_r7_*, sillar_r8a_*, sillar_r8_sales_*).
        const string Bases = "select string_agg(datname, ',' order by datname) from pg_database where datname !~ '^sillar_r(7|8a|8_sales)_[0-9a-f]{32}$'";
        const string Tamano = "select (pg_stat_file('log/r8a.log')).size";
        var antes = (await InstalacionRealR7.Consultar(lectura, Bases))[0];
        var inicio = long.Parse((await InstalacionRealR7.Consultar(lectura, Tamano))[0]);

        foreach (var (caso, falsa) in new[]
                 {
                     ("system_identifier distinto", real with { SystemIdentifier = (ulong.Parse(real.SystemIdentifier, System.Globalization.CultureInfo.InvariantCulture) ^ 1UL).ToString(System.Globalization.CultureInfo.InvariantCulture) }),
                     ("cluster_name distinto", real with { ClusterName = real.ClusterName + "_otro" }),
                 })
        {
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => AislamientoPostgresTemporal.VerificarAsync(cadena, falsa, TestContext.Current.CancellationToken));
            Assert.Contains("No se ejecuta ningún SQL mutante", e.Message, StringComparison.Ordinal);
            Anotar($"instancia real · {caso} → rechazada: {e.Message}");
        }

        var marca = $"r8-01-marca-{Guid.NewGuid():N}";
        await InstalacionRealR7.Consultar(lectura, $"select '{marca}'");
        var texto = "";
        for (var i = 0; i < 100 && !texto.Contains(marca, StringComparison.Ordinal); i++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
            texto = (await InstalacionRealR7.Consultar(lectura, $"select pg_read_file('log/r8a.log', {inicio}, (pg_stat_file('log/r8a.log')).size - {inicio})"))[0];
        }

        var sentencias = texto.Split('\n')
            .Where(l => l.StartsWith("app=r8a-verificacion|", StringComparison.Ordinal) && l.Contains("|LOG:", StringComparison.Ordinal))
            .Select(l => l[(l.IndexOf("|LOG:", StringComparison.Ordinal) + 1)..])
            .ToList();
        Assert.Equal(2, sentencias.Count(s => s.Contains("select system_identifier::text, current_setting('cluster_name') from pg_control_system()", StringComparison.Ordinal)));
        Assert.All(sentencias, s => Assert.True(
            s.Contains("from pg_control_system()", StringComparison.Ordinal) || s.EndsWith("statement: DISCARD ALL", StringComparison.Ordinal), s));
        Assert.Equal(antes, (await InstalacionRealR7.Consultar(lectura, Bases))[0]);
        Anotar($"instancia real · sentencias de la verificación en los dos rechazos: {string.Join(" | ", sentencias)}");
        Anotar($"instancia real · bases antes={antes} después={antes}");
    }
}
