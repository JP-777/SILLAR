using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Sillar.Modules.Sales.Data;
using Sillar.Shared.Replication;
using static Sillar.Host.CommerceBridge.Tests.ComposicionVerticalTests;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>
/// P1-POST-G6-R7: una instalación real de CORE sobre PostgreSQL desechable, recorrida en sus tres
/// fases con los mecanismos de CORE (instalación, alta de cuentas, activación de Reporting e
/// inicio de sesión) y Kestrel solo en bucle local. Sin base disponible, las pruebas se omiten,
/// igual que las físicas de CORE (BaseDePrueba.cs:128-133); <c>.env</c> nunca se lee.
/// </summary>
public sealed class InstalacionRealR7 : IAsyncLifetime
{
    public const string ClaveSuper = "Turquesa-Lluvia-Faro-2026";
    public const string ClaveAdmin = "Granito-Brisa-Puerto-2026";
    public const string ClaveEditor = "Cobalto-Niebla-Muelle-2026";

    public bool Disponible { get; private set; }
    public string Servidor { get; private set; } = "";
    public string Base { get; } = $"sillar_r7_{Guid.NewGuid():N}";
    public string Cadena { get; private set; } = "";
    public List<string> Evidencia { get; } = [];
    public AuthenticatedHost? Host { get; private set; }
    public HttpClient Http { get; private set; } = null!;
    public BaseVentasSimulada Ventas { get; } = new();
    public SalesDbContext? Ctx { get; private set; }
    public string CookieSuper { get; private set; } = "";
    public string CookieAdmin { get; private set; } = "";
    public string CookieEditor { get; private set; } = "";
    public string CookieRevocada { get; private set; } = "";
    public string HostAntesDelReinicio { get; private set; } = "";
    public string HostTrasElReinicio { get; private set; } = "";
    public long SesionesAntesDelReinicio { get; private set; }

    public IConfiguration Configuracion(string? cadena = null) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Default"] = cadena ?? Cadena,
        [NegativeCommerceIntegration.OptInKey] = NegativeCommerceIntegration.OptInValue,
    }).Build();

    public async ValueTask InitializeAsync()
    {
        var servidor = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(servidor))
        {
            return;
        }

        var cs = new NpgsqlConnectionStringBuilder(servidor);
        if (cs.Host is not ("127.0.0.1" or "localhost"))
        {
            throw new InvalidOperationException($"R7 solo usa una instancia de bucle local; la cadena apunta a {cs.Host}.");
        }

        Servidor = new NpgsqlConnectionStringBuilder(servidor) { Database = "postgres" }.ConnectionString;
        Cadena = new NpgsqlConnectionStringBuilder(servidor) { Database = Base }.ConnectionString;
        Disponible = true;
        Evidencia.Add($"servidor={cs.Host}:{cs.Port} usuario={cs.Username} base_de_la_prueba={Base}");
        Evidencia.AddRange(await Consultar(Servidor,
            "select 'version=' || version()",
            "select 'postmaster_inicio=' || pg_postmaster_start_time()",
            "select 'bases_antes=' || string_agg(datname, ',' order by datname) from pg_database"));
        await Ejecutar(Servidor, $"CREATE DATABASE \"{Base}\"");
        Evidencia.Add($"base_creada={Base}");

        // Fase 1 · modo instalación: POST /api/setup aplica las migraciones de CORE y crea la super administración.
        await using (var instalacion = NegativeCommerceAuthenticatedHost.BuildSetupHost(Configuracion(), Environments.Development))
        {
            await instalacion.StartAsync();
            using var http = Cliente(instalacion);
            var setup = await http.PostAsJsonAsync("/api/setup", new
            {
                businessName = "Instalación de prueba R7",
                licenseType = "trial",
                admin = new { fullName = "Super Administración R7", email = "super@r7.ejemplo.test", password = ClaveSuper },
            });
            Evidencia.Add($"fase1 POST /api/setup → {(int)setup.StatusCode}");
            if (setup.StatusCode != HttpStatusCode.Created)
            {
                Evidencia.Add("fase1 cuerpo=" + await setup.Content.ReadAsStringAsync());
                Escribir("r7-instalacion-real.txt", Evidencia);
            }

            Assert.Equal(HttpStatusCode.Created, setup.StatusCode);
            await instalacion.StopAsync();
        }

        Evidencia.AddRange(await Consultar(Cadena,
            "select 'migraciones=' || string_agg(\"MigrationId\", ',' order by \"MigrationId\") from core.\"__migrations\"",
            "select 'schemas=' || string_agg(nspname, ',' order by nspname) from pg_namespace where nspname not like 'pg_%' and nspname <> 'information_schema'",
            "select 'tablas_core=' || string_agg(tablename, ',' order by tablename) from pg_tables where schemaname = 'core'",
            "select 'tablas_fuera_de_core=' || count(*) from pg_tables where schemaname not in ('core', 'pg_catalog', 'information_schema')"));

        // Fase 2 · modo normal con solo CORE activo: la super administración da de alta las cuentas y activa Reporting.
        var fase2 = await NegativeCommerceAuthenticatedHost.BuildAsync(Configuracion(), Environments.Development, null, CancellationToken.None);
        Evidencia.Add($"fase2 activos=[{string.Join(",", fase2.ActiveModules)}] reporting_montado={fase2.ReportingMounted}");
        Assert.False(fase2.ReportingMounted);
        await using (fase2.App)
        {
            await fase2.App.StartAsync();
            using var http = Cliente(fase2.App);
            var (cookie, csrf) = await Entrar(http, "super@r7.ejemplo.test", ClaveSuper);
            var noMontada = await Pedir(http, HttpMethod.Get, "/api/admin/reporting/orders?start=2026-10-01T00:00:00Z&end=2026-10-02T00:00:00Z", cookie);
            Evidencia.Add($"fase2 GET /api/admin/reporting/orders (Reporting inactivo) → {(int)noMontada.StatusCode}");
            foreach (var (correo, clave, rol) in new[] { ("admin@r7.ejemplo.test", ClaveAdmin, "admin"), ("editor@r7.ejemplo.test", ClaveEditor, "editor") })
            {
                var alta = await Pedir(http, HttpMethod.Post, "/api/admin/users", cookie, csrf,
                    new { fullName = $"Cuenta {rol} R7", email = correo, password = clave, role = rol, phone = (string?)null });
                Evidencia.Add($"fase2 POST /api/admin/users rol={rol} → {(int)alta.StatusCode}");
                Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
            }

            var activar = await Pedir(http, HttpMethod.Post, "/api/admin/modules/reporting/activate", cookie, csrf);
            Evidencia.Add($"fase2 POST /api/admin/modules/reporting/activate → {(int)activar.StatusCode}");
            Assert.True(activar.IsSuccessStatusCode, await activar.Content.ReadAsStringAsync());
            await fase2.App.StopAsync();
        }

        // Fase 3 · reinicio: CORE da Reporting por activo y el Host monta M10 sobre la composición negativa.
        Ventas.Pedidos.AddRange(CohorteR6());
        Ventas.Pagos.AddRange(PagosR6());
        Ctx = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1").AddInterceptors(Ventas).Options,
            new NodeIdentity("prueba"), TimeProvider.System);
        Host = await NegativeCommerceAuthenticatedHost.BuildAsync(Configuracion(), Environments.Development, s =>
        {
            s.RemoveAll<SalesDbContext>();
            s.AddSingleton(Ctx);
        }, CancellationToken.None);
        Evidencia.Add($"fase3 activos=[{string.Join(",", Host.ActiveModules)}] reporting_montado={Host.ReportingMounted}");
        Assert.True(Host.ReportingMounted);
        await Host.App.StartAsync();
        Http = Cliente(Host.App);
        Evidencia.Add($"fase3 escucha={string.Join(",", Direcciones(Host.App))}");

        CookieSuper = (await Entrar(Http, "super@r7.ejemplo.test", ClaveSuper)).Cookie;
        CookieAdmin = (await Entrar(Http, "admin@r7.ejemplo.test", ClaveAdmin)).Cookie;
        CookieEditor = (await Entrar(Http, "editor@r7.ejemplo.test", ClaveEditor)).Cookie;
        var (revocable, csrfRevocable) = await Entrar(Http, "admin@r7.ejemplo.test", ClaveAdmin);
        var salir = await Pedir(Http, HttpMethod.Post, "/api/admin/auth/logout", revocable, csrfRevocable);
        Evidencia.Add($"fase3 POST /api/admin/auth/logout (sesión a revocar) → {(int)salir.StatusCode}");
        Assert.True(salir.IsSuccessStatusCode);
        CookieRevocada = revocable;
        // Fase 4 · R8 (D): reinicio del Host. Las cookies emitidas antes tienen que seguir valiendo
        // —o seguir revocadas— porque la sesión vive en PostgreSQL, no en el proceso.
        HostAntesDelReinicio = string.Join(",", Direcciones(Host.App));
        SesionesAntesDelReinicio = long.Parse((await Consultar(Cadena, "select count(*) from core.admin_sessions"))[0]);
        Http.Dispose();
        await Host.App.StopAsync();
        await Host.App.DisposeAsync();
        Host = await NegativeCommerceAuthenticatedHost.BuildAsync(Configuracion(), Environments.Development, s =>
        {
            s.RemoveAll<SalesDbContext>();
            s.AddSingleton(Ctx);
        }, CancellationToken.None);
        await Host.App.StartAsync();
        Http = Cliente(Host.App);
        HostTrasElReinicio = string.Join(",", Direcciones(Host.App));
        Evidencia.Add($"fase4 reinicio: host_antes={HostAntesDelReinicio} host_despues={HostTrasElReinicio} sesiones_en_base={SesionesAntesDelReinicio} (las cookies son las de la fase 3)");

        Evidencia.AddRange(await Consultar(Cadena,
            "select 'cuentas=' || string_agg(email || ':' || role || ':' || is_active, ',' order by email) from core.admin_users",
            "select 'sesiones=' || count(*) || ' revocadas=' || count(revoked_at) from core.admin_sessions",
            "select 'modulos_activos=' || string_agg(m.code, ',' order by m.code) from core.modules m join core.module_activations a on a.module_id = m.module_id where a.is_active"));
        Escribir("r7-instalacion-real.txt", Evidencia);
    }

    public async ValueTask DisposeAsync()
    {
        if (!Disponible)
        {
            return;
        }

        Http?.Dispose();
        if (Host is not null)
        {
            await Host.App.StopAsync();
            await Host.App.DisposeAsync();
        }

        NpgsqlConnection.ClearAllPools();
        await Ejecutar(Servidor, $"DROP DATABASE IF EXISTS \"{Base}\" WITH (FORCE)");
        Evidencia.AddRange(await Consultar(Servidor, "select 'bases_despues=' || string_agg(datname, ',' order by datname) from pg_database"));
        Escribir("r7-instalacion-real.txt", Evidencia);
    }

    public static IEnumerable<string> Direcciones(WebApplication app)
        => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;

    public static HttpClient Cliente(WebApplication app)
    {
        var direccion = Assert.Single(Direcciones(app));
        Assert.StartsWith("http://127.0.0.1:", direccion, StringComparison.Ordinal);
        return new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { BaseAddress = new Uri(direccion) };
    }

    /// <summary>Inicio de sesión real: devuelve el valor de la cookie emitida y el token CSRF.</summary>
    public static async Task<(string Cookie, string Csrf)> Entrar(HttpClient http, string correo, string clave)
    {
        var r = await http.PostAsJsonAsync("/api/admin/auth/login", new { email = correo, password = clave });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var cookie = r.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("sillar_panel=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        var valor = cookie["sillar_panel=".Length..cookie.IndexOf(';', StringComparison.Ordinal)];
        var cuerpo = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
        return (valor, cuerpo.GetProperty("csrfToken").GetString()!);
    }

    public static async Task<HttpResponseMessage> Pedir(HttpClient http, HttpMethod metodo, string ruta, string? cookie = null, string? csrf = null, object? cuerpo = null)
    {
        using var peticion = new HttpRequestMessage(metodo, ruta);
        if (cookie is not null) peticion.Headers.Add("Cookie", $"sillar_panel={cookie}");
        if (csrf is not null) peticion.Headers.Add("X-CSRF-Token", csrf);
        if (cuerpo is not null) peticion.Content = JsonContent.Create(cuerpo);
        return await http.SendAsync(peticion);
    }

    public static async Task Ejecutar(string cadena, string sql)
    {
        await using var c = new NpgsqlConnection(cadena);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<List<string>> Consultar(string cadena, params string[] consultas)
    {
        var salida = new List<string>();
        await using var c = new NpgsqlConnection(cadena);
        await c.OpenAsync();
        foreach (var sql in consultas)
        {
            await using var cmd = new NpgsqlCommand(sql, c);
            salida.Add(Convert.ToString(await cmd.ExecuteScalarAsync()) ?? "(nulo)");
        }

        return salida;
    }
}

/// <summary>
/// R7: autenticación de CORE → middleware real → autorización administrativa → rutas reales de
/// M10 → lectores reales de M03. Cada caso cuenta las consultas que llegaron a M03.
/// </summary>
public sealed class AutenticacionRealTests(InstalacionRealR7 f) : IClassFixture<InstalacionRealR7>
{
    private const string Ventana = "?start=2026-10-01T00:00:00Z&end=2026-10-02T00:00:00Z";
    private static readonly List<string> Registro = [];

    private void Requiere()
    {
        if (!f.Disponible)
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay instancia PostgreSQL desechable para R7.");
        }
    }

    private async Task<(HttpStatusCode Codigo, string Cuerpo, int ConsultasM03)> Get(string ruta, string? cookie, string quien)
    {
        var antes = f.Ventas.Consultas.Count;
        using var r = await InstalacionRealR7.Pedir(f.Http, HttpMethod.Get, ruta + Ventana, cookie);
        var cuerpo = await r.Content.ReadAsStringAsync();
        var consultas = f.Ventas.Consultas.Count - antes;
        lock (Registro)
        {
            Registro.Add($"{quien} · GET {ruta} → {(int)r.StatusCode} · consultas_M03={consultas}");
            Escribir("r7-casos-http.txt", Registro);
        }

        return (r.StatusCode, cuerpo, consultas);
    }

    public static TheoryData<string> Rutas => new("/api/admin/reporting/orders", "/api/admin/reporting/orders/value", "/api/admin/reporting/payments");

    [Theory]
    [MemberData(nameof(Rutas))]
    public async Task R7_Anonimo_recibe_401_sin_consultar_M03(string ruta)
    {
        Requiere();
        var (codigo, _, consultas) = await Get(ruta, null, "anónimo");
        Assert.Equal(HttpStatusCode.Unauthorized, codigo);
        Assert.Equal(0, consultas);
    }

    [Theory]
    [MemberData(nameof(Rutas))]
    public async Task R7_Editor_autenticado_sin_rol_admin_recibe_403_sin_consultar_M03(string ruta)
    {
        Requiere();
        var (codigo, _, consultas) = await Get(ruta, f.CookieEditor, "editor (autenticado, sin rol admin)");
        Assert.Equal(HttpStatusCode.Forbidden, codigo);
        Assert.Equal(0, consultas);
    }

    [Fact]
    public async Task R7_Una_cookie_que_CORE_no_emitio_recibe_401_sin_consultar_M03()
    {
        Requiere();
        var (codigo, _, consultas) = await Get("/api/admin/reporting/orders", Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "cookie que CORE no emitió");
        Assert.Equal(HttpStatusCode.Unauthorized, codigo);
        Assert.Equal(0, consultas);
    }

    [Fact]
    public async Task R7_Una_sesion_revocada_por_logout_recibe_401_sin_consultar_M03()
    {
        Requiere();
        var (codigo, _, consultas) = await Get("/api/admin/reporting/orders", f.CookieRevocada, "sesión revocada por logout");
        Assert.Equal(HttpStatusCode.Unauthorized, codigo);
        Assert.Equal(0, consultas);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("super_admin")]
    public async Task R7_Admin_y_super_admin_leen_I01_I03_I04_con_una_sola_consulta_agregada(string rol)
    {
        Requiere();
        var cookie = rol == "admin" ? f.CookieAdmin : f.CookieSuper;
        var antes = f.Ventas.Consultas.Count;

        var (codigo, texto, consultas) = await Get("/api/admin/reporting/orders", cookie, rol);

        Assert.Equal(HttpStatusCode.OK, codigo);
        var cuerpo = JsonDocument.Parse(texto).RootElement;
        Assert.Equal("with_data", cuerpo.GetProperty("status").GetString());
        Assert.Equal(6, cuerpo.GetProperty("i01CreatedOrders").GetInt64());
        Assert.Equal(I03Esperado, cuerpo.GetProperty("i03CurrentStatusCounts").EnumerateArray()
            .Select(x => (x.GetProperty("status").GetString()!, x.GetProperty("count").GetInt64())));
        Assert.Equal(2, cuerpo.GetProperty("i04CurrentlyExpiredOrders").GetInt64());
        Assert.Equal((4, 2), (cuerpo.GetProperty("activeOrders").GetInt64(), cuerpo.GetProperty("inactiveOrders").GetInt64()));
        Assert.Equal(1, consultas);
        Assert.Equal(SqlEsperado.I01I03I04, f.Ventas.Consultas[antes].Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(0, f.Ventas.Guardados + f.Ventas.ComandosProhibidos + f.Ventas.Transacciones);
    }

    [Fact]
    public async Task R7_Admin_ve_I02_e_I05_I07_no_medibles_sin_importes()
    {
        Requiere();
        var (codigoValor, valor, consultasValor) = await Get("/api/admin/reporting/orders/value", f.CookieAdmin, "admin");
        Assert.Equal(HttpStatusCode.OK, codigoValor);
        var v = JsonDocument.Parse(valor).RootElement;
        Assert.Equal("not_measurable", v.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, v.GetProperty("byCurrency").ValueKind);
        Assert.Equal(0, consultasValor);

        var (codigoPagos, pagos, consultasPagos) = await Get("/api/admin/reporting/payments", f.CookieAdmin, "admin");
        Assert.Equal(HttpStatusCode.OK, codigoPagos);
        var p = JsonDocument.Parse(pagos).RootElement;
        foreach (var i in new[] { "i05", "i06", "i07" })
        {
            Assert.Equal("not_measurable", p.GetProperty(i).GetProperty("status").GetString());
        }

        Assert.Equal(JsonValueKind.Null, p.GetProperty("i06").GetProperty("byCurrency").ValueKind);
        Assert.Equal("writer_not_accredited", p.GetProperty("notMeasurableReason").GetString());
        Assert.Equal(1, consultasPagos);
    }

    [Fact]
    public async Task R7_La_composicion_negativa_sigue_cerrada_dentro_del_Host_autenticado()
    {
        Requiere();
        await using var scope = f.Host!.App.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        Assert.IsType<Sillar.Modules.Payments.Application.M03ReadOnlyNegativeEffectPortAdapter>(
            Assert.Single(sp.GetServices<Sillar.Modules.Payments.Application.Contracts.IProviderCommercialEffectPort>()));
        Assert.IsType<Sillar.Modules.Payments.Application.Contracts.HoldIndeterminateCommercialRetryPolicy>(
            sp.GetRequiredService<Sillar.Modules.Payments.Application.Contracts.IProviderCommercialRetryPolicy>());
        Assert.Null(sp.GetService<Sillar.Modules.Sales.Contracts.IOrderValueAggregateReader>());
        Assert.Null(sp.GetService<Sillar.Modules.Sales.Contracts.IExternalSettlementCandidate>());
        Assert.Equal(["core", "reporting"], f.Host.ActiveModules);
    }

    /// <summary>H4 (R6): el Host autenticado valida al construir y valida ámbitos; una dependencia rota no arranca.</summary>
    [Fact]
    public async Task R7_H4_El_Host_autenticado_rechaza_dependencias_rotas_y_cautivas_al_construir()
    {
        Requiere();
        foreach (var (caso, sabotaje) in new (string, Action<IServiceCollection>)[]
                 {
                     ("dependencia ausente", s => s.AddSingleton<ServicioHuerfano>()),
                     ("ámbito cautivo en singleton", s => { s.AddScoped<ServicioDeAmbito>(); s.AddSingleton<ServicioCautivo>(); }),
                 })
        {
            var e = await Assert.ThrowsAnyAsync<Exception>(() =>
                NegativeCommerceAuthenticatedHost.BuildAsync(f.Configuracion(), Environments.Development, sabotaje, CancellationToken.None));
            lock (Registro)
            {
                Registro.Add($"H4 R7 · {caso} → rechazado al construir: {e.GetType().Name}");
                Escribir("r7-casos-http.txt", Registro);
            }
        }
    }
}

internal sealed class ServicioDeAmbito;

internal sealed class ServicioCautivo(ServicioDeAmbito ambito)
{
    public ServicioDeAmbito Ambito { get; } = ambito;
}

internal sealed record ServicioNoRegistrado(string Valor);

internal sealed class ServicioHuerfano(ServicioNoRegistrado dependencia)
{
    public ServicioNoRegistrado Dependencia { get; } = dependencia;
}

/// <summary>H4 en el Host de R6, sin base: la misma validación al construir.</summary>
public sealed class ValidacionAlConstruirTests
{
    [Fact]
    public void R7_H4_El_Host_de_Reporting_rechaza_dependencias_rotas_y_cautivas_al_construir()
    {
        Assert.ThrowsAny<Exception>(() => NegativeCommerceReportingHost.Build(Config(), Environments.Development, s => s.AddSingleton<ServicioHuerfano>()));
        Assert.ThrowsAny<Exception>(() => NegativeCommerceReportingHost.Build(Config(), Environments.Development,
            s => { s.AddScoped<ServicioDeAmbito>(); s.AddSingleton<ServicioCautivo>(); }));
        // Control: sin sabotaje construye.
        using var _ = (IDisposable)NegativeCommerceReportingHost.Build(Config(), Environments.Development);
    }

    [Fact]
    public void R7_El_Host_autenticado_solo_acepta_una_base_de_bucle_local()
    {
        var remota = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=db.ejemplo.test;Port=5432;Database=x;Username=x;Password=x",
            [NegativeCommerceIntegration.OptInKey] = NegativeCommerceIntegration.OptInValue,
        }).Build();
        Assert.Throws<InvalidOperationException>(() => NegativeCommerceAuthenticatedHost.BuildSetupHost(remota, Environments.Development));
        Assert.Throws<InvalidOperationException>(() => NegativeCommerceAuthenticatedHost.BuildSetupHost(Config(), Environments.Production));
    }
}
