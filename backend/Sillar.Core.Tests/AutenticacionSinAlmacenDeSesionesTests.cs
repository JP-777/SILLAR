using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Core.Endpoints;
using Sillar.Core.Services;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// H-21: una cookie de sesión contra una base donde todavía no existe el
/// almacenamiento de sesiones.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reproducido a mano en los dos sentidos.</b> Con una cookie vieja y
/// <c>core</c> apartado, la recarga caía en «No se pudo cargar el sistema» con un
/// 500: el handler consultaba <c>core.admin_sessions</c> antes de que ninguna ruta
/// —ni siquiera <c>/api/setup/status</c>— pudiera decir nada. Devuelto
/// <c>core</c>, la misma cookie recuperaba el panel.
/// </para>
/// <para>
/// <b>La regla es local:</b> si todavía no existe el almacenamiento que permite
/// validar una cookie, esa cookie no concede identidad y la autenticación se
/// comporta como ausente. Nada más: no es un detector de modo instalación, y
/// cualquier otra avería sigue saliendo como tal.
/// </para>
/// <para>
/// Toda prueba que espera <c>NoResult</c> comprueba además que <b>no</b> hay
/// identidad: un <c>NoResult</c> leído como usuario autenticado sería peor que
/// el 500.
/// </para>
/// </remarks>
public sealed class AutenticacionSinAlmacenDeSesionesTests
{
    private const string CookieVieja = "token-de-una-sesion-que-ya-no-existe";

    // ==================================================================
    // A nivel de handler.
    // ==================================================================

    [Fact]
    public async Task Sin_cookie_no_hay_resultado_como_siempre()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await using var contexto = Contexto(cadena);
            NoEsIdentidad(await AutenticarAsync(contexto, cookie: null));
        }, ct);
    }

    [Fact]
    public async Task Con_cookie_y_sin_tabla_de_sesiones_no_hay_resultado_y_no_hay_identidad()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await using var contexto = Contexto(cadena);
            NoEsIdentidad(await AutenticarAsync(contexto, CookieVieja));
        }, ct);
    }

    [Fact]
    public async Task Con_la_tabla_presente_y_una_sesion_valida_autentica_con_normalidad()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            var token = await InstalarYAbrirSesionAsync(cadena, ct);

            await using var contexto = Contexto(cadena);
            var resultado = await AutenticarAsync(contexto, token);

            Assert.True(resultado.Succeeded);
            Assert.True(resultado.Principal!.Identity!.IsAuthenticated);
            Assert.Equal(PeticionValida().Admin!.Email, resultado.Principal.FindFirstValue(ClaimTypes.Email));
        }, ct);
    }

    [Fact]
    public async Task Cuando_aparecen_las_tablas_el_handler_vuelve_a_consultar_con_normalidad()
    {
        var ct = TestContext.Current.CancellationToken;

        // La misma base, antes y después: lo único que cambia es que el
        // almacenamiento de sesiones pasa a existir.
        await ConBaseVaciaAsync(async cadena =>
        {
            await using (var antes = Contexto(cadena))
            {
                NoEsIdentidad(await AutenticarAsync(antes, CookieVieja));
            }

            var token = await InstalarYAbrirSesionAsync(cadena, ct);

            // Ahora la cookie vieja SÍ se consulta, y como no está en la tabla,
            // falla — que es lo normal. Ya no es «ausente»: hay contra qué validar.
            await using (var desconocida = Contexto(cadena))
            {
                var resultado = await AutenticarAsync(desconocida, CookieVieja);

                Assert.False(resultado.None);
                Assert.False(resultado.Succeeded);
                Assert.Equal("Sesión desconocida.", resultado.Failure?.Message);
            }

            await using var valida = Contexto(cadena);
            Assert.True((await AutenticarAsync(valida, token)).Succeeded);
        }, ct);
    }

    // ------------------------------------------------------------------
    // Lo que NO debe tragarse.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Un_42P01_de_otra_relacion_no_se_traga()
    {
        var ct = TestContext.Current.CancellationToken;

        // La tabla de sesiones existe; la que falta es admin_users, a la que la
        // consulta del handler hace JOIN. PostgreSQL da 42P01 nombrando
        // core.admin_users —medido—, y eso es una avería, no un «todavía no
        // hay dónde validar».
        await ConBaseVaciaAsync(async cadena =>
        {
            await EjecutarAsync(cadena, "CREATE SCHEMA core; CREATE TABLE core.admin_sessions (admin_user_id int)", ct);

            await using var contexto = Contexto(cadena);
            var error = await Assert.ThrowsAsync<PostgresException>(() => AutenticarAsync(contexto, CookieVieja));

            Assert.Equal(PostgresErrorCodes.UndefinedTable, error.SqlState);
        }, ct);
    }

    [Fact]
    public async Task Un_UndefinedColumn_no_se_traga()
    {
        var ct = TestContext.Current.CancellationToken;

        // Las dos tablas existen pero sin las columnas que el modelo espera.
        await ConBaseVaciaAsync(async cadena =>
        {
            await EjecutarAsync(cadena,
                "CREATE SCHEMA core; CREATE TABLE core.admin_sessions (x int); CREATE TABLE core.admin_users (y int)", ct);

            await using var contexto = Contexto(cadena);
            var error = await Assert.ThrowsAsync<PostgresException>(() => AutenticarAsync(contexto, CookieVieja));

            Assert.Equal(PostgresErrorCodes.UndefinedColumn, error.SqlState);
        }, ct);
    }

    [Fact]
    public async Task Una_base_inexistente_no_se_convierte_en_ausencia()
    {
        var ct = TestContext.Current.CancellationToken;
        var cadena = Cadena();

        if (cadena is null)
        {
            Assert.Skip("Sin ConnectionStrings__Default.");
            return;
        }

        var inexistente = new NpgsqlConnectionStringBuilder(cadena) { Database = $"no_existe_{Guid.NewGuid():N}" }.ConnectionString;

        await using var contexto = Contexto(inexistente);
        var error = await Assert.ThrowsAsync<PostgresException>(() => AutenticarAsync(contexto, CookieVieja));

        Assert.Equal(PostgresErrorCodes.InvalidCatalogName, error.SqlState);
    }

    [Fact]
    public async Task Una_conexion_caida_no_se_convierte_en_ausencia()
    {
        var cadena = Cadena();

        if (cadena is null)
        {
            Assert.Skip("Sin ConnectionStrings__Default.");
            return;
        }

        // Un puerto donde no escucha nadie: la conexión se rechaza en el acto.
        var caida = new NpgsqlConnectionStringBuilder(cadena) { Port = 1, Timeout = 3 }.ConnectionString;

        await using var contexto = Contexto(caida);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => AutenticarAsync(contexto, CookieVieja));

        Assert.IsNotType<PostgresException>(error);
    }

    // ==================================================================
    // A nivel de pipeline: UseRouting -> UseAuthentication -> la ruta.
    // ==================================================================

    [Fact]
    public async Task Con_cookie_vieja_y_sin_core_setup_status_responde_y_una_ruta_protegida_da_401()
    {
        var ct = TestContext.Current.CancellationToken;

        // La forma del modo normal, que es donde vive H-21: el host arrancó
        // instalado, la base perdió core, y la cookie llega a UseAuthentication
        // antes que a cualquier ruta.
        await ConBaseVaciaAsync(async cadena =>
        {
            var (pipeline, servicios) = Pipeline(cadena, rutas =>
            {
                rutas.MapAlwaysAvailableStatus();
                rutas.MapGet("/protegida", () => "no deberías leer esto").RequireAuthorization();
            });

            var estado = await PedirAsync(pipeline, servicios, "GET", "/api/setup/status", CookieVieja);
            Assert.Equal(StatusCodes.Status200OK, estado.Response.StatusCode);
            Assert.False(estado.User.Identity?.IsAuthenticated ?? false);

            var protegida = await PedirAsync(pipeline, servicios, "GET", "/protegida", CookieVieja);
            Assert.Equal(StatusCodes.Status401Unauthorized, protegida.Response.StatusCode);
            Assert.False(protegida.User.Identity?.IsAuthenticated ?? false);
        }, ct);
    }

    [Fact]
    public async Task Base_pristina_y_cookie_vieja_el_instalador_termina_y_la_cookie_nunca_autentica()
    {
        var ct = TestContext.Current.CancellationToken;

        // El caso crítico, en su composición MÁS estricta: autenticación y rutas
        // de setup en el mismo pipeline. En el host real el modo instalación no
        // monta autenticación, así que allí la cookie ni se mira; aquí se exige
        // que, aunque se mirase, no bloquee ni conceda nada.
        await ConBaseVaciaAsync(async cadena =>
        {
            var (pipeline, servicios) = Pipeline(cadena, rutas => rutas.MapSetupEndpoints());

            var estado = await PedirAsync(pipeline, servicios, "GET", "/api/setup/status", CookieVieja);
            Assert.Equal(StatusCodes.Status200OK, estado.Response.StatusCode);
            Assert.Contains("\"migrationsPending\":true", Cuerpo(estado));
            Assert.False(estado.User.Identity?.IsAuthenticated ?? false);

            var instalar = await PedirAsync(pipeline, servicios, "POST", "/api/setup", CookieVieja,
                JsonSerializer.SerializeToUtf8Bytes(PeticionValida()));

            // Con el cuerpo en el mensaje: un 400 aquí puede ser el enlazado o la
            // validación, y sin leerlo no se sabe cuál.
            Assert.True(
                instalar.Response.StatusCode == StatusCodes.Status201Created,
                $"POST /api/setup respondió {instalar.Response.StatusCode}: {Cuerpo(instalar)}");
            Assert.False(instalar.User.Identity?.IsAuthenticated ?? false);

            // Las migraciones de todos los módulos desplegados, aplicadas por el
            // instalador de H-27, y la instalación terminada.
            Assert.Equal(["catalog", "cms", "core", "crm"], (await SchemasAsync(cadena, ct)).Where(s => s != "public").Order());

            await using var contexto = Contexto(cadena);
            Assert.Equal(SetupState.Completed, await Servicio(contexto).GetStateAsync(ct));

            // Y la cookie vieja, ya con tabla de sesiones, sigue sin conceder
            // nada: ahora falla por desconocida.
            Assert.False((await AutenticarAsync(contexto, CookieVieja)).Succeeded);
        }, ct);
    }

    // ==================================================================

    /// <summary>Un NoResult de verdad: ni éxito, ni fallo, ni identidad.</summary>
    private static void NoEsIdentidad(AuthenticateResult resultado)
    {
        Assert.True(resultado.None);
        Assert.False(resultado.Succeeded);
        Assert.Null(resultado.Principal);
        Assert.Null(resultado.Ticket);
        Assert.Null(resultado.Failure);
    }

    private static async Task<AuthenticateResult> AutenticarAsync(CoreDbContext contexto, string? cookie)
    {
        var handler = new AdminSessionAuthenticationHandler(
            new OpcionesFijas(), NullLoggerFactory.Instance, UrlEncoder.Default, contexto, TimeProvider.System);

        var http = new DefaultHttpContext();
        if (cookie is not null)
        {
            http.Request.Headers.Cookie = $"{AdminSessionCookie.Name}={cookie}";
        }

        await handler.InitializeAsync(
            new AuthenticationScheme(AdminSessionAuthenticationHandler.SchemeName, null, typeof(AdminSessionAuthenticationHandler)),
            http);

        return await handler.AuthenticateAsync();
    }

    /// <summary>Instala con el arnés de H-27 y abre una sesión para su super_admin.</summary>
    private static async Task<string> InstalarYAbrirSesionAsync(string cadena, CancellationToken ct)
    {
        await using var contexto = Contexto(cadena);
        Assert.Equal(SetupOutcome.Completed, (await Servicio(contexto).CompleteAsync(PeticionValida(), ct)).Outcome);

        var usuario = await contexto.AdminUsers.SingleAsync(ct);
        var token = SessionTokens.CreateSessionToken();
        var ahora = TimeProvider.System.GetUtcNow();

        // Los mismos campos que pone el inicio de sesión (AdminAuthenticationService).
        contexto.AdminSessions.Add(new AdminSession
        {
            AdminSessionId = Guid.CreateVersion7(),
            AdminUserId = usuario.AdminUserId,
            TokenHash = SessionTokens.Hash(token),
            CsrfTokenHash = SessionTokens.Hash("csrf-de-prueba"),
            IssuedAt = ahora,
            LastSeenAt = ahora,
            ExpiresAt = SessionPolicy.ExpiresAt(ahora, ahora)
        });
        await contexto.SaveChangesAsync(ct);

        return token;
    }

    /// <summary>
    /// Un pipeline de verdad —enrutado, autenticación, autorización y las
    /// rutas que se pidan— sin levantar un servidor.
    /// </summary>
    private static (RequestDelegate Pipeline, IServiceProvider Servicios) Pipeline(
        string cadena,
        Action<IEndpointRouteBuilder> rutas)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var servicios = new ServiceCollection();

        servicios.AddSingleton<IConfiguration>(configuracion);
        servicios.AddLogging();
        servicios.AddRouting();

        // Lo que un host registra solo y aquí no hay host que lo haga: el
        // middleware de enrutado lo pide al construirse.
        var diagnostico = new System.Diagnostics.DiagnosticListener("Sillar.Core.Tests");
        servicios.AddSingleton(diagnostico);
        servicios.AddSingleton<System.Diagnostics.DiagnosticSource>(diagnostico);
        servicios.AddSingleton<IHostApplicationLifetime>(new CicloDeVidaInerte());
        servicios.AddSingleton(Desplegados());
        servicios.AddCoreEssentials(configuracion, cadena);
        servicios.AddCoreAuthentication();

        var proveedor = servicios.BuildServiceProvider();
        var app = new ApplicationBuilder(proveedor);

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(rutas);

        return (app.Build(), proveedor);
    }

    private static async Task<HttpContext> PedirAsync(
        RequestDelegate pipeline,
        IServiceProvider servicios,
        string metodo,
        string ruta,
        string cookie,
        byte[]? cuerpo = null)
    {
        var ambito = servicios.CreateScope();
        var http = new DefaultHttpContext { RequestServices = ambito.ServiceProvider };

        http.Request.Method = metodo;
        http.Request.Path = ruta;
        http.Request.Headers.Cookie = $"{AdminSessionCookie.Name}={cookie}";
        http.Response.Body = new MemoryStream();

        if (cuerpo is not null)
        {
            // Lo que Kestrel declara y un DefaultHttpContext suelto no: que la
            // petición puede traer cuerpo. Sin esto la minimal API ni lo lee, el
            // parámetro obligatorio llega nulo y responde 400 sin explicación —
            // que es exactamente lo que devolvía antes de añadirlo.
            http.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature>(new ConCuerpo());
            http.Request.ContentType = "application/json";
            http.Request.ContentLength = cuerpo.Length;
            http.Request.Body = new MemoryStream(cuerpo);
        }

        await pipeline(http);
        return http;
    }

    private static string Cuerpo(HttpContext http)
    {
        http.Response.Body.Position = 0;
        return new StreamReader(http.Response.Body).ReadToEnd();
    }

    private sealed class ConCuerpo : Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private sealed class OpcionesFijas : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue { get; } = new();

        public AuthenticationSchemeOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }
}
