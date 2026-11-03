using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Sillar.Modules.Catalog;
using Sillar.Modules.Catalog.Data;
using Sillar.Modules.Crm;
using Sillar.Modules.Crm.Data;
using Sillar.Modules.Crm.Domain;
using Sillar.Modules.Sales;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Shared.Data.Modularity;
using Sillar.Shared.Replication;
using static Sillar.Host.CommerceBridge.Tests.ComposicionVerticalTests;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>
/// P1-POST-G6-R8, ampliación física: una instalación real de CORE con las migraciones existentes
/// de Catalog, CRM y Sales aplicadas por cada módulo, clientes de prueba creados con el modelo de
/// CRM y pedidos de prueba insertados como <b>preparación de datos</b> (no como creación comercial:
/// el mecanismo legítimo, <c>CreadorDePedidos</c>, exige una autoridad de moneda que no existe).
/// El Host monta el <c>SalesDbContext</c> real de M03 contra esta base: sin simulador ni interceptor.
/// </summary>
/// <remarks>
/// Lo que llega a PostgreSQL se observa desde fuera del proceso: la instancia desechable registra
/// cada sentencia (<c>log_statement = all</c>) con el nombre de aplicación de quien la envía, y la
/// prueba lee ese registro con <c>pg_read_file</c>. El Host usa <see cref="AppHost"/> y la
/// preparación de datos <see cref="AppPreparacion"/>, así que las sentencias del Host se separan
/// sin tocar su composición.
/// </remarks>
public sealed class InstalacionFisicaR8 : IAsyncLifetime
{
    public const string AppHost = "r8a-host";
    public const string AppPreparacion = "r8a-preparacion";
    public const string Registro = "log/r8a.log";
    public const string ClaveSuper = "Turquesa-Lluvia-Faro-2026";
    public const string ClaveAdmin = "Granito-Brisa-Puerto-2026";
    public const string ClaveEditor = "Cobalto-Niebla-Muelle-2026";

    /// <summary>Las tres de Sales que su contexto declara ejecutables; las dos candidatas de pagos no.</summary>
    public static readonly string[] SalesEjecutables =
        ["20260930185029_SalesInitial", "20261026235312_SalesCurrencyCandidate20261026", "20261027145605_SalesPaymentSourceNullableCandidate20261027"];

    public bool Disponible { get; private set; }
    public string Servidor { get; private set; } = "";
    public string Base { get; } = $"sillar_r8a_{Guid.NewGuid():N}";
    public string Cadena { get; private set; } = "";
    public string CadenaHost { get; private set; } = "";
    public List<string> Evidencia { get; } = [];
    public IdentidadEsperadaPg? Esperada { get; private set; }
    public string Entrada { get; private set; } = "";
    /// <summary>
    /// Nombre con el que esta instancia se registra como dueña de su base. R10 (O-2): único por
    /// instancia, no el literal «r8a» que compartían dos instancias del mismo tipo.
    /// </summary>
    public string Fixture { get; } = $"r8a-{Guid.NewGuid():N}"[..12];
    public List<string> InventarioPrevio { get; } = [];
    public AuthenticatedHost? Host { get; private set; }
    public HttpClient Http { get; private set; } = null!;
    public string CookieSuper { get; private set; } = "";
    public string CookieAdmin { get; private set; } = "";
    public string CookieEditor { get; private set; } = "";
    public string CookieRevocada { get; private set; } = "";
    public List<Guid> Clientes { get; } = [];
    public List<string> HuellaDePedidos { get; private set; } = [];

    public IConfiguration Configuracion(string? cadena = null) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Default"] = cadena ?? CadenaHost,
        [NegativeCommerceIntegration.OptInKey] = NegativeCommerceIntegration.OptInValue,
    }).Build();

    /// <summary>
    /// Los pedidos de prueba. Ventana A = [2026-10-01, 2026-10-02): seis, dos de baja, con un pedido
    /// en el instante inicial (incluido) y otro en el final (excluido). Totales distintos de cero
    /// para poder comprobar que ningún importe se publica; moneda NULL (desconocida), nunca inventada.
    /// </summary>
    public static readonly (string Codigo, int Cliente, string Estado, bool Activo, string Creado, decimal Total)[] Pedidos =
    [
        ("R8A-0001", 0, OrderStatus.Cancelled, false, "2026-10-01T00:00:00Z", 11.10m),
        ("R8A-0002", 0, OrderStatus.PendingPayment, true, "2026-10-01T08:00:00Z", 22.20m),
        ("R8A-0003", 1, OrderStatus.Preparing, true, "2026-10-01T10:00:00Z", 33.30m),
        ("R8A-0004", 1, OrderStatus.Expired, true, "2026-10-01T12:00:00Z", 44.40m),
        ("R8A-0005", 2, OrderStatus.Expired, false, "2026-10-01T13:00:00Z", 55.50m),
        ("R8A-0006", 2, OrderStatus.Delivered, true, "2026-10-01T23:59:59.999Z", 66.60m),
        ("R8A-0007", 0, OrderStatus.Expired, true, "2026-10-02T00:00:00Z", 77.70m),
        ("R8A-0008", 1, OrderStatus.PendingPayment, true, "2026-09-30T23:59:59Z", 88.80m),
        ("R8A-0009", 2, OrderStatus.Delivered, true, "2026-10-15T09:30:00Z", 99.90m),
    ];

    public async ValueTask InitializeAsync()
    {
        var servidor = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(servidor))
        {
            return;
        }

        // R8-01: con cadena presente, la barrera decide antes de cualquier SQL. Primero sin red
        // (cadena completa y puerto del contenedor esperado); después, identidad del servidor
        // contra la que dio el canal independiente. Un fallo aquí no es omisión: falla la fixture.
        Esperada = AislamientoPostgresTemporal.LeerIdentidad(Environment.GetEnvironmentVariable(AislamientoPostgresTemporal.VariableIdentidad));
        AislamientoPostgresTemporal.ValidarCadena(servidor, Esperada);
        Entrada = servidor;
        var verificada = await AislamientoPostgresTemporal.VerificarAsync(servidor, Esperada);
        Evidencia.Add(Cabecera(Fixture, Base));
        InventarioPrevio.Add(Cabecera(Fixture, Base));
        Evidencia.Add($"R8-01 barrera superada: contenedor={Esperada.Contenedor} system_identifier={Esperada.SystemIdentifier} cluster_name={Esperada.ClusterName} puerto={verificada.Puerto}");
        var cs = new NpgsqlConnectionStringBuilder(verificada.Cadena);

        Servidor = new NpgsqlConnectionStringBuilder(servidor) { Database = "postgres", ApplicationName = AppPreparacion }.ConnectionString;
        Cadena = new NpgsqlConnectionStringBuilder(servidor) { Database = Base, ApplicationName = AppPreparacion }.ConnectionString;
        CadenaHost = new NpgsqlConnectionStringBuilder(servidor) { Database = Base, ApplicationName = AppHost }.ConnectionString;
        // Disponible solo ahora, tras la barrera: si la identidad se rechaza, la limpieza no hace nada.
        Disponible = true;

        // Identidad de la instancia antes de crear nada: si no registra sentencias, no es la de R8.
        Evidencia.Add($"servidor={cs.Host}:{cs.Port} usuario={cs.Username} base_de_la_prueba={Base}");
        Evidencia.AddRange(await InstalacionRealR7.Consultar(Servidor,
            "select 'version=' || version()",
            "select 'system_identifier=' || system_identifier from pg_control_system()",
            "select 'postmaster_inicio=' || pg_postmaster_start_time()",
            "select 'cluster_name=' || current_setting('cluster_name')",
            "select 'log_statement=' || current_setting('log_statement') || ' logging_collector=' || current_setting('logging_collector') || ' log_filename=' || current_setting('log_filename')",
            "select 'bases_antes=' || string_agg(datname, ',' order by datname) from pg_database"));
        if (!Evidencia.Contains("log_statement=all logging_collector=on log_filename=r8a.log"))
        {
            throw new InvalidOperationException("La instancia no registra sentencias en log/r8a.log: no es la instancia desechable de la ampliación R8.");
        }

        await AislamientoPostgresTemporal.CrearBasePropiaAsync(Entrada, Esperada, Base, Fixture);
        Evidencia.Add($"base_creada={Base} por {Fixture}");

        // Fase 1 · instalación real: POST /api/setup aplica las migraciones de CORE.
        await using (var instalacion = NegativeCommerceAuthenticatedHost.BuildSetupHost(Configuracion(), Environments.Development))
        {
            await instalacion.StartAsync();
            using var http = InstalacionRealR7.Cliente(instalacion);
            var setup = await http.PostAsJsonAsync("/api/setup", new
            {
                businessName = "Instalación de prueba R8",
                licenseType = "trial",
                admin = new { fullName = "Super Administración R8", email = "super@r8.ejemplo.test", password = ClaveSuper },
            });
            Evidencia.Add($"fase1 POST /api/setup → {(int)setup.StatusCode}");
            Assert.Equal(HttpStatusCode.Created, setup.StatusCode);
            await instalacion.StopAsync();
        }

        // Fase 1b · inventario previo, desde los propios contextos, y migraciones de cada módulo por su mecanismo.
        var nodo = new NodeIdentity("r8a");
        await using (var c = new CatalogDbContext(PersistenciaDeModulo.Opciones<CatalogDbContext>(Cadena, CatalogDbContext.Schema, CatalogDbContext.MigrationsHistoryTable), nodo, TimeProvider.System))
        {
            InventarioPrevio.Add("catalog=" + string.Join(",", c.Database.GetMigrations()));
        }

        await using (var c = new CrmDbContext(PersistenciaDeModulo.Opciones<CrmDbContext>(Cadena, CrmDbContext.Schema, CrmDbContext.MigrationsHistoryTable), nodo, TimeProvider.System))
        {
            InventarioPrevio.Add("crm=" + string.Join(",", c.Database.GetMigrations()));
        }

        await using (var c = new SalesDbContext(PersistenciaDeModulo.Opciones<SalesDbContext>(Cadena, SalesDbContext.Schema, SalesDbContext.MigrationsHistoryTable), nodo, TimeProvider.System))
        {
            InventarioPrevio.Add("sales=" + string.Join(",", c.Database.GetMigrations()));
        }

        Assert.Contains("sales=" + string.Join(",", SalesEjecutables), InventarioPrevio);
        Escribir(ArchivoDe(Fixture, "inventario-previo.txt"), InventarioPrevio);
        RegistrarEvidencia(nameof(InstalacionFisicaR8), Fixture, Base, ArchivoDe(Fixture, "inventario-previo.txt"));

        // Orden de dependencias: Sales depende de CRM y Catalog (SalesModule: dependencias duras).
        foreach (var (codigo, aplicar) in new (string, Func<Task>)[]
                 {
                     ("catalog", () => new CatalogModule().ApplyMigrationsAsync(Cadena, CancellationToken.None)),
                     ("crm", () => new CrmModule().ApplyMigrationsAsync(Cadena, CancellationToken.None)),
                     ("sales", () => new SalesModule().ApplyMigrationsAsync(Cadena, CancellationToken.None)),
                 })
        {
            await aplicar();
            Evidencia.Add($"fase1b migraciones aplicadas por {codigo}");
        }

        // Fase 2 · alta de cuentas y activación de Reporting, por CORE.
        var fase2 = await NegativeCommerceAuthenticatedHost.BuildAsync(Configuracion(), Environments.Development, null, CancellationToken.None);
        Assert.False(fase2.ReportingMounted);
        await using (fase2.App)
        {
            await fase2.App.StartAsync();
            using var http = InstalacionRealR7.Cliente(fase2.App);
            var (cookie, csrf) = await InstalacionRealR7.Entrar(http, "super@r8.ejemplo.test", ClaveSuper);
            foreach (var (correo, clave, rol) in new[] { ("admin@r8.ejemplo.test", ClaveAdmin, "admin"), ("editor@r8.ejemplo.test", ClaveEditor, "editor") })
            {
                var alta = await InstalacionRealR7.Pedir(http, HttpMethod.Post, "/api/admin/users", cookie, csrf,
                    new { fullName = $"Cuenta {rol} R8", email = correo, password = clave, role = rol, phone = (string?)null });
                Evidencia.Add($"fase2 POST /api/admin/users rol={rol} → {(int)alta.StatusCode}");
                Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
            }

            var activar = await InstalacionRealR7.Pedir(http, HttpMethod.Post, "/api/admin/modules/reporting/activate", cookie, csrf);
            Evidencia.Add($"fase2 POST /api/admin/modules/reporting/activate → {(int)activar.StatusCode}");
            Assert.True(activar.IsSuccessStatusCode, await activar.Content.ReadAsStringAsync());
            await fase2.App.StopAsync();
        }

        // Fase 2b · PREPARACIÓN DE DATOS DE PRUEBA. Clientes con el modelo de CRM (sus CHECK y su
        // nodo de origen); pedidos por SQL controlado, que no es creación comercial autorizada.
        // Sin líneas, sin pagos, sin liquidaciones y sin moneda.
        await using (var crm = new CrmDbContext(PersistenciaDeModulo.Opciones<CrmDbContext>(Cadena, CrmDbContext.Schema, CrmDbContext.MigrationsHistoryTable), nodo, TimeProvider.System))
        {
            foreach (var n in new[] { "Uno", "Dos", "Tres" })
            {
                var cliente = new Customer { FullName = $"Cliente de prueba R8 {n}", Email = $"cliente.{n.ToLowerInvariant()}@r8.ejemplo.test" };
                crm.Customers.Add(cliente);
                Clientes.Add(cliente.CustomerId);
            }

            await crm.SaveChangesAsync();
        }

        await using (var c = new NpgsqlConnection(Cadena))
        {
            await c.OpenAsync();
            foreach (var p in Pedidos)
            {
                await using var cmd = new NpgsqlCommand(
                    "/* R8 · preparación de datos de prueba, no creación comercial */ "
                    + "INSERT INTO sales.orders (order_id, order_code, customer_id, customer_full_name, customer_email, status, payment_due_at, total_amount, is_active, origin_node, created_at) "
                    + "SELECT @id, @codigo, c.customer_id, c.full_name, c.email, @estado, @creado + interval '2 days', @total, @activo, 'r8a', @creado FROM crm.customers c WHERE c.customer_id = @cliente", c);
                cmd.Parameters.AddWithValue("id", Guid.CreateVersion7());
                cmd.Parameters.AddWithValue("codigo", p.Codigo);
                cmd.Parameters.AddWithValue("estado", p.Estado);
                cmd.Parameters.AddWithValue("creado", DateTimeOffset.Parse(p.Creado, System.Globalization.CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("total", p.Total);
                cmd.Parameters.AddWithValue("activo", p.Activo);
                cmd.Parameters.AddWithValue("cliente", Clientes[p.Cliente]);
                Assert.Equal(1, await cmd.ExecuteNonQueryAsync());
            }
        }

        HuellaDePedidos = await Huella();
        Evidencia.Add($"fase2b clientes={Clientes.Count} pedidos={Pedidos.Length} (preparación de datos de prueba)");

        // Fase 3 · Host con Reporting activo y el SalesDbContext real de M03: sin sustitución.
        Host = await NegativeCommerceAuthenticatedHost.BuildAsync(Configuracion(), Environments.Development, null, CancellationToken.None);
        Evidencia.Add($"fase3 activos=[{string.Join(",", Host.ActiveModules)}] reporting_montado={Host.ReportingMounted}");
        Assert.True(Host.ReportingMounted);
        await Host.App.StartAsync();
        Http = InstalacionRealR7.Cliente(Host.App);
        Evidencia.Add($"fase3 escucha={string.Join(",", InstalacionRealR7.Direcciones(Host.App))}");

        CookieSuper = (await InstalacionRealR7.Entrar(Http, "super@r8.ejemplo.test", ClaveSuper)).Cookie;
        CookieAdmin = (await InstalacionRealR7.Entrar(Http, "admin@r8.ejemplo.test", ClaveAdmin)).Cookie;
        CookieEditor = (await InstalacionRealR7.Entrar(Http, "editor@r8.ejemplo.test", ClaveEditor)).Cookie;
        var (revocable, csrfRevocable) = await InstalacionRealR7.Entrar(Http, "admin@r8.ejemplo.test", ClaveAdmin);
        var salir = await InstalacionRealR7.Pedir(Http, HttpMethod.Post, "/api/admin/auth/logout", revocable, csrfRevocable);
        Assert.True(salir.IsSuccessStatusCode);
        CookieRevocada = revocable;
        EscribirEvidencia();
    }

    /// <summary>R10 (O-1): la evidencia de esta instancia, en su propio archivo e indexada.</summary>
    private void EscribirEvidencia()
    {
        var archivo = ArchivoDe(Fixture, "instalacion-fisica.txt");
        Escribir(archivo, Evidencia);
        RegistrarEvidencia(nameof(InstalacionFisicaR8), Fixture, Base, archivo);
    }

    /// <summary>Cada pedido con su versión de fila física (<c>xmin</c>): cambia si alguien lo escribe.</summary>
    public async Task<List<string>> Huella()
    {
        var salida = new List<string>();
        await using var c = new NpgsqlConnection(Cadena);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "select order_code || '|' || status || '|' || is_active || '|' || to_char(created_at at time zone 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.MS\"Z\"') || '|' || total_amount || '|' || coalesce(currency_code, 'NULL') || '|' || row_version || '|' || xmin::text || '|' || updated_at from sales.orders order by order_code", c);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) salida.Add(r.GetString(0));
        return salida;
    }

    /// <summary>
    /// Ejecuta <paramref name="accion"/> y devuelve las sentencias que el Host envió a PostgreSQL
    /// mientras tanto, leídas del registro de la instancia. Una marca enviada después por la
    /// preparación garantiza que el registro ya contiene todo lo anterior.
    /// </summary>
    public async Task<List<string>> SentenciasDelHost(Func<Task> accion)
    {
        var inicio = long.Parse((await InstalacionRealR7.Consultar(Cadena, $"select (pg_stat_file('{Registro}')).size"))[0]);
        await accion();
        var marca = $"r8a-marca-{Guid.NewGuid():N}";
        await InstalacionRealR7.Consultar(Cadena, $"select '{marca}'");
        string texto = "";
        for (var i = 0; i < 100 && !texto.Contains(marca, StringComparison.Ordinal); i++)
        {
            await Task.Delay(50);
            texto = (await InstalacionRealR7.Consultar(Cadena,
                $"select pg_read_file('{Registro}', {inicio}, (pg_stat_file('{Registro}')).size - {inicio})"))[0];
        }

        Assert.Contains(marca, texto, StringComparison.Ordinal);
        var entradas = new List<string>();
        foreach (var linea in texto.Split('\n'))
        {
            if (linea.StartsWith("app=", StringComparison.Ordinal)) entradas.Add(linea);
            // PostgreSQL antepone un tabulador a cada línea de continuación de una sentencia.
            else if (entradas.Count > 0) entradas[^1] += "\n" + (linea.StartsWith('\t') ? linea[1..] : linea);
        }

        var prefijo = $"app={AppHost}|db={Base}|";
        return [.. entradas
            .Where(e => e.StartsWith(prefijo, StringComparison.Ordinal))
            .Select(e => e[prefijo.Length..].TrimEnd())];
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

        // Solo la base que esta fixture creó, y solo tras reverificar la identidad del servidor.
        await AislamientoPostgresTemporal.EliminarBasePropiaAsync(Entrada, Esperada!, Base, Fixture);
        Evidencia.AddRange(await InstalacionRealR7.Consultar(Servidor, "select 'bases_despues=' || string_agg(datname, ',' order by datname) from pg_database"));
        EscribirEvidencia();
    }
}

/// <summary>
/// R8 ampliación: CORE → autenticación real → Host HTTP → M10 → lector real de M03 → PostgreSQL.
/// Cada respuesta se contrasta con las filas insertadas y con el registro de sentencias.
/// </summary>
public sealed class R8AmpliacionFisicaTests(InstalacionFisicaR8 f) : IClassFixture<InstalacionFisicaR8>
{
    private const string VentanaA = "?start=2026-10-01T00:00:00Z&end=2026-10-02T00:00:00Z";
    private static readonly Dictionary<string, List<string>> Notas = [];
    private static readonly Regex Ventas = new(@"\bsales\.", RegexOptions.CultureInvariant);

    private void Requiere()
    {
        if (!f.Disponible)
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay instancia PostgreSQL desechable para la ampliación R8.");
        }
    }

    /// <summary>R10 (O-1): archivos de la clase, propios de la instancia de fixture que usa, con cabecera e índice.</summary>
    private void Anotar(string archivo, params string[] lineas)
    {
        lock (Notas)
        {
            var propio = ArchivoDe(f.Fixture, archivo);
            if (!Notas.TryGetValue(propio, out var l)) Notas[propio] = l = [Cabecera(f.Fixture, f.Base)];
            l.AddRange(lineas);
            Escribir(propio, l);
            RegistrarEvidencia(nameof(R8AmpliacionFisicaTests), f.Fixture, f.Base, propio);
        }
    }

    /// <summary>Las sentencias de M03: las del Host que tocan el schema <c>sales</c>.</summary>
    private static List<string> DeM03(IEnumerable<string> sentencias) => [.. sentencias.Where(s => Ventas.IsMatch(s))];

    private async Task<(HttpStatusCode Codigo, string Cuerpo, List<string> Host)> Get(string ruta, string? cookie, string quien)
    {
        HttpStatusCode codigo = 0;
        var cuerpo = "";
        var host = await f.SentenciasDelHost(async () =>
        {
            using var r = await InstalacionRealR7.Pedir(f.Http, HttpMethod.Get, ruta, cookie);
            codigo = r.StatusCode;
            cuerpo = await r.Content.ReadAsStringAsync();
        });
        var m03 = DeM03(host);
        Anotar("r8a-casos-http.txt", $"{quien} · GET {ruta} → {(int)codigo} · sentencias_del_host={host.Count} · sentencias_M03={m03.Count}");
        Anotar("r8a-sentencias-m03.txt", [$"--- {quien} · GET {ruta} → {(int)codigo}", .. m03.Select(s => s.Replace("\n", " ", StringComparison.Ordinal))]);
        if ((int)codigo == 200) Anotar("r8a-respuestas-http.txt", $"{quien} · GET {ruta}", cuerpo);
        return (codigo, cuerpo, host);
    }

    private static string Ventana(string inicio, string fin) => $"?start={inicio}&end={fin}";

    // -----------------------------------------------------------------
    // Migraciones, tablas y FK reales
    // -----------------------------------------------------------------

    [Fact]
    public async Task R8A_Las_migraciones_aplicadas_son_CORE_4_Catalog_1_CRM_2_Sales_3_y_ninguna_de_Payments()
    {
        Requiere();
        var l = await InstalacionRealR7.Consultar(f.Cadena,
            "select string_agg(\"MigrationId\", ',' order by \"MigrationId\") from core.\"__migrations\"",
            "select string_agg(\"MigrationId\", ',' order by \"MigrationId\") from catalog.\"__migrations\"",
            "select string_agg(\"MigrationId\", ',' order by \"MigrationId\") from crm.\"__migrations\"",
            "select string_agg(\"MigrationId\", ',' order by \"MigrationId\") from sales.\"__migrations\"",
            "select string_agg(nspname, ',' order by nspname) from pg_namespace where nspname not like 'pg_%' and nspname <> 'information_schema'",
            "select count(*) from pg_tables where schemaname = 'payments'",
            "select coalesce(string_agg(\"MigrationId\", ','), '(ninguna)') from (select \"MigrationId\" from sales.\"__migrations\" where \"MigrationId\" in ('20261026235423_SalesPaymentSourceCandidate20261026', '20261026235530_SalesPaymentRetentionCandidate20261026')) x");
        Anotar("r8a-migraciones-aplicadas.txt",
            $"core={l[0]}", $"catalog={l[1]}", $"crm={l[2]}", $"sales={l[3]}", $"schemas={l[4]}",
            $"tablas_payments={l[5]}", $"candidatas_de_pagos_aplicadas={l[6]}");

        Assert.Equal(4, l[0].Split(',').Length);
        Assert.Equal("20260816073647_CatalogInitial", l[1]);
        Assert.Equal("20260824190200_CrmInitial,20260922115958_CrmCustomersEmailTrimAuthority", l[2]);
        Assert.Equal(string.Join(",", InstalacionFisicaR8.SalesEjecutables), l[3]);
        Assert.Equal("catalog,core,crm,public,sales", l[4]);
        Assert.Equal("0", l[5]);
        Assert.Equal("(ninguna)", l[6]);
    }

    [Fact]
    public async Task R8A_Las_FK_de_Sales_apuntan_a_tablas_reales_de_CRM_y_Catalog()
    {
        Requiere();
        var l = await InstalacionRealR7.Consultar(f.Cadena,
            "select string_agg(schemaname || '.' || tablename, ',' order by schemaname, tablename) from pg_tables where schemaname in ('catalog', 'crm', 'sales')",
            "select string_agg(conname || ':' || conrelid::regclass::text || '->' || confrelid::regclass::text, ',' order by conname) from pg_constraint where contype = 'f' and connamespace = 'sales'::regnamespace and confrelid::regclass::text not like 'sales.%'",
            "select string_agg(conname || ':' || conrelid::regclass::text || '->' || confrelid::regclass::text, ',' order by conname) from pg_constraint where contype = 'f' and connamespace in ('crm'::regnamespace, 'catalog'::regnamespace) and confrelid::regclass::text like 'core.%'",
            "select string_agg(conname || ':' || pg_get_constraintdef(oid), ' ; ' order by conname) from pg_constraint where conrelid = 'sales.orders'::regclass and contype = 'c'");
        Anotar("r8a-tablas-y-fk.txt", $"tablas={l[0]}", $"fk_sales_fuera_de_sales={l[1]}", $"fk_crm_catalog_a_core={l[2]}", $"check_sales_orders={l[3]}");
        Assert.Contains("fk_orders_customer_id:sales.orders->crm.customers", l[1], StringComparison.Ordinal);
        Assert.Contains("fk_order_lines_item_id:sales.order_lines->catalog.product_items", l[1], StringComparison.Ordinal);
        Assert.Contains("crm.customers", l[0], StringComparison.Ordinal);
        Assert.Contains("catalog.product_items", l[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cliente inexistente", "23503")]
    [InlineData("estado fuera del CHECK", "23514")]
    [InlineData("total negativo", "23514")]
    [InlineData("código repetido", "23505")]
    public async Task R8A_La_base_rechaza_pedidos_que_violan_FK_o_CHECK(string caso, string sqlstate)
    {
        Requiere();
        var (cliente, estado, total, codigo) = caso switch
        {
            "cliente inexistente" => (Guid.CreateVersion7(), OrderStatus.PendingPayment, 1m, "R8A-X-1"),
            "estado fuera del CHECK" => (f.Clientes[0], "paid", 1m, "R8A-X-2"),
            "total negativo" => (f.Clientes[0], OrderStatus.PendingPayment, -1m, "R8A-X-3"),
            _ => (f.Clientes[0], OrderStatus.PendingPayment, 1m, "R8A-0001"),
        };
        await using var c = new NpgsqlConnection(f.Cadena);
        await c.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand(
            "/* R8 · intento de prueba que la base debe rechazar */ INSERT INTO sales.orders (order_id, order_code, customer_id, customer_full_name, customer_email, status, payment_due_at, total_amount, origin_node) "
            + "VALUES (@id, @codigo, @cliente, 'Cliente de prueba R8', 'x@r8.ejemplo.test', @estado, now(), @total, 'r8a')", c);
        cmd.Parameters.AddWithValue("id", Guid.CreateVersion7());
        cmd.Parameters.AddWithValue("codigo", codigo);
        cmd.Parameters.AddWithValue("cliente", cliente);
        cmd.Parameters.AddWithValue("estado", estado);
        cmd.Parameters.AddWithValue("total", total);
        var e = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Anotar("r8a-rechazos-de-la-base.txt", $"{caso} → sqlstate={e.SqlState} restricción={e.ConstraintName}");
        Assert.Equal(sqlstate, e.SqlState);
        Assert.Equal(InstalacionFisicaR8.Pedidos.Length, long.Parse((await InstalacionRealR7.Consultar(f.Cadena, "select count(*) from sales.orders"))[0]));
    }

    [Fact]
    public async Task R8A_Los_datos_de_prueba_son_los_declarados_y_cada_pedido_tiene_su_cliente_de_CRM()
    {
        Requiere();
        var l = await InstalacionRealR7.Consultar(f.Cadena,
            "select string_agg(full_name || '|' || email || '|' || origin_node || '|' || is_active, ',' order by full_name) from crm.customers",
            "select count(*) from sales.orders o join crm.customers c on c.customer_id = o.customer_id and c.full_name = o.customer_full_name and c.email = o.customer_email",
            "select count(*) from sales.order_lines",
            "select count(*) from sales.order_payments",
            "select count(*) from sales.orders where currency_code is not null",
            "select count(*) from catalog.product_items");
        Anotar("r8a-datos-de-prueba.txt",
            ["# PREPARACIÓN DE DATOS DE PRUEBA: clientes con el modelo de CRM; pedidos por SQL controlado. No es creación comercial autorizada.",
             $"clientes={l[0]}", $"pedidos_con_cliente_coherente={l[1]}", $"lineas={l[2]} pagos={l[3]} pedidos_con_moneda={l[4]} items_de_catalogo={l[5]}",
             "pedidos (codigo|estado|activo|creado_utc|total|moneda|row_version|xmin|updated_at):", .. f.HuellaDePedidos]);
        Assert.Equal(3, f.Clientes.Count);
        Assert.Equal(InstalacionFisicaR8.Pedidos.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), l[1]);
        Assert.Equal(("0", "0", "0"), (l[2], l[3], l[4]));
    }

    // -----------------------------------------------------------------
    // Autenticación: 401 y 403 sin una sola sentencia a M03
    // -----------------------------------------------------------------

    public static TheoryData<string> Rutas => new("/api/admin/reporting/orders", "/api/admin/reporting/orders/value", "/api/admin/reporting/payments");

    [Theory]
    [MemberData(nameof(Rutas))]
    public async Task R8A_Anonimo_recibe_401_y_PostgreSQL_no_recibe_ninguna_sentencia_de_M03(string ruta)
    {
        Requiere();
        var (codigo, _, host) = await Get(ruta + VentanaA, null, "anónimo");
        Assert.Equal(HttpStatusCode.Unauthorized, codigo);
        Assert.Empty(DeM03(host));
    }

    [Theory]
    [MemberData(nameof(Rutas))]
    public async Task R8A_Editor_recibe_403_y_PostgreSQL_no_recibe_ninguna_sentencia_de_M03(string ruta)
    {
        Requiere();
        var (codigo, _, host) = await Get(ruta + VentanaA, f.CookieEditor, "editor");
        Assert.Equal(HttpStatusCode.Forbidden, codigo);
        Assert.Empty(DeM03(host));
    }

    [Fact]
    public async Task R8A_Cookie_no_emitida_y_sesion_revocada_reciben_401_sin_sentencias_de_M03()
    {
        Requiere();
        foreach (var (quien, cookie) in new[] { ("cookie que CORE no emitió", Convert.ToBase64String(Guid.NewGuid().ToByteArray())), ("sesión revocada", f.CookieRevocada) })
        {
            var (codigo, _, host) = await Get("/api/admin/reporting/orders" + VentanaA, cookie, quien);
            Assert.Equal(HttpStatusCode.Unauthorized, codigo);
            Assert.Empty(DeM03(host));
        }
    }

    // -----------------------------------------------------------------
    // I01, I03, I04 contra las filas físicas
    // -----------------------------------------------------------------

    /// <summary>Lo esperado, calculado por SQL independiente sobre las filas insertadas.</summary>
    private async Task<(long I01, Dictionary<string, long> I03, long I04, long Activos, long Inactivos)> Contraste(string inicio, string fin)
    {
        var porEstado = OrderStatus.All.ToDictionary(e => e, _ => 0L);
        long activos = 0, inactivos = 0;
        await using var c = new NpgsqlConnection(f.Cadena);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand("select status, is_active, count(*) from sales.orders where created_at >= @i and created_at < @f group by 1, 2", c);
        cmd.Parameters.AddWithValue("i", DateTimeOffset.Parse(inicio, System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime());
        cmd.Parameters.AddWithValue("f", DateTimeOffset.Parse(fin, System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            porEstado[r.GetString(0)] += r.GetInt64(2);
            if (r.GetBoolean(1)) activos += r.GetInt64(2); else inactivos += r.GetInt64(2);
        }

        return (porEstado.Values.Sum(), porEstado, porEstado[OrderStatus.Expired], activos, inactivos);
    }

    private static void AfirmarCuerpo(JsonElement cuerpo, (long I01, Dictionary<string, long> I03, long I04, long Activos, long Inactivos) esperado)
    {
        Assert.Equal(esperado.I01 == 0 ? "empty" : "with_data", cuerpo.GetProperty("status").GetString());
        Assert.Equal(esperado.I01, cuerpo.GetProperty("i01CreatedOrders").GetInt64());
        Assert.Equal(OrderStatus.All.Select(e => (e, esperado.I03[e])),
            cuerpo.GetProperty("i03CurrentStatusCounts").EnumerateArray().Select(x => (x.GetProperty("status").GetString()!, x.GetProperty("count").GetInt64())));
        Assert.Equal(esperado.I04, cuerpo.GetProperty("i04CurrentlyExpiredOrders").GetInt64());
        Assert.Equal((esperado.Activos, esperado.Inactivos), (cuerpo.GetProperty("activeOrders").GetInt64(), cuerpo.GetProperty("inactiveOrders").GetInt64()));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("super_admin")]
    public async Task R8A_Admin_y_super_admin_leen_I01_I03_I04_de_los_pedidos_fisicos_con_una_sola_consulta_de_lectura(string rol)
    {
        Requiere();
        var cookie = rol == "admin" ? f.CookieAdmin : f.CookieSuper;
        var (codigo, texto, host) = await Get("/api/admin/reporting/orders" + VentanaA, cookie, rol);

        Assert.Equal(HttpStatusCode.OK, codigo);
        var cuerpo = JsonDocument.Parse(texto).RootElement;
        var esperado = await Contraste("2026-10-01T00:00:00Z", "2026-10-02T00:00:00Z");
        // Escrito a mano desde InstalacionFisicaR8.Pedidos: seis en la ventana, dos de baja, dos vencidos (uno de baja).
        Assert.Equal((6L, 2L, 4L, 2L), (esperado.I01, esperado.I04, esperado.Activos, esperado.Inactivos));
        AfirmarCuerpo(cuerpo, esperado);

        var m03 = Assert.Single(DeM03(host), s => s.StartsWith("LOG:", StringComparison.Ordinal));
        var sql = m03[(m03.IndexOf(": ", "LOG:  ".Length, StringComparison.Ordinal) + 2)..].Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(SqlEsperado.I01I03I04.Replace("@startUtc", "$1", StringComparison.Ordinal).Replace("@endUtc", "$2", StringComparison.Ordinal), sql);
        Assert.Contains(host, s => s.StartsWith("DETAIL:  parameters: $1 = '2026-10-01 00:00:00+00', $2 = '2026-10-02 00:00:00+00'", StringComparison.Ordinal));
    }

    public static TheoryData<string, string, string> Periodos => new()
    {
        { "ventana B: el pedido del instante final de A", "2026-10-02T00:00:00Z", "2026-10-03T00:00:00Z" },
        { "ventana vacía", "2026-11-01T00:00:00Z", "2026-11-02T00:00:00Z" },
        { "ventana amplia: los nueve", "2026-09-01T00:00:00Z", "2026-11-01T00:00:00Z" },
        { "un milisegundo: solo el del instante inicial", "2026-10-01T00:00:00Z", "2026-10-01T00:00:00.001Z" },
        { "zona -05:00: el día local de Lima", "2026-10-01T00:00:00-05:00", "2026-10-02T00:00:00-05:00" },
    };

    [Theory]
    [MemberData(nameof(Periodos))]
    public async Task R8A_Los_filtros_de_periodo_coinciden_con_las_filas_fisicas(string caso, string inicio, string fin)
    {
        Requiere();
        var (codigo, texto, host) = await Get("/api/admin/reporting/orders" + Ventana(Uri.EscapeDataString(inicio), Uri.EscapeDataString(fin)), f.CookieAdmin, $"admin · {caso}");
        Assert.Equal(HttpStatusCode.OK, codigo);
        var esperado = await Contraste(inicio, fin);
        AfirmarCuerpo(JsonDocument.Parse(texto).RootElement, esperado);
        Assert.Single(DeM03(host), s => s.StartsWith("LOG:", StringComparison.Ordinal));

        // Escrito a mano desde InstalacionFisicaR8.Pedidos.
        var aMano = caso switch
        {
            "ventana B: el pedido del instante final de A" => 1L, // R8A-0007
            "ventana vacía" => 0L,
            "ventana amplia: los nueve" => 9L,
            "un milisegundo: solo el del instante inicial" => 1L, // R8A-0001
            _ => 6L, // [2026-10-01T05:00Z, 2026-10-02T05:00Z): R8A-0002 a R8A-0007
        };

        Assert.Equal(aMano, esperado.I01);
        Anotar("r8a-contraste-periodos.txt", $"{caso} · [{inicio}, {fin}) → I01={esperado.I01} I04={esperado.I04} activos={esperado.Activos} inactivos={esperado.Inactivos} I03={string.Join(",", esperado.I03.Where(x => x.Value > 0).Select(x => $"{x.Key}:{x.Value}"))}");
    }

    // -----------------------------------------------------------------
    // Indicadores monetarios: no se publica ningún importe
    // -----------------------------------------------------------------

    [Fact]
    public async Task R8A_I02_e_I05_I07_siguen_no_medibles_y_no_publican_los_totales_fisicos()
    {
        Requiere();
        var (codigoValor, valor, hostValor) = await Get("/api/admin/reporting/orders/value" + VentanaA, f.CookieAdmin, "admin");
        Assert.Equal(HttpStatusCode.OK, codigoValor);
        var v = JsonDocument.Parse(valor).RootElement;
        Assert.Equal("not_measurable", v.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, v.GetProperty("byCurrency").ValueKind);
        Assert.Empty(DeM03(hostValor));

        var (codigoPagos, pagos, hostPagos) = await Get("/api/admin/reporting/payments" + VentanaA, f.CookieAdmin, "admin");
        Assert.Equal(HttpStatusCode.OK, codigoPagos);
        var p = JsonDocument.Parse(pagos).RootElement;
        foreach (var i in new[] { "i05", "i06", "i07" })
        {
            Assert.Equal("not_measurable", p.GetProperty(i).GetProperty("status").GetString());
        }

        Assert.Equal(JsonValueKind.Null, p.GetProperty("i06").GetProperty("byCurrency").ValueKind);
        Assert.Equal("writer_not_accredited", p.GetProperty("notMeasurableReason").GetString());
        Assert.All(DeM03(hostPagos).Where(s => s.StartsWith("LOG:", StringComparison.Ordinal)),
            s => Assert.Contains("FROM sales.order_payments", s, StringComparison.Ordinal));

        // Ningún total físico de los pedidos aparece en las respuestas.
        foreach (var total in InstalacionFisicaR8.Pedidos.Select(x => x.Total))
        {
            var t = total.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture);
            Assert.DoesNotContain(t, valor, StringComparison.Ordinal);
            Assert.DoesNotContain(t, pagos, StringComparison.Ordinal);
        }
    }

    // -----------------------------------------------------------------
    // Solo lectura, composición y reinicio
    // -----------------------------------------------------------------

    [Fact]
    public async Task R8A_Reporting_solo_lee_las_filas_siguen_intactas_y_el_Host_no_escribe_en_sales()
    {
        Requiere();
        var host = await f.SentenciasDelHost(async () =>
        {
            foreach (var ruta in Rutas)
            {
                using var r = await InstalacionRealR7.Pedir(f.Http, HttpMethod.Get, ruta + VentanaA, f.CookieAdmin);
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            }
        });
        var m03 = DeM03(host).Where(s => s.StartsWith("LOG:", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(m03);
        Assert.All(m03, s => Assert.Matches(@"^LOG:  (statement|execute [^:]*): SELECT ", s));
        Assert.DoesNotContain(host, s => Regex.IsMatch(s, @"\b(INSERT|UPDATE|DELETE|TRUNCATE|ALTER|CREATE|DROP)\b[^;]*\bsales\.", RegexOptions.IgnoreCase));
        var despues = await f.Huella();
        Assert.Equal(f.HuellaDePedidos, despues);
        Anotar("r8a-solo-lectura.txt", [$"sentencias_M03_en_tres_rutas={m03.Count} (todas SELECT)", "huella antes == después:", .. despues]);
    }

    [Fact]
    public async Task R8A_El_Host_usa_el_SalesDbContext_real_contra_esta_base_sin_interceptores_y_la_composicion_sigue_cerrada()
    {
        Requiere();
        await using var scope = f.Host!.App.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var ventas = sp.GetRequiredService<SalesDbContext>();
        var cadena = new NpgsqlConnectionStringBuilder(ventas.Database.GetConnectionString());
        Assert.Equal((f.Base, InstalacionFisicaR8.AppHost, "127.0.0.1"), (cadena.Database, cadena.ApplicationName, cadena.Host));
        var extension = ventas.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>();
        Assert.True(extension?.Interceptors is null || !extension.Interceptors.Any());

        Assert.IsType<Sillar.Modules.Payments.Application.M03ReadOnlyNegativeEffectPortAdapter>(
            Assert.Single(sp.GetServices<Sillar.Modules.Payments.Application.Contracts.IProviderCommercialEffectPort>()));
        Assert.IsType<Sillar.Modules.Payments.Application.Contracts.HoldIndeterminateCommercialRetryPolicy>(
            sp.GetRequiredService<Sillar.Modules.Payments.Application.Contracts.IProviderCommercialRetryPolicy>());
        Assert.Null(sp.GetService<IOrderValueAggregateReader>());
        Assert.Null(sp.GetService<IExternalSettlementCandidate>());
        Assert.Equal(["core", "reporting"], f.Host.ActiveModules);
        Anotar("r8a-composicion.txt",
            $"sales_db={cadena.Database} app={cadena.ApplicationName} host={cadena.Host}:{cadena.Port} interceptores={extension?.Interceptors?.Count() ?? 0}",
            "puerto_efecto=M03ReadOnlyNegativeEffectPortAdapter politica=HoldIndeterminateCommercialRetryPolicy IOrderValueAggregateReader=ausente IExternalSettlementCandidate=ausente",
            $"activos=[{string.Join(",", f.Host.ActiveModules)}]");
    }

    [Fact]
    public async Task R8A_Tras_reiniciar_el_Host_la_misma_sesion_lee_los_mismos_pedidos_fisicos()
    {
        Requiere();
        var otro = await NegativeCommerceAuthenticatedHost.BuildAsync(f.Configuracion(), Environments.Development, null, CancellationToken.None);
        await using (otro.App)
        {
            await otro.App.StartAsync(TestContext.Current.CancellationToken);
            using var http = InstalacionRealR7.Cliente(otro.App);
            using var r = await InstalacionRealR7.Pedir(http, HttpMethod.Get, "/api/admin/reporting/orders" + VentanaA, f.CookieAdmin);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            AfirmarCuerpo(JsonDocument.Parse(await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement, await Contraste("2026-10-01T00:00:00Z", "2026-10-02T00:00:00Z"));
            using var e = await InstalacionRealR7.Pedir(http, HttpMethod.Get, "/api/admin/reporting/orders" + VentanaA, f.CookieEditor);
            Assert.Equal(HttpStatusCode.Forbidden, e.StatusCode);
            Anotar("r8a-reinicio.txt", $"host nuevo {string.Join(",", InstalacionRealR7.Direcciones(otro.App))} · admin (cookie previa) → 200 con I01=6 · editor → 403");
            await otro.App.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
