using Sillar.Core;
using Sillar.Modules.Catalog;
using Sillar.Modules.Crm;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.B2B.Tests;

/// <summary>
/// La vía administrativa de C6: lo que deciden el panel y el instalador con la
/// declaración real de M07. Sin base de datos: la decisión es pura
/// (<c>ModuleGraph</c>), y el panel y el instalador solo la consultan.
/// </summary>
public sealed class DependenciasDurasTests
{
    private static readonly IModule[] Completo =
        [new CoreModule(), new CatalogModule(), new CrmModule(), new B2BModule()];

    private static HashSet<string> Activos(params string[] codigos) => [.. codigos];

    [Fact]
    public void M07_declara_dependencia_dura_de_core_catalogo_y_clientes()
        => Assert.Equal(["core", "catalog", "crm"], new B2BModule().HardDependencies);

    [Fact]
    public void El_panel_no_activa_M07_si_falta_M01()
        => Assert.Equal(["catalog"], ModuleGraph.MissingHardDependencies(Completo, Activos("core", "crm"), "b2b"));

    [Fact]
    public void El_panel_no_activa_M07_si_falta_M04()
        => Assert.Equal(["crm"], ModuleGraph.MissingHardDependencies(Completo, Activos("core", "catalog"), "b2b"));

    [Fact]
    public void Con_M07_activo_el_panel_no_desactiva_M01_ni_M04()
    {
        var activos = Activos("core", "catalog", "crm", "b2b");

        Assert.Equal(["b2b"], ModuleGraph.ActiveHardDependents(Completo, activos, "catalog"));
        Assert.Equal(["b2b"], ModuleGraph.ActiveHardDependents(Completo, activos, "crm"));
    }

    /// <summary>
    /// El límite de la vía administrativa, afirmado para que no se olvide: el
    /// panel mira lo ACTIVO, no lo INSTALADO. Con M07 desactivado deja apagar
    /// M01 —y está bien, porque apagar no borra nada—. Lo que borra es el
    /// 99_drop.sql, y esa guarda es otra (ver GuardasDeDesinstalacionTests).
    /// </summary>
    [Fact]
    public void Con_M07_inactivo_el_panel_deja_desactivar_M01_porque_desactivar_no_borra()
        => Assert.Empty(ModuleGraph.ActiveHardDependents(Completo, Activos("core", "catalog", "crm"), "catalog"));

    [Fact]
    public void El_instalador_rechaza_un_despliegue_con_M07_y_sin_M01()
    {
        var grafo = ModuleGraph.Validate([new CoreModule(), new CrmModule(), new B2BModule()]);

        Assert.False(grafo.IsValid);
        Assert.Contains("catalog", grafo.DescribeErrors());
    }

    [Fact]
    public void El_instalador_rechaza_un_despliegue_con_M07_y_sin_M04()
    {
        var grafo = ModuleGraph.Validate([new CoreModule(), new CatalogModule(), new B2BModule()]);

        Assert.False(grafo.IsValid);
        Assert.Contains("crm", grafo.DescribeErrors());
    }

    [Fact]
    public void El_instalador_aplica_M07_despues_de_M01_y_M04()
    {
        var orden = ModuleGraph.Validate([new B2BModule(), new CrmModule(), new CatalogModule(), new CoreModule()])
            .InstallationOrder.Select(modulo => modulo.Code).ToList();

        Assert.True(orden.IndexOf("b2b") > orden.IndexOf("catalog"));
        Assert.True(orden.IndexOf("b2b") > orden.IndexOf("crm"));
    }
}
