using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using Sillar.Shared.Configuration;

namespace Sillar.Api.Tests;

/// <summary>
/// Lanza el binario del host contra una base que la prueba crea y destruye.
/// </summary>
/// <remarks>
/// <b>Lo que se lanza es el despliegue, no una función.</b> Un
/// <c>WebApplicationFactory</c> construiría el host dentro del proceso de
/// pruebas, y entonces «el host no arranca» se observaría como una excepción
/// atrapada por el arnés — que es precisamente lo que un despliegue no hace.
/// Aquí se ejecuta <c>dotnet exec Sillar.Api.dll</c> desde su propia carpeta de
/// salida, con su <c>runtimeconfig.json</c> y su <c>appsettings.json</c>, y lo
/// que se observa es lo que vería quien mira los registros del contenedor: qué
/// se imprimió, si llegó a escuchar y con qué código terminó.
/// </remarks>
internal static partial class HostDePrueba
{
    /// <summary>Lo que imprime Kestrel al abrir el puerto. Nunca está traducido.</summary>
    [GeneratedRegex(@"Now listening on:\s*\S+://[^:]+:(\d+)")]
    private static partial Regex Escucha();

    /// <summary>
    /// La carpeta de salida del host, deducida de la de este arnés.
    /// </summary>
    /// <remarks>
    /// Las dos salidas son hermanas —<c>backend/&lt;proyecto&gt;/bin/&lt;configuración&gt;/&lt;tfm&gt;</c>—
    /// así que la configuración y el target framework se leen del propio camino
    /// en vez de escribirse a mano: con <c>-c Release</c> esto sigue apuntando
    /// al binario que se acaba de compilar, no a un Debug viejo de otro día.
    /// </remarks>
    public static string RutaDelBinario()
    {
        var tfm = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuracion = tfm.Parent ?? throw new InvalidOperationException("Salida sin configuración.");
        var bin = configuracion.Parent ?? throw new InvalidOperationException("Salida sin bin.");
        var proyecto = bin.Parent ?? throw new InvalidOperationException("bin sin proyecto.");
        var backend = proyecto.Parent ?? throw new InvalidOperationException("Proyecto sin backend.");

        var dll = Path.Combine(
            backend.FullName, "Sillar.Api", "bin", configuracion.Name, tfm.Name, "Sillar.Api.dll");

        if (!File.Exists(dll))
        {
            throw new FileNotFoundException(
                $"No está el binario del host en '{dll}'. Este proyecto referencia a Sillar.Api, "
                + "así que si esta prueba corre es que se compiló: comprueba la configuración de la corrida.",
                dll);
        }

        return dll;
    }

    /// <summary>Arranca el host contra <paramref name="cadena"/>.</summary>
    /// <param name="cadena">Cadena de conexión de la base de esta prueba.</param>
    /// <param name="mediaRoot">Carpeta de medios, siempre temporal.</param>
    public static ProcesoDelHost Lanzar(string cadena, string mediaRoot)
    {
        var dll = RutaDelBinario();

        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        info.ArgumentList.Add("exec");
        info.ArgumentList.Add(dll);

        // **Producción, no desarrollo.** Es donde la ADR-019 importa, y además
        // quita del medio los módulos de mentira: en Development bastaría una
        // variable heredada de la consola para que 'sales' pasara a estar
        // declarado y la prueba dejara de comprobar nada.
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        info.Environment["DOTNET_ENVIRONMENT"] = "Production";
        info.Environment["Modules__IncludeDemoModules"] = "false";

        // Puerto efímero: dos frentes corriendo a la vez no pueden pelearse por
        // un número fijo, y el que toque se lee de lo que imprime Kestrel.
        info.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";

        // El entorno del proceso gana sobre cualquier .env que el host
        // encuentre subiendo por el árbol: la base es la de esta prueba y no
        // hay forma de que sea otra.
        info.Environment["ConnectionStrings__Default"] = cadena;
        info.Environment["Media__RootPath"] = mediaRoot;

        var proceso = new Process { StartInfo = info };
        return new ProcesoDelHost(proceso, Escucha());
    }

    /// <summary>La cadena del entorno, o <c>null</c> si no hay ninguna.</summary>
    public static string? CadenaDelEntorno()
    {
        DotEnv.Load();
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        return string.IsNullOrWhiteSpace(cadena) ? null : cadena;
    }

    /// <summary>
    /// Crea una base vacía, corre lo que se le pase contra ella y la destruye.
    /// </summary>
    /// <remarks>
    /// Mismo patrón que <c>BaseDePrueba.ConBaseVaciaAsync</c> de
    /// <c>Sillar.Core.Tests</c>, con otro prefijo para que las dos se puedan
    /// distinguir si alguna vez queda huérfana. No se comparte el código porque
    /// aquello es <c>internal</c> de otro ensamblado y abrirlo solo para esto
    /// ataría las pruebas de CORE a las del host.
    /// </remarks>
    public static async Task ConBaseVaciaAsync(Func<string, Task> cuerpo, CancellationToken ct)
    {
        var cadena = CadenaDelEntorno();

        if (cadena is null)
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay servidor donde crear una base vacía.");
            return;
        }

        var nombre = $"sillar_adr019_{Guid.NewGuid():N}";
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
            NpgsqlConnection.ClearAllPools();

            await using var admin = new NpgsqlConnection(mantenimiento);
            await admin.OpenAsync(CancellationToken.None);
            await using var borrar = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{nombre}\" WITH (FORCE)", admin);
            await borrar.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    /// <summary>Ejecuta SQL de preparación. Solo para dejar la base en un estado dado.</summary>
    public static async Task EjecutarAsync(string cadena, string sql, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Lee un escalar. Para comprobar el estado que la prueba preparó.</summary>
    public static async Task<object?> EscalarAsync(string cadena, string sql, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexion);
        return await comando.ExecuteScalarAsync(ct);
    }
}

/// <summary>Un host en marcha, con su salida recogida entera.</summary>
internal sealed class ProcesoDelHost : IAsyncDisposable
{
    private readonly Process proceso;
    private readonly Regex escucha;
    private readonly StringBuilder registro = new();
    private readonly TaskCompletionSource<int> puerto =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ProcesoDelHost(Process proceso, Regex escucha)
    {
        this.proceso = proceso;
        this.escucha = escucha;

        proceso.OutputDataReceived += (_, e) => Recoger(e.Data);
        proceso.ErrorDataReceived += (_, e) => Recoger(e.Data);

        proceso.Start();
        proceso.BeginOutputReadLine();
        proceso.BeginErrorReadLine();
    }

    /// <summary>Todo lo que el proceso ha impreso hasta ahora.</summary>
    public string Registro
    {
        get
        {
            lock (registro)
            {
                return registro.ToString();
            }
        }
    }

    private void Recoger(string? linea)
    {
        if (linea is null)
        {
            return;
        }

        lock (registro)
        {
            registro.AppendLine(linea);
        }

        var encontrado = escucha.Match(linea);

        if (encontrado.Success && int.TryParse(encontrado.Groups[1].Value, out var numero))
        {
            puerto.TrySetResult(numero);
        }
    }

    /// <summary>
    /// El puerto en el que el host empezó a escuchar.
    /// </summary>
    /// <remarks>
    /// Espera a una señal del propio proceso, no a un tiempo: dormir un número
    /// de segundos y confiar convierte una máquina cargada en un rojo
    /// intermitente. Si el proceso termina antes de escuchar, esto falla
    /// diciendo con qué código terminó y qué imprimió.
    /// </remarks>
    public async Task<int> PuertoAsync(CancellationToken ct)
        => await EscuchoOTerminoAsync(ct)
           ?? throw new InvalidOperationException(
               $"El host terminó con código {proceso.ExitCode} sin llegar a escuchar.{Environment.NewLine}{Registro}");

    /// <summary>
    /// Lo primero que ocurra: el puerto abierto, o el proceso terminado.
    /// </summary>
    /// <returns>El puerto si llegó a escuchar; <c>null</c> si terminó antes.</returns>
    /// <remarks>
    /// Existe para que el caso del aborto no se compruebe esperando un tiempo
    /// límite. Si la barrera se rompiera, el host arrancaría y se quedaría
    /// sirviendo: sin esta carrera, el rojo tardaría lo que durase la espera y
    /// llegaría disfrazado de cancelación en vez de decir lo que pasó.
    /// </remarks>
    public async Task<int?> EscuchoOTerminoAsync(CancellationToken ct)
    {
        var terminar = proceso.WaitForExitAsync(ct);
        var ganador = await Task.WhenAny(puerto.Task, terminar);

        if (ganador == terminar)
        {
            await terminar;
            return null;
        }

        return await puerto.Task;
    }

    /// <summary>Espera a que el proceso termine y devuelve su código de salida.</summary>
    public async Task<int> CodigoDeSalidaAsync(CancellationToken ct)
    {
        await proceso.WaitForExitAsync(ct);
        return proceso.ExitCode;
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
            // El proceso ya no existe: no hay nada que matar.
        }
        finally
        {
            proceso.Dispose();
        }
    }
}
