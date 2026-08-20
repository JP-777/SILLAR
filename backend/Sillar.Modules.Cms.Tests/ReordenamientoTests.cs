using Sillar.Modules.Cms.Services;

namespace Sillar.Modules.Cms.Tests;

public sealed class ReordenamientoTests
{
    [Fact]
    public void Cinco_elementos_reciben_exactamente_el_orden_solicitado()
    {
        var plan = OrderPlan.Create([10, 20, 30, 40, 50], [50, 20, 40, 10, 30]);

        Assert.True(plan.IsValid);
        Assert.Equal(
            [(50, 0), (20, 1), (40, 2), (10, 3), (30, 4)],
            plan.Assignments.Select(item => (item.Id, item.DisplayOrder)));
    }

    [Fact]
    public void Un_identificador_inexistente_invalida_el_plan_entero()
    {
        var plan = OrderPlan.Create([10, 20, 30], [30, 999, 10]);

        Assert.False(plan.IsValid);
        Assert.Empty(plan.Assignments);
    }

    [Fact]
    public void Un_identificador_repetido_no_produce_asignaciones_parciales()
    {
        var plan = OrderPlan.Create([10, 20, 30], [10, 10, 30]);

        Assert.False(plan.IsValid);
        Assert.Empty(plan.Assignments);
    }

    [Fact]
    public void Omitir_un_elemento_invalida_la_lista_completa()
    {
        var plan = OrderPlan.Create([10, 20, 30], [30, 10]);

        Assert.False(plan.IsValid);
        Assert.Empty(plan.Assignments);
    }

    [Fact]
    public void Una_seccion_vacia_acepta_una_lista_vacia()
    {
        var plan = OrderPlan.Create([], []);

        Assert.True(plan.IsValid);
        Assert.Empty(plan.Assignments);
    }
}
