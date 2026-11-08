using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sillar.Core;
using Sillar.Core.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Shared.Configuration;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// Contextos frescos contra PostgreSQL real, para lo que no se puede acreditar sin él.
/// </summary>
/// <remarks>
/// <para>
/// <b>No EF InMemory.</b> Lo que estas pruebas comprueban es traducción y persistencia
/// —que el filtro por cliente va en la consulta, que el <c>CHECK</c> de atribución
/// completa acepta tres nulos, que el aislamiento entre clientes lo hace la base—, y
/// un proveedor en memoria daría verde sin haber tocado nada de eso.
/// </para>
/// <para>
/// <b>No son destructivas, y por eso no exigen la base efímera de la puerta.</b> Cada
/// prueba trabaja dentro de una transacción que se deshace al terminar: ni
/// <c>TRUNCATE</c>, ni <c>DROP</c>, ni filas que sobrevivan. Es lo que permite
/// correrlas contra la base de desarrollo sin estropearla, al contrario que las de
/// M04 y M02.
/// </para>
/// <para>
/// <b>Si el schema <c>sales</c> no está migrado, fallan diciendo exactamente por
/// qué.</b> No se saltan en silencio: una prueba omitida sin declararlo es peor que
/// una roja, porque la roja se ve.
/// </para>
/// </remarks>
public sealed class SalesDbFixture
{
    private const string CorreoSuper = "super.f9.9.2@sillar.test";
    private const string ClaveSuper = "Turquesa-Lluvia-Faro-2026";
    private static int secuenciaDeBases;

    public string ConnectionString { get; }

    public SalesDbFixture()
    {
        DotEnv.Load();

        ConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings__Default. Copia .env.example como .env en la raíz.");

        using var conexion = new NpgsqlConnection(ConnectionString);
        conexion.Open();

        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables " +
            "WHERE table_schema = 'sales' AND table_name = 'orders');";

        if (comando.ExecuteScalar() is not true)
        {
            var baseDeDatos = new NpgsqlConnectionStringBuilder(ConnectionString).Database;

            throw new InvalidOperationException(
                $"El schema 'sales' no está migrado en '{baseDeDatos}', así que estas " +
                "pruebas de persistencia no pueden correr. En la puerta canónica esto " +
                "ocurrirá hasta que Integración añada Sillar.Modules.Sales a la lista " +
                "de módulos de scripts/verificar.mjs — es la costura §j de " +
                "docs/modules/sales/ESCALADAS-M03.md, pedida y no aplicada.");
        }
    }

    /// <summary>Un contexto nuevo, con nodo y reloj fijos.</summary>
    public SalesDbContext NuevoContexto(TimeProvider? reloj = null)
        => new(
            new DbContextOptionsBuilder<SalesDbContext>()
                .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable(
                    SalesDbContext.MigrationsHistoryTable, SalesDbContext.Schema))
                .Options,
            new NodeIdentity("principal"),
            reloj ?? TimeProvider.System);

    /// <summary>
    /// Ejecuta un escenario en una base creada por esta llamada e instalada por el Host real.
    /// </summary>
    /// <remarks>
    /// Esta costura es aditiva y solo la usan los cuatro escenarios de
    /// <c>UnaConexionBajoElBloqueoPersistenciaTests</c> que necesitan una moneda acreditada.
    /// La base compartida de la puerta sirve únicamente para identificar el servidor desechable;
    /// ni se modifica ni se usa para establecer PEN.
    /// </remarks>
    public async Task EnBasePropiaConMonedaAcreditadaAsync(
        Func<string, Task> escenario,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(escenario);

        var entrada = new NpgsqlConnectionStringBuilder(ConnectionString);
        AcreditarServidorDesechable(entrada);

        var sufijo = checked((long)Environment.ProcessId * 1000 + Interlocked.Increment(ref secuenciaDeBases));
        var nombre = $"sillar_verify_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{sufijo}";
        AcreditarNombrePropio(nombre);

        var mantenimiento = new NpgsqlConnectionStringBuilder(entrada.ConnectionString)
        {
            Database = "postgres",
            Pooling = false,
            ApplicationName = "f9.9.2-admin"
        }.ConnectionString;
        var propia = new NpgsqlConnectionStringBuilder(entrada.ConnectionString)
        {
            Database = nombre,
            ApplicationName = "f9.9.2-preparacion"
        }.ConnectionString;

        var creada = false;
        Exception? errorOriginal = null;

        try
        {
            await using (var admin = new NpgsqlConnection(mantenimiento))
            {
                await admin.OpenAsync(cancellationToken);
                await AcreditarConexionAdministrativaAsync(admin, entrada, cancellationToken);
                await using (var existe = new NpgsqlCommand(
                                 "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = $1)", admin))
                {
                    existe.Parameters.AddWithValue(nombre);
                    Assert.False((bool)(await existe.ExecuteScalarAsync(cancellationToken))!,
                        $"La base temporal generada ya existía y no se considera propiedad de esta prueba: {nombre}.");
                }

                // A partir de aquí el nombre se acreditó ausente y es exclusivo. No se pasa el
                // token al CREATE: una cancelación entre el commit del servidor y la respuesta
                // del cliente no puede dejar una base creada que el finally crea que no existe.
                cancellationToken.ThrowIfCancellationRequested();
                creada = true;
                await using var crear = new NpgsqlCommand($"CREATE DATABASE \"{nombre}\"", admin);
                await crear.ExecuteNonQueryAsync(CancellationToken.None);
            }

            await PrepararMonedaPorHostRealAsync(propia, cancellationToken);
            await escenario(propia);
        }
        catch (Exception exception)
        {
            errorOriginal = exception;
        }

        try
        {
            if (creada)
            {
                AcreditarNombrePropio(nombre);
                NpgsqlConnection.ClearAllPools();
                await using var admin = new NpgsqlConnection(mantenimiento);
                await admin.OpenAsync(CancellationToken.None);
                await AcreditarConexionAdministrativaAsync(admin, entrada, CancellationToken.None);
                await using var borrar = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{nombre}\" WITH (FORCE)", admin);
                await borrar.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }
        catch (Exception errorDeLimpieza)
        {
            if (errorOriginal is null)
            {
                throw;
            }

            errorOriginal.Data["F9.9.2.ErrorDeLimpieza"] = errorDeLimpieza.ToString();
        }

        if (errorOriginal is not null)
        {
            ExceptionDispatchInfo.Capture(errorOriginal).Throw();
        }
    }

    private static void AcreditarServidorDesechable(NpgsqlConnectionStringBuilder entrada)
    {
        var declarada = Environment.GetEnvironmentVariable("SILLAR_VERIFY_DATABASE");
        if (string.IsNullOrWhiteSpace(declarada)
            || !string.Equals(entrada.Database, declarada, StringComparison.Ordinal)
            || !EsNombreCanonico(declarada))
        {
            throw new InvalidOperationException(
                "F9.9.2 solo crea bases si ConnectionStrings__Default apunta exactamente a la base " +
                "efímera declarada por SILLAR_VERIFY_DATABASE con formato sillar_verify_<timestamp>_<pid>.");
        }

        if (!EsHostLoopback(entrada.Host))
        {
            throw new InvalidOperationException(
                $"F9.9.2 rechazó el servidor PostgreSQL '{entrada.Host}': solo se admite loopback desechable.");
        }
    }

    private static async Task AcreditarConexionAdministrativaAsync(
        NpgsqlConnection conexion,
        NpgsqlConnectionStringBuilder entrada,
        CancellationToken cancellationToken)
    {
        await using var comando = new NpgsqlCommand(
            "SELECT current_database(), current_user", conexion);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        Assert.True(await lector.ReadAsync(cancellationToken));
        Assert.Equal("postgres", lector.GetString(0));
        Assert.Equal(entrada.Username, lector.GetString(1));
    }

    private static bool EsHostLoopback(string? host)
        => !string.IsNullOrWhiteSpace(host)
           && (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
               || IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip));

    private static bool EsNombreCanonico(string nombre)
    {
        const string prefijo = "sillar_verify_";
        if (!nombre.StartsWith(prefijo, StringComparison.Ordinal))
        {
            return false;
        }

        var partes = nombre[prefijo.Length..].Split('_');
        return partes is [var instante, var proceso]
               && instante.Length > 0 && proceso.Length > 0
               && instante.All(char.IsAsciiDigit) && proceso.All(char.IsAsciiDigit);
    }

    private static void AcreditarNombrePropio(string nombre)
    {
        if (!EsNombreCanonico(nombre))
        {
            throw new InvalidOperationException($"Nombre temporal no autorizado: {nombre}.");
        }
    }

    private static async Task PrepararMonedaPorHostRealAsync(
        string cadena,
        CancellationToken cancellationToken)
    {
        var medios = Path.Combine(Path.GetTempPath(), $"sillar-f9-9-2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(medios);

        try
        {
            await CompletarInstalacionAsync(cadena, medios, cancellationToken);

            foreach (var modulo in new[] { "catalog", "crm", "sales" })
            {
                await ActivarModuloAsync(cadena, medios, modulo, cancellationToken);
            }

            await AcreditarPenAsync(cadena, medios, cancellationToken);
            await VerificarPersistenciaYAuditoriaAsync(cadena, cancellationToken);
        }
        finally
        {
            if (Directory.Exists(medios))
            {
                Directory.Delete(medios, recursive: true);
            }
        }
    }

    private static async Task CompletarInstalacionAsync(
        string cadena,
        string medios,
        CancellationToken cancellationToken)
    {
        await using var host = LanzarHost(cadena, medios);
        using var http = await host.CrearClienteAsync(cancellationToken);
        using var respuesta = await http.PostAsJsonAsync("/api/setup", new
        {
            businessName = "Instalación temporal F9.9.2",
            licenseType = "trial",
            admin = new
            {
                fullName = "Super Administración F9.9.2",
                email = CorreoSuper,
                password = ClaveSuper
            }
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        await host.EsperarSalidaCorrectaAsync(cancellationToken);
    }

    private static async Task ActivarModuloAsync(
        string cadena,
        string medios,
        string modulo,
        CancellationToken cancellationToken)
    {
        await using var host = LanzarHost(cadena, medios);
        using var http = await host.CrearClienteAsync(cancellationToken);
        var sesion = await EntrarAsync(http, cancellationToken);
        using var peticion = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/modules/{modulo}/activate");
        peticion.Headers.Add("Cookie", $"sillar_panel={sesion.Cookie}");
        peticion.Headers.Add("X-CSRF-Token", sesion.Csrf);
        using var respuesta = await http.SendAsync(peticion, cancellationToken);
        Assert.True(respuesta.IsSuccessStatusCode,
            $"Activar '{modulo}' devolvió {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync(cancellationToken)}");
        await host.EsperarSalidaCorrectaAsync(cancellationToken);
    }

    private static async Task AcreditarPenAsync(
        string cadena,
        string medios,
        CancellationToken cancellationToken)
    {
        await using var host = LanzarHost(cadena, medios);
        using var http = await host.CrearClienteAsync(cancellationToken);
        var sesion = await EntrarAsync(http, cancellationToken);
        using var peticion = new HttpRequestMessage(HttpMethod.Put, "/api/admin/settings/installation-currency")
        {
            Content = JsonContent.Create(new { value = "PEN" })
        };
        peticion.Headers.Add("Cookie", $"sillar_panel={sesion.Cookie}");
        peticion.Headers.Add("X-CSRF-Token", sesion.Csrf);
        using var respuesta = await http.SendAsync(peticion, cancellationToken);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(cuerpo);
        Assert.Equal(nameof(InstallationCurrencyStatus.Accredited), json.RootElement.GetProperty("status").GetString());
        Assert.Equal("PEN", json.RootElement.GetProperty("code").GetString());
    }

    private static async Task<(string Cookie, string Csrf)> EntrarAsync(
        HttpClient http,
        CancellationToken cancellationToken)
    {
        using var respuesta = await http.PostAsJsonAsync("/api/admin/auth/login", new
        {
            email = CorreoSuper,
            password = ClaveSuper
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var setCookie = Assert.Single(respuesta.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("sillar_panel=", StringComparison.Ordinal));
        var fin = setCookie.IndexOf(';', StringComparison.Ordinal);
        var cookie = setCookie["sillar_panel=".Length..fin];
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(cancellationToken));
        return (cookie, json.RootElement.GetProperty("csrfToken").GetString()!);
    }

    private static async Task VerificarPersistenciaYAuditoriaAsync(
        string cadena,
        CancellationToken cancellationToken)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [NodeIdentity.SettingKey] = "principal" })
            .Build();
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddCoreEssentials(configuracion, cadena);
        servicios.AddCoreAuthentication();
        await using var proveedor = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var ambito = proveedor.CreateAsyncScope();
        var lectura = await ambito.ServiceProvider
            .GetRequiredService<IInstallationCurrencyAuthority>()
            .ReadAsync(cancellationToken);
        Assert.Equal(InstallationCurrencyStatus.Accredited, lectura.Status);
        Assert.Equal("PEN", lectura.Currency?.Code);

        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(cancellationToken);
        await using var comando = new NpgsqlCommand(
            """
            SELECT s.setting_value,
                   count(a.audit_log_id)::int,
                   min(a.admin_user_id)::int,
                   min(a.admin_user_email)
            FROM core.site_settings AS s
            LEFT JOIN core.audit_log AS a
              ON a.module_code = 'core'
             AND a.entity_type = 'setting'
             AND a.entity_id = $1
             AND a.action = 'update'
             AND a.summary LIKE '%PEN%'
            WHERE s.setting_key = $1
            GROUP BY s.setting_value
            """, conexion);
        comando.Parameters.AddWithValue(InstallationCurrency.SettingKey);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        Assert.True(await lector.ReadAsync(cancellationToken));
        Assert.Equal("PEN", lector.GetString(0));
        Assert.Equal(1, lector.GetInt32(1));
        Assert.False(lector.IsDBNull(2));
        Assert.Equal(CorreoSuper, lector.GetString(3));
    }

    private static ProcesoHostMoneda LanzarHost(string cadena, string medios)
    {
        var dll = RutaDelHost();
        var inicio = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        inicio.ArgumentList.Add("exec");
        inicio.ArgumentList.Add(dll);
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        inicio.Environment["DOTNET_ENVIRONMENT"] = "Development";
        inicio.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        inicio.Environment["ConnectionStrings__Default"] = cadena;
        inicio.Environment["Media__RootPath"] = medios;
        inicio.Environment["Modules__IncludeDemoModules"] = "false";
        inicio.Environment["Modules__RestartAfterActivation"] = "true";
        inicio.Environment["Sillar__Database__ApplyMigrationsOnStartup"] = "true";
        inicio.Environment["Sillar__Node__Code"] = "principal";
        return new ProcesoHostMoneda(inicio);
    }

    private static string RutaDelHost()
    {
        var tfm = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuracion = tfm.Parent ?? throw new InvalidOperationException("Salida sin configuración.");
        var bin = configuracion.Parent ?? throw new InvalidOperationException("Salida sin bin.");
        var proyecto = bin.Parent ?? throw new InvalidOperationException("bin sin proyecto.");
        var backend = proyecto.Parent ?? throw new InvalidOperationException("Proyecto sin backend.");
        var dll = Path.Combine(backend.FullName, "Sillar.Api", "bin", configuracion.Name, tfm.Name, "Sillar.Api.dll");
        return File.Exists(dll)
            ? dll
            : throw new FileNotFoundException(
                $"Falta el Host compilado del mismo candidato y configuración: {dll}.", dll);
    }

    private sealed class ProcesoHostMoneda : IAsyncDisposable
    {
        private readonly Process proceso;
        private readonly StringBuilder salida = new();
        private readonly TaskCompletionSource<Uri> direccion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ProcesoHostMoneda(ProcessStartInfo inicio)
        {
            proceso = new Process { StartInfo = inicio };
            proceso.OutputDataReceived += (_, args) => Recoger(args.Data);
            proceso.ErrorDataReceived += (_, args) => Recoger(args.Data);
            proceso.Start();
            proceso.BeginOutputReadLine();
            proceso.BeginErrorReadLine();
        }

        public async Task<HttpClient> CrearClienteAsync(CancellationToken cancellationToken)
        {
            var salidaDelProceso = proceso.WaitForExitAsync(cancellationToken);
            var terminado = await Task.WhenAny(direccion.Task, salidaDelProceso);
            if (terminado == salidaDelProceso)
            {
                await salidaDelProceso;
                throw new InvalidOperationException(
                    $"El Host terminó con código {proceso.ExitCode} antes de escuchar.{Environment.NewLine}{Registro()}");
            }

            return new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
            {
                BaseAddress = await direccion.Task
            };
        }

        public async Task EsperarSalidaCorrectaAsync(CancellationToken cancellationToken)
        {
            await proceso.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            Assert.Equal(0, proceso.ExitCode);
        }

        private void Recoger(string? linea)
        {
            if (linea is null)
            {
                return;
            }

            lock (salida)
            {
                salida.AppendLine(linea);
            }

            const string marcador = "Now listening on: ";
            var inicio = linea.IndexOf(marcador, StringComparison.Ordinal);
            if (inicio >= 0 && Uri.TryCreate(linea[(inicio + marcador.Length)..].Trim(), UriKind.Absolute, out var uri))
            {
                direccion.TrySetResult(uri);
            }
        }

        private string Registro()
        {
            lock (salida)
            {
                return salida.ToString();
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!proceso.HasExited)
                {
                    proceso.Kill(entireProcessTree: true);
                    await proceso.WaitForExitAsync(CancellationToken.None);
                }
            }
            catch (InvalidOperationException)
            {
                // Ya terminó entre la comprobación y el cierre.
            }
            finally
            {
                proceso.Dispose();
            }
        }
    }
}
