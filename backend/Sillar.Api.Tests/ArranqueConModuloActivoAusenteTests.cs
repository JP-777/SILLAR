using System.Net;
using System.Net.Http.Json;

using static Sillar.Api.Tests.HostDePrueba;

namespace Sillar.Api.Tests;

/// <summary>
/// El aborto de la ADR-019, observado en el host de verdad.
///
/// <para>
/// <b>Qué promete la ADR-019.</b> Un módulo marcado activo en la base que el
/// binario desplegado no trae no es una instalación degradada: es un despliegue
/// incompleto. El host tiene que negarse a arrancar y decir cuál falta, en vez
/// de levantar un sistema al que le faltan rutas sin que nadie se entere.
/// </para>
/// <para>
/// <b>Por qué no bastaba lo que ya había.</b>
/// <c>ModuleActiveButUndeclaredTests</c> comprueba la función que compara las
/// dos listas, y está bien comprobada; pero una función que devuelve el nombre
/// correcto no demuestra que el proceso se caiga. Entre las dos cosas hay un
/// <c>catch</c>, un código de salida y un servidor que podría haber abierto el
/// puerto igualmente. <b>La afirmación pendiente era sobre el host negándose a
/// arrancar</b>, y eso solo se ve arrancándolo.
/// </para>
/// <para>
/// <b>Lo que esta prueba hace y lo que deliberadamente no hace.</b> No llama a
/// <c>ModuleGraph.ActiveButUndeclared</c>, no construye el host dentro del
/// arnés y no busca el mensaje en el código fuente. Instala de verdad, arranca
/// de verdad dos veces contra la misma base y compara: entre el arranque que
/// funciona y el que aborta <b>cambia una fila</b>.
/// </para>
/// </summary>
public sealed class ArranqueConModuloActivoAusenteTests
{
    /// <summary>
    /// Un código que ningún binario de este repositorio declara.
    /// </summary>
    /// <remarks>
    /// Es el caso real que destapó la ADR-019 y el mismo que usa la prueba de
    /// la función. Los módulos de mentira se llaman <c>demo_*</c>
    /// (<c>DemoModule.cs:102</c>), así que <c>sales</c> no aparece ni siquiera
    /// encendiendo los de demostración.
    /// </remarks>
    private const string CodigoAusente = "sales";

    private static CancellationTokenSource Limite(CancellationToken ct, int segundos)
    {
        var fuente = CancellationTokenSource.CreateLinkedTokenSource(ct);
        fuente.CancelAfter(TimeSpan.FromSeconds(segundos));
        return fuente;
    }

    [Fact]
    public async Task Un_modulo_activo_que_el_binario_no_trae_impide_arrancar_al_host()
    {
        var ct = TestContext.Current.CancellationToken;

        // Los medios van a una carpeta temporal de esta corrida: el host crea
        // la suya al escribir, y no tiene por qué ser una del repositorio.
        var medios = Path.Combine(Path.GetTempPath(), $"sillar-adr019-{Guid.NewGuid():N}");
        Directory.CreateDirectory(medios);

        try
        {
            await ConBaseVaciaAsync(async cadena =>
            {
                await InstalarAsync(cadena, medios, ct);
                await ArrancaYSirveAsync(cadena, medios, ct);

                // --- El único cambio entre el verde de arriba y el rojo de
                // abajo: una fila de catálogo y su activación. Ni el binario,
                // ni la base, ni la configuración cambian.
                await MarcarActivoUnModuloAusenteAsync(cadena, ct);

                await AbortaNombrandoloAsync(cadena, medios, ct);
            }, ct);
        }
        finally
        {
            Directory.Delete(medios, recursive: true);
        }
    }

    /// <summary>
    /// Instala por el camino de verdad: modo instalación y <c>POST /api/setup</c>.
    /// </summary>
    /// <remarks>
    /// Se instala con el producto y no con <c>INSERT</c>s porque el estado que
    /// hace falta —migraciones aplicadas de todos los módulos y una fila de
    /// <c>core.installation</c> completa— es exactamente lo que el instalador
    /// produce. Fabricarlo a mano probaría el arranque contra una base que
    /// ninguna instalación real produciría.
    ///
    /// Al terminar, el host se detiene solo: completar la instalación cambia el
    /// modo de arranque (<c>HostRestarter.StopAfterResponse</c>), así que el
    /// código 0 de aquí ya dice que el primer arranque hizo su trabajo entero.
    /// </remarks>
    private static async Task InstalarAsync(string cadena, string medios, CancellationToken ct)
    {
        using var limite = Limite(ct, segundos: 180);
        await using var instalacion = Lanzar(cadena, medios);

        var puerto = await instalacion.PuertoAsync(limite.Token);

        using var cliente = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto}") };

        var estado = await cliente.GetStringAsync("/api/setup/status", limite.Token);
        Assert.Contains("\"setupRequired\":true", estado.Replace(" ", string.Empty));

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/setup",
            new
            {
                businessName = "Negocio de prueba",
                licenseType = "trial",
                admin = new
                {
                    fullName = "Persona Que Instala",
                    email = "instala@ejemplo.test",
                    password = "Contrasena-Larga-2026",
                },
            },
            limite.Token);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var codigo = await instalacion.CodigoDeSalidaAsync(limite.Token);
        Assert.Equal(0, codigo);
    }

    /// <summary>
    /// El contraste positivo, y va <b>antes</b> del caso incoherente a propósito.
    /// </summary>
    /// <remarks>
    /// Si fuera después, un rojo del caso incoherente podría ser cualquier cosa
    /// —una conexión mal, un binario sin compilar, un puerto ocupado— y la
    /// prueba lo contaría como la ADR-019 funcionando. Arrancando primero con
    /// las activaciones coherentes queda demostrado que este host, contra esta
    /// base, arranca y sirve. Lo de después solo puede ser lo que cambió.
    /// </remarks>
    private static async Task ArrancaYSirveAsync(string cadena, string medios, CancellationToken ct)
    {
        using var limite = Limite(ct, segundos: 120);
        await using var normal = Lanzar(cadena, medios);

        var puerto = await normal.PuertoAsync(limite.Token);

        using var cliente = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto}") };
        var estado = await cliente.GetStringAsync("/api/setup/status", limite.Token);

        Assert.Contains("\"setupRequired\":false", estado.Replace(" ", string.Empty));
        Assert.Contains("Módulos activos", normal.Registro);
    }

    /// <summary>Deja la base diciendo que hay un módulo activo que no existe.</summary>
    /// <remarks>
    /// <c>module_id</c> es <c>GENERATED ALWAYS AS IDENTITY</c> (ADR-016), así
    /// que la activación se cuelga del que asigne PostgreSQL y no de un número
    /// inventado aquí.
    /// </remarks>
    private static async Task MarcarActivoUnModuloAusenteAsync(string cadena, CancellationToken ct)
    {
        await EjecutarAsync(
            cadena,
            $"""
             INSERT INTO core.modules (code, display_name, description, version, is_core, display_order)
             VALUES ('{CodigoAusente}', 'Ventas', 'Activo en la base y ausente del binario.', '1.0.0', false, 90);

             INSERT INTO core.module_activations (module_id, is_active, activated_at)
             SELECT module_id, true, now() FROM core.modules WHERE code = '{CodigoAusente}';
             """,
            ct);

        // La preparación se comprueba: una prueba que da por hecho el estado
        // que ella misma dejó puede pasar en verde sin haber preparado nada.
        var activo = await EscalarAsync(
            cadena,
            $"""
             SELECT a.is_active FROM core.module_activations a
             JOIN core.modules m ON m.module_id = a.module_id
             WHERE m.code = '{CodigoAusente}'
             """,
            ct);

        Assert.Equal(true, activo);
    }

    /// <summary>El arranque que tiene que no ocurrir.</summary>
    private static async Task AbortaNombrandoloAsync(string cadena, string medios, CancellationToken ct)
    {
        using var limite = Limite(ct, segundos: 120);
        await using var roto = Lanzar(cadena, medios);

        // 1 · No llega a servir. Se pregunta por lo primero que ocurra —puerto
        //     abierto o proceso terminado— para que, si la barrera se rompiera,
        //     el rojo dijera «abrió el puerto» en el momento en que lo abre.
        var puerto = await roto.EscuchoOTerminoAsync(limite.Token);
        var registro = roto.Registro;

        Assert.True(
            puerto is null,
            $"El host abrió el puerto {puerto} en vez de abortar: con un módulo activo ausente del "
            + $"binario estaría sirviendo sin sus rutas.{Environment.NewLine}{registro}");

        // 2 · Y termina mal: un despliegue incompleto no puede parecer una
        //     parada limpia a los ojos del orquestador.
        var codigo = await roto.CodigoDeSalidaAsync(limite.Token);
        registro = roto.Registro;

        Assert.True(
            codigo == 1,
            $"El host terminó con código {codigo} y se esperaba 1.{Environment.NewLine}{registro}");

        Assert.DoesNotContain("Now listening", registro);

        // 3 · Dice qué pasa, y lo dice nombrando al que falta: un código de
        //     salida sin causa obliga a adivinar.
        Assert.Contains("SILLAR no puede arrancar", registro);
        Assert.Contains($"ausentes de este binario: {CodigoAusente}", registro);
        Assert.DoesNotContain("Módulos activos", registro);

        // 4 · Y lo clasifica: no es una instalación a la que le falta un
        //     módulo, es una imagen construida a medias.
        Assert.Contains("Es un despliegue incompleto", registro);
    }
}
