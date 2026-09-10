using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sillar.Core.Endpoints;
using Sillar.Core.Modularity;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Dtos;
using Sillar.Core.Services;
using Sillar.Shared.Configuration;
using Sillar.Shared.Replication;

namespace Sillar.Core.Tests;

/// <summary>
/// Auditoría que revienta si alguien la llama.
/// </summary>
/// <remarks>
/// No es un espía: es una aserción. Cuando faltan las migraciones, la
/// instalación tiene que rendirse <b>antes</b> de escribir nada — y «antes» no
/// se puede comprobar mirando el resultado, solo poniendo una trampa en el
/// camino que no debería recorrerse.
/// </remarks>
internal sealed class AuditoriaQueNoDeberiaLlamarse : IAuditWriter
{
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
        => throw new InvalidOperationException(
            "Se auditó una instalación que no debía llegar a intentarse: faltaban las migraciones.");
}

/// <summary>
/// Arrancar contra una base de datos vacía.
///
/// <para>
/// <b>Qué falla si esto falla.</b> Una base recién creada no tiene el schema
/// <c>core</c>. La primera consulta del sistema —<c>GET /api/setup/status</c>,
/// que es la <b>única</b> ruta que el modo instalación monta— lanzaba
/// <c>42P01: relation "core.installation" does not exist</c> y salía por el
/// manejador genérico. La primera pantalla de quien instala en una clienta era
/// un 500 con un <c>traceId</c>.
/// </para>
/// <para>
/// <b>Por qué esta prueba crea y destruye una base entera.</b> Porque es la
/// única forma de tener lo que el caso necesita: una base <b>sin migrar</b>. Los
/// proyectos que tocan base están atados a la efímera de la puerta, que llega ya
/// migrada, así que ninguno podía reproducir esto. El arreglo se verificó en su
/// día a mano, arrancando contra una base vacía de verdad; lo que faltaba era
/// que esa verificación se repitiera sola.
/// </para>
/// <para>
/// La base se llama <c>sillar_vacia_*</c>, se crea al empezar y se destruye al
/// terminar aunque la prueba falle. No es la efímera de la puerta y no la toca.
/// </para>
/// </summary>
public sealed class ArranqueContraBaseVaciaTests
{
    private static readonly NodeIdentity Nodo = new(NodeIdentity.DefaultCode);

    /// <summary>La cadena del entorno, o <c>null</c> si no hay ninguna.</summary>
    private static string? Cadena()
    {
        DotEnv.Load();
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        return string.IsNullOrWhiteSpace(cadena) ? null : cadena;
    }

    private static CoreDbContext Contexto(string cadena)
        => new(
            new DbContextOptionsBuilder<CoreDbContext>().UseNpgsql(cadena).Options,
            Nodo,
            TimeProvider.System);

    private static SetupService Servicio(CoreDbContext contexto)
        => new(contexto, new FakePasswordHasher(), new AuditoriaQueNoDeberiaLlamarse(), TimeProvider.System);

    /// <summary>
    /// Crea una base vacía, corre lo que se le pase contra ella y la destruye.
    /// </summary>
    private static async Task ConBaseVaciaAsync(Func<string, Task> cuerpo, CancellationToken ct)
    {
        var cadena = Cadena();

        if (cadena is null)
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay servidor donde crear una base vacía.");
            return;
        }

        var nombre = $"sillar_vacia_{Guid.NewGuid():N}";
        var mantenimiento = new NpgsqlConnectionStringBuilder(cadena) { Database = "postgres" }.ConnectionString;
        var destino = new NpgsqlConnectionStringBuilder(cadena) { Database = nombre }.ConnectionString;

        await using (var admin = new NpgsqlConnection(mantenimiento))
        {
            await admin.OpenAsync(ct);
            await using var crear = new NpgsqlCommand($"CREATE DATABASE \"{nombre}\"", admin);
            await crear.ExecuteNonQueryAsync(ct);
        }

        try
        {
            await cuerpo(destino);
        }
        finally
        {
            // Las conexiones abiertas contra la base se cierran solas al salir
            // del `using` del contexto, pero el pool de Npgsql las conserva: sin
            // vaciarlo, el DROP falla por «is being accessed by other users».
            NpgsqlConnection.ClearAllPools();

            await using var admin = new NpgsqlConnection(mantenimiento);
            await admin.OpenAsync(ct);
            await using var borrar = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{nombre}\" WITH (FORCE)", admin);
            await borrar.ExecuteNonQueryAsync(ct);
        }
    }

    [Fact]
    public async Task Sin_migraciones_el_estado_es_faltan_migraciones_y_no_una_excepcion()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await using var contexto = Contexto(cadena);

            // Antes esto lanzaba PostgresException 42P01 y acababa en un 500.
            Assert.Equal(SetupState.MigrationsPending, await Servicio(contexto).GetStateAsync(ct));
        }, ct);
    }

    [Fact]
    public async Task La_misma_base_cambia_de_estado_al_aplicarle_las_migraciones()
    {
        var ct = TestContext.Current.CancellationToken;

        // **La prueba de que la de arriba comprueba algo**, y de que lo hace
        // sin depender de nada de fuera.
        //
        // La primera versión de este contraste usaba la base del entorno,
        // dando por hecho que estaría migrada. No lo estaba —el PostgreSQL de
        // desarrollo de esta máquina llevaba días vacío— y la prueba salió en
        // rojo. Fue útil: una prueba que se apoya en el estado ambiental no
        // afirma lo que dice afirmar, afirma cómo estaba la máquina ese día.
        //
        // Así que el contraste ocurre ahora sobre **la misma base**: se mira
        // antes de migrar y después de migrar, y lo único que cambia entre las
        // dos aserciones son las migraciones.
        await ConBaseVaciaAsync(async cadena =>
        {
            await using var contexto = Contexto(cadena);

            Assert.Equal(SetupState.MigrationsPending, await Servicio(contexto).GetStateAsync(ct));

            await contexto.Database.MigrateAsync(ct);

            // Ya no faltan las migraciones. Falta instalar, que es otra cosa y
            // tiene otro responsable: es la distinción entera que este arreglo
            // introdujo.
            Assert.Equal(SetupState.SetupPending, await Servicio(contexto).GetStateAsync(ct));
        }, ct);
    }

    [Fact]
    public async Task Sin_migraciones_la_instalacion_se_rinde_antes_de_escribir_nada()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await using var contexto = Contexto(cadena);

            // Datos válidos a propósito: lo que falla no son ellos. Si el
            // servicio los rechazara por inválidos, el usuario recibiría un 400
            // culpándole de algo que no ha hecho mal.
            var peticion = new SetupRequest(
                BusinessName: "Negocio de prueba",
                LicenseType: Sillar.Core.Domain.Values.LicenseType.All.First(),
                Admin: new SetupAdminRequest(
                    FullName: "Persona Que Instala",
                    Email: "instala@ejemplo.test",
                    Password: "Contrasena-Larga-2026"));

            var resultado = await Servicio(contexto).CompleteAsync(peticion, ct);

            Assert.Equal(SetupOutcome.MigrationsPending, resultado.Outcome);

            // Y no por culpa de los datos: eso es lo que separa un 503 de un 400.
            Assert.NotEqual(SetupOutcome.Invalid, resultado.Outcome);
        }, ct);
    }

    // ======================================================================
    // Las rutas, llamadas directamente contra una base vacía.
    //
    // Las dos pruebas de arriba miran el servicio. Estas miran **lo que
    // responde la ruta**, que es lo que ve quien instala: el 500 no lo producía
    // el servicio, lo producía el manejador genérico al recibir la excepción
    // que el servicio dejaba subir.
    // ======================================================================

    [Fact]
    public async Task GET_setup_status_contra_base_vacia_responde_200_y_no_un_500()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await using var contexto = Contexto(cadena);

            var respuesta = await SetupEndpoints.GetStatus(Servicio(contexto), ct);

            // 200 y no 500: la pregunta era «¿en qué estado estás?» y el
            // servidor lo sabe. Un 500 decía «no sé», y no era verdad.
            var ok = Assert.IsType<Ok<SetupStatusResponse>>(respuesta);

            Assert.True(ok.Value!.SetupRequired);
            Assert.True(ok.Value.MigrationsPending);
        }, ct);
    }

    [Fact]
    public async Task POST_setup_sin_tablas_responde_503_y_no_culpa_a_los_datos()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await using var contexto = Contexto(cadena);

            var respuesta = await SetupEndpoints.Complete(
                PeticionValida(), Servicio(contexto), Reiniciador(), new DefaultHttpContext(), ct);

            // **No es un ValidationProblem.** Ésa es la distinción entera: los
            // datos enviados están bien y decirle a quien instala que no lo
            // están sería mandarle a corregir algo que no ha hecho mal.
            Assert.IsNotType<ValidationProblem>(respuesta);

            var problema = Assert.IsType<ProblemHttpResult>(respuesta);

            Assert.Equal(StatusCodes.Status503ServiceUnavailable, problema.StatusCode);

            // Y el remedio va en la respuesta, no en la cabeza de nadie.
            Assert.Contains("dotnet ef database update", problema.ProblemDetails.Detail);
            Assert.Contains("no tienen nada de malo", problema.ProblemDetails.Detail);
        }, ct);
    }

    [Fact]
    public void El_modo_instalacion_monta_las_dos_rutas_de_setup_y_ninguna_mas()
    {
        // Disponibilidad de rutas sin levantar el host: se le da a
        // MapSetupEndpoints un constructor de rutas y se mira qué quedó
        // montado. Si alguien añadiera aquí una ruta de negocio, el modo
        // instalación dejaría de ser lo que dice ser.
        var builder = WebApplication.CreateSlimBuilder();

        // Los dos servicios que los manejadores reciben. **Se registran pero
        // nunca se resuelven**: aquí solo se enumeran las rutas montadas, no se
        // invoca ninguna. Hacen falta porque el enlazado de parámetros de las
        // minimal APIs decide en tiempo de Map si algo es un servicio o un
        // cuerpo de petición, y sin el registro infiere «cuerpo» — que en un
        // GET no está permitido. La fábrica revienta si alguien los pide, para
        // que esta prueba no pueda convertirse en otra cosa sin darse cuenta.
        builder.Services.AddSingleton(_ => Reventar<SetupService>());
        builder.Services.AddSingleton(_ => Reventar<HostRestarter>());

        var app = builder.Build();
        app.MapSetupEndpoints();

        var rutas = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(fuente => fuente.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => $"{string.Join(",", e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)} {e.RoutePattern.RawText}")
            .OrderBy(x => x)
            .ToList();

        // La barra final del POST no es un descuido: sale de `MapGroup("/api/setup")`
        // más `MapPost("")`, y es el patrón que ASP.NET registra de verdad. Se
        // escribe medido y no supuesto — la primera versión de esta línea decía
        // «/api/setup» y la prueba lo corrigió.
        Assert.Equal(["GET /api/setup/status", "POST /api/setup/"], rutas);
    }

    [Fact]
    public async Task Un_error_de_PostgreSQL_que_NO_es_42P01_no_se_traga()
    {
        var ct = TestContext.Current.CancellationToken;

        // **La prueba de que el catch es un filtro y no una manta.**
        //
        // Se prepara una base donde core.installation SÍ existe pero le falta
        // una columna: la consulta falla con 42703 (undefined_column), no con
        // 42P01. Si el catch fuera genérico —o filtrara por el tipo y no por el
        // SqlState— esto devolvería MigrationsPending y mandaría a aplicar unas
        // migraciones que ya están aplicadas.
        await ConBaseVaciaAsync(async cadena =>
        {
            await using (var preparar = new NpgsqlConnection(cadena))
            {
                await preparar.OpenAsync(ct);
                await using var crear = new NpgsqlCommand(
                    "CREATE SCHEMA core; CREATE TABLE core.installation (nada int);", preparar);
                await crear.ExecuteNonQueryAsync(ct);
            }

            await using var contexto = Contexto(cadena);

            var error = await Assert.ThrowsAsync<PostgresException>(
                () => Servicio(contexto).GetStateAsync(ct));

            Assert.Equal(PostgresErrorCodes.UndefinedColumn, error.SqlState);
            Assert.NotEqual(PostgresErrorCodes.UndefinedTable, error.SqlState);
        }, ct);
    }

    private static T Reventar<T>()
        => throw new InvalidOperationException(
            $"Se resolvió {typeof(T).Name} en una prueba que solo enumera rutas.");

    /// <summary>Datos de instalación válidos: lo que falla nunca son ellos.</summary>
    private static SetupRequest PeticionValida()
        => new(
            BusinessName: "Negocio de prueba",
            LicenseType: Sillar.Core.Domain.Values.LicenseType.All.First(),
            Admin: new SetupAdminRequest(
                FullName: "Persona Que Instala",
                Email: "instala@ejemplo.test",
                Password: "Contrasena-Larga-2026"));

    private static HostRestarter Reiniciador()
        => new(
            new CicloDeVidaInerte(),
            new ConfigurationBuilder().AddInMemoryCollection([]).Build(),
            NullLogger<HostRestarter>.Instance);

    /// <summary>Ciclo de vida que no detiene nada: aquí nadie debe reiniciar.</summary>
    private sealed class CicloDeVidaInerte : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication()
            => throw new InvalidOperationException("Se pidió reiniciar el host en un caso que no llega a instalar.");
    }
}
