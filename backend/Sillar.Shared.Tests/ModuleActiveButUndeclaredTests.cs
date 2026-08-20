using Sillar.Shared.Modularity;
using static Sillar.Shared.Tests.Instalacion;

namespace Sillar.Shared.Tests;

/// <summary>
/// Un módulo activo en la base que el binario no trae aborta el arranque
/// (ADR-019). Es distinto de <see cref="ModuleActivationTests"/>: aquello
/// juzga coherencia entre módulos que existen en los dos lados; esto detecta
/// el despliegue incompleto que el aviso del arranque dejaba pasar en silencio.
/// </summary>
public class ModuleActiveButUndeclaredTests
{
    private static readonly IReadOnlyList<IModule> Modulos = [Core(), Modulo("catalog")];

    [Fact]
    public void Si_todo_lo_activo_en_la_base_esta_declarado_no_hay_problema()
    {
        var faltantes = ModuleGraph.ActiveButUndeclared(Modulos, ["core", "catalog"]);

        Assert.Empty(faltantes);
    }

    [Fact]
    public void Un_codigo_activo_que_el_binario_no_trae_se_nombra()
    {
        // El caso real que destapó la ADR-019: la imagen se construyó sin
        // Sillar.Modules.Catalog y la base seguía marcándolo activo.
        var faltantes = ModuleGraph.ActiveButUndeclared(Modulos, ["core", "catalog", "sales"]);

        var faltante = Assert.Single(faltantes);
        Assert.Equal("sales", faltante);
    }

    [Fact]
    public void Lo_inactivo_y_ausente_no_es_un_problema()
    {
        // Desinstalado a propósito: is_orphan ya cubre este caso, y sigue
        // siendo legítimo.
        var faltantes = ModuleGraph.ActiveButUndeclared(Modulos, ["core", "catalog"]);

        Assert.DoesNotContain("sales", faltantes);
    }

    [Fact]
    public void Se_nombran_todos_los_que_faltan_no_solo_el_primero()
    {
        var faltantes = ModuleGraph.ActiveButUndeclared(Modulos, ["core", "catalog", "sales", "crm"]);

        Assert.Equal(["crm", "sales"], faltantes);
    }

    [Fact]
    public void Sin_ningun_codigo_activo_no_hay_nada_que_reportar()
    {
        var faltantes = ModuleGraph.ActiveButUndeclared(Modulos, []);

        Assert.Empty(faltantes);
    }
}
