using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sillar.Core.Contracts;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;
using Sillar.Modules.Payments.Tests;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using static Sillar.Host.CommerceBridge.Tests.ComposicionVerticalTests;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>Sabotaje: un resolvedor que se haría pasar por CORE. Nunca llega a ejecutarse.</summary>
internal sealed class ZonaImpostora : IInstallationTimeZoneResolver
{
    public Task<InstallationTimeZoneResolution> ResolveAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("nunca");
}

/// <summary>Sabotaje: una auditoría que se haría pasar por CORE. Nunca llega a ejecutarse.</summary>
internal sealed class AuditoriaImpostora : IExportAudit
{
    public Task<ExportAuditResult> RecordAsync(ExportAuditRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("nunca");
}

/// <summary>
/// P1-POST-G6-R9 (Claude A). Sobre la instalación física R8A (CORE real, Catalog, CRM y Sales
/// migrados, Host sin sustituciones, barrera R8-01):
/// <list type="bullet">
/// <item>A: las dos rutas de Reporting que solo estaban montadas, <c>/installation-zone</c> y
/// <c>/orders/export-audit</c>, ejecutadas por HTTP con los productores candidatos de CORE.</item>
/// <item>B: el recorrido negativo M03 → M11 sobre los pedidos físicos, y el inbox hasta donde
/// llegan sus productores.</item>
/// </list>
/// </summary>
public sealed class R9IntegracionTests(InstalacionFisicaR8 f) : IClassFixture<InstalacionFisicaR8>
{
    private const string RutaZona = "/api/admin/reporting/installation-zone";
    private const string RutaAuditoria = "/api/admin/reporting/orders/export-audit";
    private static readonly Dictionary<string, List<string>> Notas = [];
    private static readonly Regex Ventas = new(@"\bsales\.", RegexOptions.CultureInvariant);
    private static readonly Regex Auditoria = new(@"\baudit_log\b", RegexOptions.CultureInvariant);

    private void Requiere()
    {
        if (!f.Disponible)
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay instancia PostgreSQL desechable para R9.");
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
            RegistrarEvidencia(nameof(R9IntegracionTests), f.Fixture, f.Base, propio);
        }
    }

    /// <summary>
    /// R10 (O-3): todo lo que se intentó —exitoso o fallido—, para que ninguna afirmación pase por
    /// alto una sentencia rechazada. La separación queda en <see cref="ClasificadorDeRegistroPg"/>.
    /// </summary>
    private static List<string> Sentencias(SentenciasClasificadas c) => [.. c.Intentadas];

    private static bool EsMutante(string sql) => ClasificadorDeRegistroPg.EsMutante(sql);

    private async Task<(HttpStatusCode Codigo, string Cuerpo, List<string> Sql)> Llamar(
        HttpMethod metodo, string ruta, string? cookie, string? csrf, object? cuerpo, string quien)
    {
        HttpStatusCode codigo = 0;
        var texto = "";
        var entradas = await f.SentenciasDelHost(async () =>
        {
            using var r = await InstalacionRealR7.Pedir(f.Http, metodo, ruta, cookie, csrf, cuerpo);
            codigo = r.StatusCode;
            texto = await r.Content.ReadAsStringAsync();
        });
        var c = ClasificadorDeRegistroPg.Clasificar(entradas);
        var sql = Sentencias(c);
        Anotar("r9-rutas-http.txt",
            $"{quien} · {metodo} {ruta}{(cuerpo is null ? "" : " " + JsonSerializer.Serialize(cuerpo))} → {(int)codigo} · {c.Resumen} · M03={sql.Count(s => Ventas.IsMatch(s))} · audit_log={sql.Count(s => Auditoria.IsMatch(s))}");
        Anotar("r9-sentencias-fallidas.txt", [$"--- {quien} · {metodo} {ruta} → {(int)codigo} · fallidas={c.Fallidas.Count}", .. c.Fallidas.Zip(c.Errores, (f, e) => $"{e} ⇐ {f.Replace("\n", " ", StringComparison.Ordinal)}")]);
        // Ningún flujo de R9 envía sentencias que PostgreSQL rechace: si aparece una, se ve y es rojo.
        Assert.Empty(c.Fallidas);
        Anotar("r9-sentencias-http.txt", [$"--- {quien} · {metodo} {ruta} → {(int)codigo}", .. sql.Select(s => s.Replace("\n", " ", StringComparison.Ordinal))]);
        Anotar("r9-respuestas-http.txt", $"{quien} · {metodo} {ruta} → {(int)codigo}", texto);
        return (codigo, texto, sql);
    }

    private async Task<Dictionary<string, long>> AuditoriaPorAccion()
    {
        var filas = await InstalacionRealR7.Consultar(f.Cadena,
            "select coalesce(string_agg(action || '=' || n, ',' order by action), '') from (select action, count(*) n from core.audit_log group by action) x");
        return filas[0].Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('='))
            .ToDictionary(p => p[0], p => long.Parse(p[1]), StringComparer.Ordinal);
    }

    // -----------------------------------------------------------------
    // A · composición: los productores de CORE, y solo ellos
    // -----------------------------------------------------------------

    [Fact]
    public void R9_A_El_Host_resuelve_la_zona_y_la_auditoria_con_los_productores_candidatos_de_CORE()
    {
        Requiere();
        using var ambito = f.Host!.App.Services.CreateScope();
        var sp = ambito.ServiceProvider;
        var zona = Assert.Single(sp.GetServices<IInstallationTimeZoneResolver>());
        var auditoria = Assert.Single(sp.GetServices<IExportAudit>());
        var tzdb = f.Host.App.Services.GetServices(Tipo("Sillar.Core.Settings.ITzdbSource")).ToArray();
        var atestaciones = f.Host.App.Services.GetServices(Tipo("Sillar.Core.Settings.ITimeZoneAttestationStore")).ToArray();

        var lineas = new[]
        {
            $"IInstallationTimeZoneResolver → {zona.GetType().FullName}",
            $"IExportAudit → {auditoria.GetType().FullName}",
            $"ITzdbSource → {string.Join(", ", tzdb.Select(t => t!.GetType().FullName))}",
            $"ITimeZoneAttestationStore → {string.Join(", ", atestaciones.Select(t => t!.GetType().FullName))}",
            $"ensamblado de los cuatro = {string.Join(", ", new[] { zona, auditoria, tzdb[0]!, atestaciones[0]! }.Select(o => o.GetType().Assembly.GetName().Name).Distinct())}",
        };
        Anotar("r9-composicion-core.txt", lineas);

        Assert.Equal("Sillar.Core.Settings.InstallationTimeZoneResolver", zona.GetType().FullName);
        Assert.Equal("Sillar.Core.Auditing.ExportAudit", auditoria.GetType().FullName);
        Assert.Equal("Sillar.Core.Settings.UnavailableTzdbSource", Assert.Single(tzdb)!.GetType().FullName);
        Assert.Equal("Sillar.Core.Settings.NoTimeZoneAttestationStore", Assert.Single(atestaciones)!.GetType().FullName);
        Assert.All(new[] { zona, auditoria, tzdb[0]!, atestaciones[0]! }, o => Assert.Equal("Sillar.Core", o.GetType().Assembly.GetName().Name));
    }

    private static Type Tipo(string nombre) => typeof(Sillar.Core.CoreModule).Assembly.GetType(nombre, throwOnError: true)!;

    public static TheoryData<string> Sabotajes => new(
        "sustituir la zona por una impostora", "quitar la zona", "sustituir la auditoría por una impostora",
        "quitar la auditoría", "sustituir la fuente de zonas", "sustituir el almacén de atestaciones");

    [Theory]
    [MemberData(nameof(Sabotajes))]
    public async Task R9_A_La_sustitucion_de_infraestructura_no_puede_tocar_la_zona_ni_la_auditoria_de_CORE(string sabotaje)
    {
        Requiere();
        static void Quitar(IServiceCollection s, string nombre)
        {
            foreach (var d in s.Where(d => d.ServiceType.FullName == nombre).ToArray()) s.Remove(d);
        }

        Action<IServiceCollection> alterar = sabotaje switch
        {
            "sustituir la zona por una impostora" => s => { s.RemoveAll<IInstallationTimeZoneResolver>(); s.AddScoped<IInstallationTimeZoneResolver, ZonaImpostora>(); },
            "quitar la zona" => s => s.RemoveAll<IInstallationTimeZoneResolver>(),
            "sustituir la auditoría por una impostora" => s => { s.RemoveAll<IExportAudit>(); s.AddScoped<IExportAudit, AuditoriaImpostora>(); },
            "quitar la auditoría" => s => s.RemoveAll<IExportAudit>(),
            "sustituir la fuente de zonas" => s => { Quitar(s, "Sillar.Core.Settings.ITzdbSource"); s.AddSingleton(Tipo("Sillar.Core.Settings.ITzdbSource"), Tipo("Sillar.Core.Settings.UnavailableTzdbSource")); },
            "sustituir el almacén de atestaciones" => s => { Quitar(s, "Sillar.Core.Settings.ITimeZoneAttestationStore"); s.AddSingleton(Tipo("Sillar.Core.Settings.ITimeZoneAttestationStore"), Tipo("Sillar.Core.Settings.NoTimeZoneAttestationStore")); },
            _ => throw new ArgumentOutOfRangeException(nameof(sabotaje)),
        };

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => NegativeCommerceAuthenticatedHost.BuildAsync(
            f.Configuracion(), Environments.Development, alterar, TestContext.Current.CancellationToken));
        Assert.Contains("servicios críticos de CORE", e.Message, StringComparison.Ordinal);
        Anotar("r9-sabotajes-core.txt", $"{sabotaje} → rechazado: {e.Message}");
    }

    // -----------------------------------------------------------------
    // A1 · GET /installation-zone
    // -----------------------------------------------------------------

    [Fact]
    public async Task R9_A1_La_zona_sale_de_CORE_como_inicial_aprobada_sin_verificar_y_sin_valores_inventados()
    {
        Requiere();
        // La fuente legítima, leída aparte: ninguna fila de la clave y ninguna atestación.
        var fuente = await InstalacionRealR7.Consultar(f.Cadena,
            $"select count(*) from core.site_settings where setting_key = '{IanaZoneCandidate.SettingKey}'",
            "select count(*) from core.installation",
            "select coalesce(string_agg(table_name, ','), '(ninguna)') from information_schema.tables where table_schema = 'core' and table_name like '%attest%'");
        Anotar("r9-zona-fuente.txt",
            $"filas de {IanaZoneCandidate.SettingKey} en core.site_settings = {fuente[0]}",
            $"filas en core.installation = {fuente[1]}",
            $"tablas de atestación en core = {fuente[2]}");
        Assert.Equal("0", fuente[0]);
        Assert.Equal("1", fuente[1]);

        foreach (var (quien, cookie) in new[] { ("admin", f.CookieAdmin), ("super_admin", f.CookieSuper) })
        {
            var (codigo, cuerpo, sql) = await Llamar(HttpMethod.Get, RutaZona, cookie, null, null, quien);
            Assert.Equal(HttpStatusCode.OK, codigo);
            var j = JsonDocument.Parse(cuerpo).RootElement;
            // CoreSeamEndpoints.Observe: inicial aprobada → solo «zone», el resto nulo.
            Assert.Equal("approved_initial", j.GetProperty("kind").GetString());
            Assert.Equal(IInstallationTimeZoneResolver.InitialZoneId, j.GetProperty("zone").GetString());
            Assert.Equal(JsonValueKind.Null, j.GetProperty("original").ValueKind);
            Assert.Equal(JsonValueKind.Null, j.GetProperty("canonical").ValueKind);
            Assert.Equal(JsonValueKind.Null, j.GetProperty("runtimeId").ValueKind);
            Assert.Equal(5, j.EnumerateObject().Count());
            Assert.DoesNotContain(sql, s => Ventas.IsMatch(s));
            Assert.DoesNotContain(sql, EsMutante);
        }
    }

    public static TheoryData<string> Rechazos => new("anónimo", "sesión revocada", "editor");

    [Theory]
    [MemberData(nameof(Rechazos))]
    public async Task R9_A1_Sin_autorizacion_la_zona_se_rechaza_antes_de_leer_CORE_ni_M03(string quien)
    {
        Requiere();
        var (cookie, esperado) = quien switch
        {
            "anónimo" => ((string?)null, HttpStatusCode.Unauthorized),
            "sesión revocada" => (f.CookieRevocada, HttpStatusCode.Unauthorized),
            "editor" => (f.CookieEditor, HttpStatusCode.Forbidden),
            _ => throw new ArgumentOutOfRangeException(nameof(quien)),
        };

        var (codigo, _, sql) = await Llamar(HttpMethod.Get, RutaZona, cookie, null, null, quien);
        Assert.Equal(esperado, codigo);
        // Lo único que se lee es la sesión: ni configuración, ni instalación, ni M03.
        Assert.All(sql, s => Assert.Contains("admin_sessions", s, StringComparison.Ordinal));
        Assert.DoesNotContain(sql, s => s.Contains("site_settings", StringComparison.Ordinal) || s.Contains("core.installation", StringComparison.Ordinal));
        Assert.DoesNotContain(sql, s => Ventas.IsMatch(s));
        Assert.DoesNotContain(sql, EsMutante);
        if (cookie is null) Assert.Empty(sql);
    }

    [Fact]
    public async Task R9_A1_El_API_de_CORE_no_crea_la_clave_de_zona_y_la_zona_sigue_sin_verificar()
    {
        Requiere();
        var (cookie, csrf) = await InstalacionRealR7.Entrar(f.Http, "admin@r8.ejemplo.test", InstalacionFisicaR8.ClaveAdmin);
        var antes = await AuditoriaPorAccion();
        var (codigo, _, sql) = await Llamar(HttpMethod.Put, $"/api/admin/settings/{IanaZoneCandidate.SettingKey}", cookie, csrf,
            new { value = "America/Lima" }, "admin (PUT ajuste de zona)");
        // SettingsEndpoints: «Las claves no se crean ni se borran desde el API: una clave desconocida responde 404».
        Assert.Equal(HttpStatusCode.NotFound, codigo);
        Assert.DoesNotContain(sql, s => Ventas.IsMatch(s));
        Assert.DoesNotContain(sql, s => s.Contains("site_settings", StringComparison.Ordinal) && EsMutante(s));
        Assert.Equal("0", (await InstalacionRealR7.Consultar(f.Cadena, $"select count(*) from core.site_settings where setting_key = '{IanaZoneCandidate.SettingKey}'"))[0]);
        Assert.Equal(antes, await AuditoriaPorAccion());

        var (despues, cuerpo, _) = await Llamar(HttpMethod.Get, RutaZona, cookie, null, null, "admin (tras el PUT)");
        Assert.Equal(HttpStatusCode.OK, despues);
        Assert.Equal("approved_initial", JsonDocument.Parse(cuerpo).RootElement.GetProperty("kind").GetString());
    }

    // -----------------------------------------------------------------
    // A2 · POST /orders/export-audit
    // -----------------------------------------------------------------

    public static TheoryData<string> RechazosAuditoria => new(
        "anónimo", "sesión revocada", "editor con su CSRF", "admin sin CSRF", "admin con CSRF ajeno", "admin con CSRF alterado");

    [Theory]
    [MemberData(nameof(RechazosAuditoria))]
    public async Task R9_A2_La_auditoria_rechaza_sin_autorizacion_o_sin_CSRF_y_no_escribe_nada(string quien)
    {
        Requiere();
        var (admin, csrfAdmin) = await InstalacionRealR7.Entrar(f.Http, "admin@r8.ejemplo.test", InstalacionFisicaR8.ClaveAdmin);
        var (editor, csrfEditor) = await InstalacionRealR7.Entrar(f.Http, "editor@r8.ejemplo.test", InstalacionFisicaR8.ClaveEditor);
        var (_, csrfSuper) = await InstalacionRealR7.Entrar(f.Http, "super@r8.ejemplo.test", InstalacionFisicaR8.ClaveSuper);
        var (cookie, csrf, esperado, titulo) = quien switch
        {
            "anónimo" => ((string?)null, (string?)null, HttpStatusCode.Unauthorized, (string?)null),
            "sesión revocada" => (f.CookieRevocada, null, HttpStatusCode.Unauthorized, null),
            "editor con su CSRF" => (editor, csrfEditor, HttpStatusCode.Forbidden, null),
            "admin sin CSRF" => (admin, null, HttpStatusCode.Forbidden, "Falta el token CSRF o no es válido."),
            "admin con CSRF ajeno" => (admin, csrfSuper, HttpStatusCode.Forbidden, "Falta el token CSRF o no es válido."),
            "admin con CSRF alterado" => (admin, csrfAdmin[..^2] + (csrfAdmin[^2..] == "AA" ? "BB" : "AA"), HttpStatusCode.Forbidden, "Falta el token CSRF o no es válido."),
            _ => throw new ArgumentOutOfRangeException(nameof(quien)),
        };

        var antes = await AuditoriaPorAccion();
        var (codigo, cuerpo, sql) = await Llamar(HttpMethod.Post, RutaAuditoria, cookie, csrf, new { zone = "UTC", dataRows = 9 }, quien);
        Assert.Equal(esperado, codigo);
        if (titulo is not null) Assert.Equal(titulo, JsonDocument.Parse(cuerpo).RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain(sql, s => Ventas.IsMatch(s));
        Assert.DoesNotContain(sql, s => Auditoria.IsMatch(s));
        Assert.DoesNotContain(sql, s => s.Contains("site_settings", StringComparison.Ordinal) || s.Contains("core.installation", StringComparison.Ordinal));
        Assert.DoesNotContain(sql, EsMutante);
        Assert.Equal(antes, await AuditoriaPorAccion());
        Assert.False((await AuditoriaPorAccion()).ContainsKey("export"));
    }

    [Fact]
    public async Task R9_A2_Con_sesion_y_CSRF_la_ruta_original_valida_el_cuerpo_y_sin_zona_verificada_no_audita()
    {
        Requiere();
        var antes = await AuditoriaPorAccion();
        var huella = await f.Huella();
        var resultados = new List<string>();

        foreach (var (correo, clave, quien) in new[]
                 {
                     ("admin@r8.ejemplo.test", InstalacionFisicaR8.ClaveAdmin, "admin"),
                     ("super@r8.ejemplo.test", InstalacionFisicaR8.ClaveSuper, "super_admin"),
                 })
        {
            var (cookie, csrf) = await InstalacionRealR7.Entrar(f.Http, correo, clave);
            var trasEntrar = await AuditoriaPorAccion();

            // 400: la propia ruta M10 valida el cuerpo antes de leer la zona.
            foreach (var malo in new object[] { new { zone = "UTC" }, new { dataRows = 9 }, new { zone = "", dataRows = 9 }, new { zone = "UTC", dataRows = -1 } })
            {
                var (c400, cuerpo400, sql400) = await Llamar(HttpMethod.Post, RutaAuditoria, cookie, csrf, malo, quien);
                Assert.Equal(HttpStatusCode.BadRequest, c400);
                Assert.Equal("Indica la zona del archivo y cuántas filas de datos lleva.",
                    JsonDocument.Parse(cuerpo400).RootElement.GetProperty("errors").GetProperty("exportacion")[0].GetString());
                Assert.DoesNotContain(sql400, s => Auditoria.IsMatch(s) || Ventas.IsMatch(s) || EsMutante(s));
                resultados.Add($"{quien} · {JsonSerializer.Serialize(malo)} → 400");
            }

            // 409: con un cuerpo válido, la zona de CORE no está verificada y no se audita.
            foreach (var zona in new[] { "UTC", IInstallationTimeZoneResolver.InitialZoneId, "Europe/Madrid" })
            {
                var (c409, cuerpo409, sql409) = await Llamar(HttpMethod.Post, RutaAuditoria, cookie, csrf, new { zone = zona, dataRows = 9 }, quien);
                Assert.Equal(HttpStatusCode.Conflict, c409);
                Assert.Equal("Para exportar, la zona horaria de la instalación tiene que estar verificada, y todavía no lo está.",
                    JsonDocument.Parse(cuerpo409).RootElement.GetProperty("title").GetString());
                Assert.DoesNotContain(sql409, s => Auditoria.IsMatch(s) || Ventas.IsMatch(s) || EsMutante(s));
                resultados.Add($"{quien} · zona={zona} dataRows=9 → 409 zona no verificada");
            }

            Assert.Equal(trasEntrar, await AuditoriaPorAccion());
        }

        var despues = await AuditoriaPorAccion();
        Assert.False(despues.ContainsKey("export"));
        // Lo único nuevo en la auditoría de CORE son los dos inicios de sesión de esta prueba.
        Assert.Equal(antes.GetValueOrDefault("login") + 2, despues.GetValueOrDefault("login"));
        Assert.Equal(antes.Where(p => p.Key != "login"), despues.Where(p => p.Key != "login"));
        Assert.Equal(huella, await f.Huella());
        Anotar("r9-auditoria-export.txt",
            [.. resultados,
             $"core.audit_log por acción antes={string.Join(",", antes.Select(p => $"{p.Key}={p.Value}"))}",
             $"core.audit_log por acción después={string.Join(",", despues.Select(p => $"{p.Key}={p.Value}"))}",
             "filas con action=export: 0",
             "pedidos (xmin, row_version, updated_at): sin cambios"]);
    }

    [Fact]
    public async Task R9_A2_La_auditoria_real_de_CORE_persiste_los_inicios_de_sesion_en_PostgreSQL()
    {
        Requiere();
        var antes = await AuditoriaPorAccion();
        await InstalacionRealR7.Entrar(f.Http, "admin@r8.ejemplo.test", InstalacionFisicaR8.ClaveAdmin);
        var despues = await AuditoriaPorAccion();
        var ultima = await InstalacionRealR7.Consultar(f.Cadena,
            "select action || '|' || coalesce(admin_user_email, '') || '|' || coalesce(module_code, '') from core.audit_log order by audit_log_id desc limit 1");
        Anotar("r9-auditoria-core.txt",
            $"antes={string.Join(",", antes.Select(p => $"{p.Key}={p.Value}"))}",
            $"después={string.Join(",", despues.Select(p => $"{p.Key}={p.Value}"))}",
            $"última fila={ultima[0]}");
        Assert.True(despues.GetValueOrDefault("login") >= antes.GetValueOrDefault("login") + 1);
        Assert.StartsWith("login|admin@r8.ejemplo.test|", ultima[0], StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------
    // B · M03 → M11 sobre los pedidos físicos
    // -----------------------------------------------------------------

    private async Task<Guid> IdDe(string codigo)
        => Guid.Parse((await InstalacionRealR7.Consultar(f.Cadena, $"select order_id::text from sales.orders where order_code = '{codigo}'"))[0]);

    /// <summary>Código con la forma ratificada (OrderCode.cs:137-162); los de R8A («R8A-0001») no la tienen.</summary>
    private const string CodigoR9 = "R9-2026-0001";

    /// <summary>
    /// PREPARACIÓN DE DATOS DE PRUEBA, como la de R8A: un pedido con código de forma ratificada para
    /// recorrer la correlación por código, que es la auténtica. Pendiente, activo y sin moneda.
    /// </summary>
    private async Task AsegurarPedidoR9()
    {
        await using var c = new Npgsql.NpgsqlConnection(f.Cadena);
        await c.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new Npgsql.NpgsqlCommand(
            "/* R9 · preparación de datos de prueba, no creación comercial */ "
            + "INSERT INTO sales.orders (order_id, order_code, customer_id, customer_full_name, customer_email, status, payment_due_at, total_amount, is_active, origin_node, created_at) "
            + "SELECT @id, @codigo, c.customer_id, c.full_name, c.email, @estado, @creado + interval '2 days', 12.34, true, 'r8a', @creado FROM crm.customers c WHERE c.customer_id = @cliente "
            + "ON CONFLICT (order_code) DO NOTHING", c);
        cmd.Parameters.AddWithValue("id", Guid.CreateVersion7());
        cmd.Parameters.AddWithValue("codigo", CodigoR9);
        cmd.Parameters.AddWithValue("estado", OrderStatus.PendingPayment);
        cmd.Parameters.AddWithValue("creado", DateTimeOffset.Parse("2026-11-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("cliente", f.Clientes[0]);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public static TheoryData<string> CasosM03M11 => new(
        "R8A-0002 pendiente y activo, por id", "R8A-0003 en preparación, por id", "R8A-0004 vencido y activo, por id",
        "R8A-0006 entregado y activo, por id", "R8A-0001 cancelado y de baja, por id", "R8A-0005 vencido y de baja, por id",
        "R8A-0002 por su código de prueba, sin forma ratificada", "id que no existe",
        "R9-2026-0001 por código", "R9-2026-0001 por código e id coherentes", "R9-2026-0001 con el id de R8A-0003",
        "r9-2026-0001 en minúsculas", "R9-2026-0099 bien formado y desconocido");

    [Theory]
    [MemberData(nameof(CasosM03M11))]
    public async Task R9_B_El_puerto_M11_evalua_pedidos_fisicos_de_M03_y_solo_da_negativos(string caso)
    {
        Requiere();
        await AsegurarPedidoR9();
        // Esperados escritos a mano: OrderCorrelationPolicy.cs:83-149 (forma, desconocido, divergente),
        // ExternalCommercePolicy.cs:99-126 (pagabilidad) y la tabla de M03ReadOnlyNegativeReason
        // (M03CommercialReadOnlyEvaluationContracts.cs:86-114). Lecturas: correlación, relectura y pagos.
        const M03CommercialReadOnlyEvaluationOutcome Dr = M03CommercialReadOnlyEvaluationOutcome.DecisionRequired, Rj = M03CommercialReadOnlyEvaluationOutcome.Rejected;
        var (peticion, desenlace, codigo, lecturas) = caso switch
        {
            "R8A-0002 pendiente y activo, por id" => (new M03OrderCorrelationRequestSnapshot(null, await IdDe("R8A-0002")), Dr, "decision_required/currency_authority_missing", 3),
            "R8A-0003 en preparación, por id" => (new(null, await IdDe("R8A-0003")), Dr, "decision_required/currency_authority_missing", 3),
            "R8A-0004 vencido y activo, por id" => (new(null, await IdDe("R8A-0004")), Dr, "decision_required/currency_authority_missing", 3),
            "R8A-0006 entregado y activo, por id" => (new(null, await IdDe("R8A-0006")), Dr, "decision_required/order_lifecycle_unratified", 3),
            "R8A-0001 cancelado y de baja, por id" => (new(null, await IdDe("R8A-0001")), Rj, "rejected/inactive_order", 3),
            "R8A-0005 vencido y de baja, por id" => (new(null, await IdDe("R8A-0005")), Rj, "rejected/inactive_order", 3),
            // Rechaza antes de leer lo que el formato ratificado no produce (OrderCorrelationPolicy.cs:83-88).
            "R8A-0002 por su código de prueba, sin forma ratificada" => (new("R8A-0002", null), Rj, "rejected/malformed_reference", 0),
            "id que no existe" => (new(null, Guid.Parse("01999f3a-0000-7000-8000-0000000009f9")), Rj, "rejected/unknown_reference", 1),
            "R9-2026-0001 por código" => (new(CodigoR9, null), Dr, "decision_required/currency_authority_missing", 3),
            "R9-2026-0001 por código e id coherentes" => (new(CodigoR9, await IdDe(CodigoR9)), Dr, "decision_required/currency_authority_missing", 3),
            "R9-2026-0001 con el id de R8A-0003" => (new(CodigoR9, await IdDe("R8A-0003")), Rj, "rejected/divergent_declaration", 1),
            // La columna distingue mayúsculas y la política no las pliega: la base compara byte a byte.
            "r9-2026-0001 en minúsculas" => (new("r9-2026-0001", null), Rj, "rejected/unknown_reference", 1),
            "R9-2026-0099 bien formado y desconocido" => (new("R9-2026-0099", null), Rj, "rejected/unknown_reference", 1),
            _ => throw new ArgumentOutOfRangeException(nameof(caso)),
        };

        var huella = await f.Huella();
        var resultados = new List<M03CommercialReadOnlyEvaluationResult>();
        var porVuelta = new List<List<string>>();
        // Dos vueltas: la segunda tiene que leer exactamente lo mismo; nada se reintenta por dentro.
        for (var vuelta = 0; vuelta < 2; vuelta++)
        {
            var entradas = await f.SentenciasDelHost(async () =>
            {
                using var ambito = f.Host!.App.Services.CreateScope();
                var puerto = ambito.ServiceProvider.GetRequiredService<IM03CommercialReadOnlyEvaluationPort>();
                resultados.Add(await puerto.EvaluateAsync(peticion, TestContext.Current.CancellationToken));
            });
            var c = ClasificadorDeRegistroPg.Clasificar(entradas);
            Assert.Empty(c.Fallidas);
            Assert.Empty(c.Mutantes);
            porVuelta.Add(Sentencias(c));
        }

        var r = resultados[0];
        Assert.Equal(desenlace, r.Outcome);
        Assert.Null(r.TechnicalReason);
        Assert.True(M03ReadOnlyNegativeReason.TryFormat(r, out var razon));
        Assert.Equal(M03ReadOnlyNegativeReason.Prefix + codigo, razon);
        Assert.Equal(resultados[0].Outcome, resultados[1].Outcome);
        Assert.Equal(resultados[0].SourceReason, resultados[1].SourceReason);

        var sql = porVuelta[0];
        Assert.Equal(lecturas, sql.Count);
        Assert.All(sql, s => Assert.StartsWith("SELECT", s.TrimStart(), StringComparison.Ordinal));
        Assert.All(sql, s => Assert.Matches(Ventas, s));
        Assert.DoesNotContain(sql, s => s.Contains("payments.", StringComparison.Ordinal));
        Assert.Equal(sql.Count, porVuelta[1].Count);
        Assert.Equal(huella, await f.Huella());

        Anotar("r9-m03-m11-fisico.txt",
            $"{caso} | {r.Outcome} {r.SourceVerdict}/{r.SourceReason} | código={razon} | lecturas_sales={sql.Count} (2.ª vuelta {porVuelta[1].Count}) | fallidas=0 | mutantes=0 | pedidos sin cambios");
        Anotar("r9-m03-m11-sentencias.txt", [$"--- {caso}", .. sql.Select(s => s.Replace("\n", " ", StringComparison.Ordinal))]);
    }

    [Fact]
    public async Task R9_B_La_composicion_fisica_conserva_las_barreras_de_correlacion_autoridad_y_recepcion()
    {
        Requiere();
        var raiz = f.Host!.App.Services;
        using var ambito = raiz.CreateScope();
        var sp = ambito.ServiceProvider;
        string Impl<T>() where T : class => string.Join(",", sp.GetServices<T>().Select(x => x!.GetType().FullName));

        var lineas = new List<string>
        {
            $"IProviderAuthorizedOrderCorrelationSource → {Impl<IProviderAuthorizedOrderCorrelationSource>()}",
            $"IProviderAuthenticatedOrderDeclarationSource → {(sp.GetService<IProviderAuthenticatedOrderDeclarationSource>() is null ? "(ninguno)" : "PRESENTE")}",
            $"IProviderEvidenceVerifier → {(sp.GetService<IProviderEvidenceVerifier>() is null ? "(ninguno)" : "PRESENTE")}",
            $"IProviderReceptionService → {(sp.GetService<IProviderReceptionService>() is null ? "(ninguno)" : "PRESENTE")}",
            $"IProviderCommercialEffectPort → {Impl<IProviderCommercialEffectPort>()}",
            $"IProviderCommercialRetryPolicy → {Impl<IProviderCommercialRetryPolicy>()}",
            $"IM03CommercialReadOnlyEvaluationPort → {Impl<IM03CommercialReadOnlyEvaluationPort>()}",
            $"ICommercialReadOnlyEvaluationCandidate → {Impl<ICommercialReadOnlyEvaluationCandidate>()}",
            $"IExternalSettlementCandidate → {(sp.GetService<IExternalSettlementCandidate>() is null ? "(ninguno)" : "PRESENTE")}",
            $"IPayableOrderCandidate → {(sp.GetService<IPayableOrderCandidate>() is null ? "(ninguno)" : "PRESENTE")}",
            $"IOrderValueAggregateReader → {(sp.GetService<IOrderValueAggregateReader>() is null ? "(ninguno)" : "PRESENTE")}",
            $"SalesDbContext → {Impl<SalesDbContext>()}",
        };

        var fisico = await InstalacionRealR7.Consultar(f.Cadena,
            "select coalesce(to_regnamespace('payments')::text, '(no existe)')",
            "select count(*) from pg_tables where schemaname = 'payments'");
        lineas.Add($"schema payments en la base física = {fisico[0]} · tablas = {fisico[1]}");
        Anotar("r9-m11-barreras.txt", [.. lineas]);

        Assert.Equal("Sillar.Modules.Payments.Application.UnavailableProviderAuthorizedOrderCorrelationSource", Impl<IProviderAuthorizedOrderCorrelationSource>());
        Assert.Null(sp.GetService<IProviderAuthenticatedOrderDeclarationSource>());
        Assert.Null(sp.GetService<IProviderEvidenceVerifier>());
        Assert.Null(sp.GetService<IProviderReceptionService>());
        Assert.Equal("Sillar.Modules.Payments.Application.M03ReadOnlyNegativeEffectPortAdapter", Impl<IProviderCommercialEffectPort>());
        Assert.Equal("Sillar.Modules.Payments.Application.Contracts.HoldIndeterminateCommercialRetryPolicy", Impl<IProviderCommercialRetryPolicy>());
        Assert.Equal("Sillar.Modules.Payments.Application.M03CommercialReadOnlyEvaluationPortAdapter", Impl<IM03CommercialReadOnlyEvaluationPort>());
        Assert.Equal("Sillar.Modules.Sales.Commercial.CandidateCommercialReadOnlyEvaluationAdapter", Impl<ICommercialReadOnlyEvaluationCandidate>());
        Assert.Null(sp.GetService<IExternalSettlementCandidate>());
        Assert.Null(sp.GetService<IPayableOrderCandidate>());
        Assert.Null(sp.GetService<IOrderValueAggregateReader>());
        Assert.Equal("(no existe)", fisico[0]);
        Assert.Equal("0", fisico[1]);
    }

    /// <summary>Lo que M11 tiene persistido para un evento: nada, porque no hay tablas de Payments.</summary>
    private sealed class SinHechos : IProviderSettlementFactsSource
    {
        public ValueTask<ProviderSettlementFacts?> LoadCandidateFactsAsync(Guid inboxEventId, CancellationToken cancellationToken)
            => ValueTask.FromResult<ProviderSettlementFacts?>(null);
    }

    private sealed class SinReferencias : IProviderVerifiedEvidenceReferenceSource
    {
        public ValueTask<IReadOnlyList<ProviderVerifiedEvidenceReference>> LoadAsync(Guid inboxEventId, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<ProviderVerifiedEvidenceReference>>([]);
    }

    [Fact]
    public async Task R9_B_El_procesador_real_del_inbox_sobre_el_Host_fisico_se_detiene_sin_hechos_y_sin_tocar_M03()
    {
        Requiere();
        // El inbox de M11 no tiene tablas (sin migraciones de Payments): se sustituye solo su
        // infraestructura, como en R5, por el inbox en memoria con el evento de prueba de R5 y
        // fuentes vacías. No se fabrican hechos, referencias, correlación ni declaración.
        var estado = new ProviderInboxStatus(
            Guid.CreateVersion7(), PaymentTestData.Evidence(), ProviderInboxProcessingState.Ready,
            ProviderInboxCommercialState.NotRequested, null, 0, null, null, null, null, null, 1);
        var inbox = new MemoryInboxRepository(estado);
        var h = await NegativeCommerceAuthenticatedHost.BuildAsync(f.Configuracion(), Environments.Development, s =>
        {
            s.RemoveAll<IProviderInboxRepository>();
            s.AddSingleton<IProviderInboxRepository>(inbox);
            s.RemoveAll<IProviderSettlementFactsSource>();
            s.AddSingleton<IProviderSettlementFactsSource>(new SinHechos());
            s.RemoveAll<IProviderVerifiedEvidenceReferenceSource>();
            s.AddSingleton<IProviderVerifiedEvidenceReferenceSource>(new SinReferencias());
        }, TestContext.Current.CancellationToken);
        await using var _ = h.App;
        Assert.True(h.ReportingMounted);

        var huella = await f.Huella();
        ProviderInboxProcessResult? r = null, otra = null;
        var entradas = await f.SentenciasDelHost(async () =>
        {
            using var ambito = h.App.Services.CreateScope();
            var procesador = ambito.ServiceProvider.GetRequiredService<IProviderInboxProcessingService>();
            r = await procesador.ProcessAsync(estado.InboxEventId, PaymentTestData.VerifiedAt.AddMinutes(1),
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
            otra = await procesador.ProcessAsync(estado.InboxEventId, PaymentTestData.VerifiedAt.AddMinutes(30),
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
        });

        var st = inbox.Status;
        Assert.Equal(ProviderInboxProcessOutcome.DecisionRequired, r!.Outcome);
        Assert.Equal(ProviderInboxProcessingState.Held, st.ProcessingState);
        Assert.Equal(ProviderInboxCommercialState.DecisionRequired, st.CommercialState);
        Assert.Equal("verified_settlement_facts_missing", st.HoldReason);
        Assert.False(M03ReadOnlyNegativeReason.IsM03ReadOnly(st.HoldReason));
        Assert.Null(st.CommercialEffectReference);
        Assert.Null(st.NextAttemptAt);
        Assert.Equal(ProviderInboxProcessOutcome.NotReady, otra!.Outcome);
        var clasificadas = ClasificadorDeRegistroPg.Clasificar(entradas);
        var sql = Sentencias(clasificadas);
        Assert.Empty(sql);
        Assert.Empty(clasificadas.Fallidas);
        Assert.Equal(huella, await f.Huella());
        Anotar("r9-inbox-fisico.txt",
            $"inbox→procesador real (Host físico) | {r.Outcome} | inbox={st.ProcessingState}/{st.CommercialState} | razón={st.HoldReason} | próximo_intento={(st.NextAttemptAt is null ? "ninguno" : st.NextAttemptAt)} | referencia={(st.CommercialEffectReference ?? "ninguna")} | {clasificadas.Resumen} | reproceso={otra.Outcome}");
    }
}
