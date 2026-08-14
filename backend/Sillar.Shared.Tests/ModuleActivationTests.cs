using Sillar.Shared.Modularity;
using static Sillar.Shared.Tests.Instalacion;

namespace Sillar.Shared.Tests;

/// <summary>
/// Coherencia de las activaciones: el paso 6 del arranque.
/// </summary>
/// <remarks>
/// Aquí ya no se juzga el producto sino la instalación concreta. Es la
/// comprobación que impide que Seguimiento funcione con Órdenes de Servicio
/// apagado: si falla, el host aborta en lugar de degradarse en silencio.
/// </remarks>
public class ModuleActivationTests
{
    private static readonly IReadOnlyList<IModule> Modulos =
    [
        Core(),
        Modulo("catalog"),
        Modulo("crm", orden: 2),
        Modulo("sales", duras: ["catalog"], blandas: ["crm"], orden: 3)
    ];

    [Fact]
    public void Con_todas_las_dependencias_duras_activas_no_hay_problema()
    {
        var problemas = ModuleGraph.ValidateActivations(Modulos, Activos("core", "catalog", "sales"));

        Assert.Empty(problemas);
    }

    [Fact]
    public void Un_modulo_activo_con_su_dependencia_dura_apagada_es_un_problema()
    {
        var problemas = ModuleGraph.ValidateActivations(Modulos, Activos("core", "sales"));

        var problema = Assert.Single(problemas);
        Assert.Contains("'sales'", problema);
        Assert.Contains("'catalog'", problema);
    }

    [Fact]
    public void Una_dependencia_blanda_apagada_no_es_un_problema()
    {
        // Ventas funciona sin Clientes: guarda los datos de contacto como
        // snapshot y nadie lo nota.
        var problemas = ModuleGraph.ValidateActivations(Modulos, Activos("core", "catalog", "sales"));

        Assert.DoesNotContain(problemas, problema => problema.Contains("'crm'"));
    }

    [Fact]
    public void Lo_que_esta_apagado_no_se_juzga()
    {
        // Ventas está inactivo, así que da igual que Catálogo también lo esté.
        var problemas = ModuleGraph.ValidateActivations(Modulos, Activos("core"));

        Assert.Empty(problemas);
    }

    [Fact]
    public void Se_reportan_todas_las_dependencias_que_faltan_no_solo_la_primera()
    {
        IReadOnlyList<IModule> instalacion = [Core(), Modulo("catalog"), Modulo("crm", orden: 2), Modulo("portal", duras: ["catalog", "crm"], orden: 4)];

        var problemas = ModuleGraph.ValidateActivations(instalacion, Activos("core", "portal"));

        Assert.Equal(2, problemas.Count);
    }

    private static IReadOnlySet<string> Activos(params string[] codigos) => codigos.ToHashSet();
}
