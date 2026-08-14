using Sillar.Shared.Modularity;
using static Sillar.Shared.Tests.Instalacion;

namespace Sillar.Shared.Tests;

/// <summary>
/// Ninguna operación que el endpoint permita puede dejar el sistema sin arrancar.
/// </summary>
/// <remarks>
/// Es la prueba que sostiene el §2 de la entrega 3. El endpoint de activación
/// escribe y acto seguido el host se detiene para relanzarse; si aceptara un
/// estado que el validador de arranque rechaza, la instalación quedaría muerta y
/// solo se recuperaría entrando por SQL.
///
/// En lugar de comprobar unos cuantos casos elegidos a mano, se recorren
/// <b>todos</b> los estados alcanzables del grafo aplicando cada operación
/// permitida, y se comprueba que el validador del arranque acepta todos.
/// </remarks>
public class ModuleActivationSafetyTests
{
    /// <summary>
    /// Un grafo con las formas que importan: cadena de tres, dependencia dura
    /// cruzada, dependencia blanda y un módulo suelto.
    /// </summary>
    private static IReadOnlyList<IModule> Instalacion() =>
    [
        Core(),
        Modulo("catalog", orden: 1),
        Modulo("crm", orden: 2),
        Modulo("sales", duras: ["catalog"], blandas: ["crm"], orden: 3),
        Modulo("services", orden: 4),
        Modulo("tracking", duras: ["services"], orden: 5)
    ];

    /// <summary>
    /// Reproduce lo que el endpoint permite: la operación se acepta solo si el
    /// validador del arranque aprueba el estado resultante.
    /// </summary>
    private static bool EndpointLoPermite(
        IReadOnlyList<IModule> modules,
        IReadOnlySet<string> activos,
        string codigo,
        bool activar)
    {
        var resultante = ModuleGraph.ResultingActiveCodes(activos, codigo, activar);
        return ModuleGraph.ValidateActivations(modules, resultante).Count == 0;
    }

    [Fact]
    public void Ninguna_secuencia_de_operaciones_permitidas_deja_el_sistema_sin_arrancar()
    {
        var modules = Instalacion();
        var codigos = modules.Select(modulo => modulo.Code).ToList();

        // Recorrido en anchura sobre los estados alcanzables desde «solo CORE».
        var inicial = new HashSet<string>(["core"], StringComparer.OrdinalIgnoreCase);
        var vistos = new HashSet<string> { Clave(inicial) };
        var pendientes = new Queue<HashSet<string>>([inicial]);
        var estadosExplorados = 0;

        while (pendientes.Count > 0)
        {
            var actual = pendientes.Dequeue();
            estadosExplorados++;

            // Invariante: todo estado alcanzable por operaciones permitidas
            // tiene que arrancar.
            Assert.Empty(ModuleGraph.ValidateActivations(modules, actual));

            foreach (var codigo in codigos)
            {
                foreach (var activar in (bool[])[true, false])
                {
                    if (!EndpointLoPermite(modules, actual, codigo, activar))
                    {
                        continue;
                    }

                    var siguiente = new HashSet<string>(
                        ModuleGraph.ResultingActiveCodes(actual, codigo, activar),
                        StringComparer.OrdinalIgnoreCase);

                    if (vistos.Add(Clave(siguiente)))
                    {
                        pendientes.Enqueue(siguiente);
                    }
                }
            }
        }

        // El número exacto, no un mínimo: si alguien toca el grafo de prueba o
        // las reglas de activación, esta cuenta cambia y hay que mirar por qué.
        //
        // Son 19: el estado vacío, más 18 con CORE activo. Esos 18 salen de
        // multiplicar las combinaciones válidas de cada rama —{}, {catalog},
        // {catalog,sales} son 3; {}, {services}, {services,tracking} son otras
        // 3— por los dos estados de Clientes, que no condiciona a nadie.
        Assert.Equal(19, estadosExplorados);
    }

    [Fact]
    public void Activar_con_una_dependencia_dura_inactiva_no_esta_permitido()
    {
        var modules = Instalacion();
        var activos = Activos("core");

        Assert.False(EndpointLoPermite(modules, activos, "sales", activar: true));
        Assert.Equal(["catalog"], ModuleGraph.MissingHardDependencies(modules, activos, "sales"));
    }

    [Fact]
    public void Activar_con_las_dependencias_duras_activas_si_esta_permitido()
    {
        var modules = Instalacion();

        Assert.True(EndpointLoPermite(modules, Activos("core", "catalog"), "sales", activar: true));
    }

    [Fact]
    public void Desactivar_un_modulo_del_que_otro_activo_depende_duro_no_esta_permitido()
    {
        var modules = Instalacion();
        var activos = Activos("core", "catalog", "sales");

        Assert.False(EndpointLoPermite(modules, activos, "catalog", activar: false));
        Assert.Equal(["sales"], ModuleGraph.ActiveHardDependents(modules, activos, "catalog"));
    }

    [Fact]
    public void Una_dependencia_blanda_inactiva_no_impide_activar()
    {
        var modules = Instalacion();

        // Ventas declara a Clientes como blanda: funciona sin él, guardando los
        // datos de contacto como snapshot.
        Assert.True(EndpointLoPermite(modules, Activos("core", "catalog"), "sales", activar: true));
    }

    [Fact]
    public void Una_dependencia_blanda_no_impide_desactivar_al_otro()
    {
        var modules = Instalacion();
        var activos = Activos("core", "catalog", "crm", "sales");

        Assert.True(EndpointLoPermite(modules, activos, "crm", activar: false));
        Assert.Empty(ModuleGraph.ActiveHardDependents(modules, activos, "crm"));
    }

    [Fact]
    public void Desactivar_CORE_dejaria_a_todos_los_activos_sin_su_dependencia_dura()
    {
        var modules = Instalacion();

        // El endpoint lo rechaza antes por is_core, pero el grafo también lo
        // impide: es la segunda barrera.
        Assert.False(EndpointLoPermite(modules, Activos("core", "catalog"), "core", activar: false));
    }

    [Fact]
    public void Desactivar_en_orden_inverso_a_las_dependencias_si_esta_permitido()
    {
        var modules = Instalacion();
        var activos = Activos("core", "services", "tracking");

        // Primero Seguimiento, que es quien depende.
        Assert.True(EndpointLoPermite(modules, activos, "tracking", activar: false));

        var sinTracking = ModuleGraph.ResultingActiveCodes(activos, "tracking", activate: false);
        Assert.True(EndpointLoPermite(modules, sinTracking, "services", activar: false));
    }

    private static IReadOnlySet<string> Activos(params string[] codigos)
        => codigos.ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Representación estable de un estado, para detectar repetidos.</summary>
    private static string Clave(IEnumerable<string> activos)
        => string.Join(",", activos.Order(StringComparer.Ordinal));
}
