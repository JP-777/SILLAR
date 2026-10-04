namespace Sillar.Api.Tests;

/// <summary>
/// Todo módulo del repositorio llega al despliegue del host.
/// </summary>
/// <remarks>
/// <para>
/// <b>La barrera que no existía.</b> <c>ModuleDiscovery</c> no busca módulos en el
/// repositorio: recorre los archivos <c>Sillar.*.dll</c> de la carpeta del
/// despliegue (<c>ModuleDiscovery.cs:49</c>), porque los módulos se publican junto
/// al host (ADR-002). De ahí sale una consecuencia incómoda: <b>un módulo al que
/// nadie le haya puesto su <c>ProjectReference</c> en <c>Sillar.Api.csproj</c>
/// compila, pasa sus propias pruebas y no existe para el producto</b> —su DLL nunca
/// se copia—, y no hay rojo en ninguna parte. Lo descubres cuando abres el panel y
/// el módulo no está.
/// </para>
/// <para>
/// <b>Es §2 de <c>ANTES-DE-EMPEZAR-UN-MODULO.md</c>: una barrera que nunca ha dicho
/// no.</b> El 4 de octubre de 2026, con M07 recompuesto sobre <c>main</c>, la puerta
/// daba verde y M07 no estaba en el host. Lo mismo le pasó a M03 el 3 de octubre con
/// la lista de migraciones de la puerta (§j).
/// </para>
/// <para>
/// <b>Se afirma de todos y no del que falta hoy.</b> La lista de módulos se deduce
/// de las carpetas del repositorio, no se escribe: ponerle a esta prueba el nombre
/// de M07 sería la tercera señal del §1 —algo transversal con el nombre del único
/// que lo usa— y mañana tocaría volver a tocarla.
/// </para>
/// </remarks>
public sealed class ModulosEnElDespliegueTests
{
    /// <summary>
    /// El módulo de mentira, que <b>no</b> tiene que estar en un despliegue.
    /// </summary>
    /// <remarks>
    /// Su <c>ProjectReference</c> está condicionado a Debug a propósito
    /// (<c>Sillar.Api.csproj:28</c>, primera barrera de la entrega 4a §0), así que
    /// exigirlo haría fallar esta prueba en Release por cumplir la regla. Que en
    /// Release no llegue es asunto de esa barrera, no de ésta.
    /// </remarks>
    private const string Mentira = "Sillar.Modules.Demo";

    /// <summary>
    /// Los proyectos de módulo del repositorio: <c>Sillar.Modules.&lt;X&gt;</c> sin
    /// más puntos. Quedan fuera los <c>.Contracts</c> —que viajan por ser
    /// referencia de quien los usa— y los <c>.Tests</c>, que no se despliegan.
    /// </summary>
    private static string[] ProyectosDeModulo(DirectoryInfo backend)
        => [.. backend.EnumerateDirectories("Sillar.Modules.*")
            .Select(d => d.Name)
            .Where(n => n.Count(c => c == '.') == 2 && n != Mentira)
            .Order(StringComparer.Ordinal)];

    [Fact]
    public void Cada_modulo_del_repositorio_tiene_su_ensamblado_junto_al_host()
    {
        var host = new FileInfo(HostDePrueba.RutaDelBinario());
        var despliegue = host.Directory!;
        var backend = despliegue.Parent!.Parent!.Parent!.Parent!;

        var proyectos = ProyectosDeModulo(backend);

        // La preparación se comprueba: si el barrido no encontrara ninguna carpeta
        // —por un cambio de nombre, o por leer el directorio equivocado— esta prueba
        // pasaría en verde sin haber mirado nada.
        Assert.True(
            proyectos.Length >= 4,
            $"Solo {proyectos.Length} proyectos de módulo en '{backend.FullName}': el barrido está mirando donde no debe.");

        var ausentes = proyectos.Where(p => !File.Exists(Path.Combine(despliegue.FullName, $"{p}.dll"))).ToArray();

        Assert.True(
            ausentes.Length == 0,
            $"Estos módulos no llegan al despliegue del host: {string.Join(", ", ausentes)}.{Environment.NewLine}"
            + $"El host descubre módulos mirando los Sillar.*.dll de '{despliegue.FullName}', así que un "
            + "módulo sin ProjectReference en backend/Sillar.Api/Sillar.Api.csproj compila, pasa sus pruebas "
            + "y no existe para el producto. Añade ahí su ProjectReference, y en ningún otro sitio.");
    }
}
