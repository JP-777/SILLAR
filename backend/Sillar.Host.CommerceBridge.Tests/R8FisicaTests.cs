using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Sillar.Core;
using Sillar.Core.Authentication;
using Sillar.Modules.Sales;
using Sillar.Modules.Sales.Data;
using Sillar.Shared.Replication;
using System.Text.Encodings.Web;
using static Sillar.Host.CommerceBridge.Tests.ComposicionVerticalTests;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>Un esquema que nunca se ejecuta: solo sirve para demostrar que no se puede añadir.</summary>
internal sealed class EsquemaIntruso(IOptionsMonitor<AuthenticationSchemeOptions> o, ILoggerFactory l, UrlEncoder e)
    : AuthenticationHandler<AuthenticationSchemeOptions>(o, l, e)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => throw new InvalidOperationException("nunca");
}

internal sealed class PermitirTodo : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context) => throw new InvalidOperationException("nunca");
}

/// <summary>
/// P1-POST-G6-R8 (Claude A). D: persistencia de sesiones tras reiniciar el Host y barrera contra
/// sustituciones de servicios críticos de CORE. B: reproducción material del bloqueo de la
/// persistencia física de M03 (sus migraciones exigen tablas de CRM y Catalog).
/// </summary>
public sealed class R8FisicaTests(InstalacionRealR7 f) : IClassFixture<InstalacionRealR7>
{
    private static readonly Dictionary<string, List<string>> Registro = [];

    private void Requiere()
    {
        if (!f.Disponible)
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay instancia PostgreSQL desechable para R8.");
        }
    }

    private static void Anotar(string archivo, string linea)
    {
        lock (Registro)
        {
            if (!Registro.TryGetValue(archivo, out var lineas)) Registro[archivo] = lineas = [];
            lineas.Add(linea);
            Escribir(archivo, lineas);
        }
    }

    [Fact]
    public async Task R8_D_Las_sesiones_de_CORE_sobreviven_al_reinicio_del_Host_y_la_revocada_sigue_revocada()
    {
        Requiere();
        Assert.NotEqual("", f.HostAntesDelReinicio);
        Assert.NotEqual("", f.HostTrasElReinicio);

        foreach (var (quien, cookie, esperado) in new[]
                 {
                     ("admin", f.CookieAdmin, HttpStatusCode.OK),
                     ("super_admin", f.CookieSuper, HttpStatusCode.OK),
                     ("editor", f.CookieEditor, HttpStatusCode.Forbidden),
                     ("revocada", f.CookieRevocada, HttpStatusCode.Unauthorized),
                 })
        {
            using var r = await InstalacionRealR7.Pedir(f.Http, HttpMethod.Get, "/api/admin/auth/me", cookie);
            var datos = await InstalacionRealR7.Pedir(f.Http, HttpMethod.Get,
                "/api/admin/reporting/orders?start=2026-10-01T00:00:00Z&end=2026-10-02T00:00:00Z", cookie);
            Assert.Equal(esperado, datos.StatusCode);
            Anotar("r8-reinicio.txt", $"tras el reinicio · {quien} · /auth/me → {(int)r.StatusCode} · /reporting/orders → {(int)datos.StatusCode}");
        }

        // Usar una cookie no crea sesiones nuevas: las que hay son las de antes del reinicio.
        var despues = long.Parse((await InstalacionRealR7.Consultar(f.Cadena, "select count(*) from core.admin_sessions"))[0]);
        Assert.Equal(f.SesionesAntesDelReinicio, despues);
        Anotar("r8-reinicio.txt", $"sesiones en core.admin_sessions antes={f.SesionesAntesDelReinicio} después={despues} · host {f.HostAntesDelReinicio} → {f.HostTrasElReinicio}");
    }

    public static TheoryData<string> Sabotajes => new(
        "sustituir CsrfTokenFactory", "sustituir CoreDbContext", "añadir un esquema de autenticación",
        "alterar las opciones de autorización", "añadir un manejador de autorización", "quitar ICurrentAdmin");

    [Theory]
    [MemberData(nameof(Sabotajes))]
    public async Task R8_D_La_sustitucion_de_infraestructura_no_puede_tocar_servicios_criticos_de_CORE(string sabotaje)
    {
        Requiere();
        Action<IServiceCollection> alterar = sabotaje switch
        {
            "sustituir CsrfTokenFactory" => s => { s.RemoveAll<CsrfTokenFactory>(); s.AddSingleton(new CsrfTokenFactory(Guid.NewGuid())); },
            "sustituir CoreDbContext" => s => { s.RemoveAll<Sillar.Core.Data.CoreDbContext>(); s.AddScoped<Sillar.Core.Data.CoreDbContext>(); },
            "añadir un esquema de autenticación" => s => s.AddAuthentication().AddScheme<AuthenticationSchemeOptions, EsquemaIntruso>("Intruso", null),
            "alterar las opciones de autorización" => s => s.PostConfigure<AuthorizationOptions>(o => o.FallbackPolicy = null),
            "añadir un manejador de autorización" => s => s.AddSingleton<IAuthorizationHandler, PermitirTodo>(),
            "quitar ICurrentAdmin" => s => s.RemoveAll<Sillar.Core.Contracts.ICurrentAdmin>(),
            _ => throw new ArgumentOutOfRangeException(nameof(sabotaje)),
        };

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => NegativeCommerceAuthenticatedHost.BuildAsync(
            f.Configuracion(), Environments.Development, s => { s.RemoveAll<SalesDbContext>(); s.AddSingleton(f.Ctx!); alterar(s); }, CancellationToken.None));
        Assert.Contains("servicios críticos de CORE", e.Message, StringComparison.Ordinal);
        Anotar("r8-sustituciones.txt", $"{sabotaje} → rechazado: {e.Message}");
    }

    [Fact]
    public async Task R8_D_Sustituir_solo_infraestructura_comercial_sigue_permitido()
    {
        Requiere();
        var h = await NegativeCommerceAuthenticatedHost.BuildAsync(f.Configuracion(), Environments.Development,
            s => { s.RemoveAll<SalesDbContext>(); s.AddSingleton(f.Ctx!); }, CancellationToken.None);
        await h.App.DisposeAsync();
        Assert.True(h.ReportingMounted);
    }

    /// <summary>
    /// B: con solo las migraciones autorizadas (CORE y Sales), la primera de Sales falla en sus FK
    /// cruzadas a <c>crm.customers</c> y <c>catalog.product_items</c> (SalesInitial.cs:474-487), y
    /// no deja ninguna migración de Sales aplicada. Es la evidencia del bloqueo, no un sustituto.
    /// </summary>
    [Fact]
    public async Task R8_B_La_primera_migracion_de_Sales_exige_CRM_y_Catalog_y_no_queda_aplicada()
    {
        Requiere();
        var nombre = $"sillar_r8_sales_{Guid.NewGuid():N}";
        var cadena = new NpgsqlConnectionStringBuilder(f.Cadena) { Database = nombre }.ConnectionString;
        await InstalacionRealR7.Ejecutar(f.Servidor, $"CREATE DATABASE \"{nombre}\"");
        var lineas = new List<string> { $"base={nombre} servidor={new NpgsqlConnectionStringBuilder(cadena).Host}:{new NpgsqlConnectionStringBuilder(cadena).Port}" };
        try
        {
            // Inventario previo, desde los propios contextos.
            await using (var ventas = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>().UseNpgsql(cadena).Options, new NodeIdentity("prueba"), TimeProvider.System))
            {
                lineas.Add("sales_migraciones_del_proyecto=" + string.Join(",", ventas.Database.GetMigrations()));
            }

            await new CoreModule().ApplyMigrationsAsync(cadena, CancellationToken.None);
            lineas.AddRange(await InstalacionRealR7.Consultar(cadena,
                "select 'core_aplicadas=' || string_agg(\"MigrationId\", ',' order by \"MigrationId\") from core.\"__migrations\"",
                "select 'schemas_tras_core=' || string_agg(nspname, ',' order by nspname) from pg_namespace where nspname not like 'pg_%' and nspname <> 'information_schema'"));

            var error = await Assert.ThrowsAnyAsync<Exception>(() => new SalesModule().ApplyMigrationsAsync(cadena, CancellationToken.None));
            var pg = error as PostgresException ?? error.InnerException as PostgresException;
            Assert.NotNull(pg);
            lineas.Add($"sales_fallo sqlstate={pg.SqlState} mensaje={pg.MessageText} consulta={pg.InternalQuery ?? "(n/d)"}");
            Assert.Contains("crm", pg.MessageText, StringComparison.Ordinal);

            lineas.AddRange(await InstalacionRealR7.Consultar(cadena,
                "select 'schemas_tras_sales=' || string_agg(nspname, ',' order by nspname) from pg_namespace where nspname not like 'pg_%' and nspname <> 'information_schema'",
                "select 'tablas_sales=' || coalesce(string_agg(tablename, ',' order by tablename), '(ninguna)') from pg_tables where schemaname = 'sales'",
                "select 'sales_migraciones_aplicadas=' || coalesce((select string_agg(\"MigrationId\", ',') from sales.\"__migrations\"), '(ninguna)') where to_regclass('sales.\"__migrations\"') is not null union all select 'sales_historial=(no existe)' where to_regclass('sales.\"__migrations\"') is null",
                "select 'tablas_fuera_de_core_y_sales=' || count(*) from pg_tables where schemaname not in ('core', 'sales', 'pg_catalog', 'information_schema')"));
            Assert.Contains(lineas, l => l == "tablas_sales=(ninguna)" || l == "tablas_sales=__migrations");
            Assert.DoesNotContain(lineas, l => l.StartsWith("sales_migraciones_aplicadas=2026", StringComparison.Ordinal));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await InstalacionRealR7.Ejecutar(f.Servidor, $"DROP DATABASE IF EXISTS \"{nombre}\" WITH (FORCE)");
            lineas.Add("base_eliminada=" + nombre);
            Escribir("r8-bloqueo-migraciones-sales.txt", lineas);
        }
    }
}
