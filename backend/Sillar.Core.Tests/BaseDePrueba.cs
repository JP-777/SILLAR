using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Dtos;
using Sillar.Core.Modularity;
using Sillar.Core.Services;
using Sillar.Core.Setup;
using Sillar.Modules.Catalog;
using Sillar.Modules.Cms;
using Sillar.Modules.Crm;
using Sillar.Modules.Demo;
using Sillar.Shared.Configuration;
using Sillar.Shared.Events;
using Sillar.Shared.Modularity;
using Sillar.Shared.Replication;

namespace Sillar.Core.Tests;

/// <summary>
/// Lo que comparten las pruebas que necesitan una base de verdad: crearla vacía,
/// usarla y destruirla, y los colaboradores de mentira del instalador y de la
/// activación.
/// </summary>
/// <remarks>
/// Estaba dentro de <c>ArranqueContraBaseVaciaTests</c>. Salió de ahí cuando la
/// necesitaron otras dos suites: tres copias de cómo se crea una base de prueba
/// es la clase de duplicación que este repositorio ya ha pagado.
/// </remarks>
internal static class BaseDePrueba
{
    public static readonly NodeIdentity Nodo = new(NodeIdentity.DefaultCode);

    /// <summary>La cadena del entorno, o <c>null</c> si no hay ninguna.</summary>
    public static string? Cadena()
    {
        DotEnv.Load();
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        return string.IsNullOrWhiteSpace(cadena) ? null : cadena;
    }

    /// <summary>
    /// Un contexto de CORE con <b>la misma configuración que en producción</b>,
    /// historial incluido. Con la de por defecto, EF buscaría el historial en
    /// otra tabla y el instalador vería otra base.
    /// </summary>
    public static CoreDbContext Contexto(string cadena)
        => new(CoreDataServiceExtensions.BuildOptions(cadena), Nodo, TimeProvider.System);

    /// <summary>
    /// Los módulos de un despliegue de desarrollo: los cuatro reales y los de
    /// demostración, que no tienen schema.
    /// </summary>
    /// <remarks>
    /// Desordenados a propósito: el instalador no puede depender del orden en
    /// que llegan, y así se nota si lo hiciera.
    /// </remarks>
    public static DeclaredModules Desplegados()
        => new(
        [
            new CrmModule(),
            new DemoSalesModule(),
            new CmsModule(),
            new DemoCatalogModule(),
            new CoreModule(),
            new DemoCrmModule(),
            new CatalogModule(),
            new DemoServicesModule(),
            new DemoServiceOrdersModule(),
            new DemoTrackingModule(),
        ]);

    public static InstaladorDeModulos Instalador(DeclaredModules? modulos = null)
        => new(modulos ?? Desplegados());

    public static SetupService Servicio(CoreDbContext contexto, IAuditWriter? auditoria = null)
        => new(contexto, new FakePasswordHasher(), auditoria ?? new AuditoriaNula(), TimeProvider.System, Instalador());

    /// <summary>Datos de instalación válidos: lo que falla nunca son ellos.</summary>
    public static SetupRequest PeticionValida()
        => new(
            BusinessName: "Negocio de prueba",
            LicenseType: Sillar.Core.Domain.Values.LicenseType.All.First(),
            Admin: new SetupAdminRequest(
                FullName: "Persona Que Instala",
                Email: "instala@ejemplo.test",
                Password: "Contrasena-Larga-2026"));

    public static HostRestarter Reiniciador()
        => new(
            new CicloDeVidaInerte(),
            new ConfigurationBuilder().AddInMemoryCollection([]).Build(),
            NullLogger<HostRestarter>.Instance);

    public static ModuleActivationService Activacion(CoreDbContext contexto)
        => new(contexto, Desplegados(), new AuditoriaNula(), new EventosNulos(), TimeProvider.System, Reiniciador());

    /// <summary>Crea las filas de módulos y activaciones, como el arranque.</summary>
    public static Task SincronizarAsync(CoreDbContext contexto, CancellationToken ct)
        => new ModuleSynchronizer(contexto, NullLogger<ModuleSynchronizer>.Instance)
            .SynchronizeAsync(Desplegados().Modules, ct);

    /// <summary>
    /// Crea una base vacía, corre lo que se le pase contra ella y la destruye.
    /// </summary>
    /// <remarks>
    /// Se llama <c>sillar_vacia_*</c>, se crea al empezar y se destruye al
    /// terminar aunque la prueba falle. No es la efímera de la puerta y no la
    /// toca.
    /// </remarks>
    public static async Task ConBaseVaciaAsync(Func<string, Task> cuerpo, CancellationToken ct)
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
            // El pool de Npgsql conserva las conexiones: sin vaciarlo, el DROP
            // falla por «is being accessed by other users».
            NpgsqlConnection.ClearAllPools();

            await using var admin = new NpgsqlConnection(mantenimiento);
            await admin.OpenAsync(ct);
            await using var borrar = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{nombre}\" WITH (FORCE)", admin);
            await borrar.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>Los schemas no internos de la base, ordenados.</summary>
    public static async Task<List<string>> SchemasAsync(string cadena, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(
            "SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('pg_catalog','information_schema','pg_toast') " +
            "AND nspname NOT LIKE 'pg\\_%' ORDER BY nspname", conexion);

        var schemas = new List<string>();
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            schemas.Add(lector.GetString(0));
        }

        return schemas;
    }

    /// <summary>Ejecuta SQL de preparación. Solo para dejar la base en un estado dado.</summary>
    public static async Task EjecutarAsync(string cadena, string sql, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>Auditoría que no escribe nada.</summary>
internal sealed class AuditoriaNula : IAuditWriter
{
    public int Escrituras { get; private set; }

    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        Escrituras += 1;
        return Task.CompletedTask;
    }
}

/// <summary>Bus de eventos que no reparte nada.</summary>
internal sealed class EventosNulos : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : notnull
        => Task.CompletedTask;
}

/// <summary>Ciclo de vida que no detiene nada: aquí nadie debe reiniciar el host de verdad.</summary>
internal sealed class CicloDeVidaInerte : IHostApplicationLifetime
{
    public int Paradas { get; private set; }

    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => CancellationToken.None;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication() => Paradas += 1;
}
