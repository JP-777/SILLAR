using Microsoft.EntityFrameworkCore;
using Npgsql;
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
}
