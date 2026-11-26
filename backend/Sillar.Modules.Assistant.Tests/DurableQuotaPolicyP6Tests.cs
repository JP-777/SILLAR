using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;
using Xunit;

namespace Sillar.Modules.Assistant.Tests;

public sealed class DurableQuotaPolicyP6Tests
{
    [Fact]
    public void NingunaPoliticaNulaEsValida() =>
        Assert.False(DurableAssistantQuotaPolicyStore.ValidPolicy(null));

    [Fact]
    public void FaqNoPuedeHabilitarMotor() =>
        Assert.False(DurableAssistantQuotaPolicyStore.ValidPolicy(new((AssistantMode)0, true, 10, 1)));

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(32767)]
    public void ModosFueraDeEnumeracionSeRechazan(int value) =>
        Assert.False(DurableAssistantQuotaPolicyStore.ValidPolicy(new((AssistantMode)value, false, 10, 1)));

    [Fact]
    public void LimiteNegativoSeRechaza() =>
        Assert.False(DurableAssistantQuotaPolicyStore.ValidPolicy(new(AssistantMode.ExternalApi, true, -1, 1)));

    [Fact]
    public void ConcurrenciaNegativaSeRechaza() =>
        Assert.False(DurableAssistantQuotaPolicyStore.ValidPolicy(new(AssistantMode.ExternalApi, true, 10, -1)));

    [Fact]
    public void PresupuestoDisponibleNoPuedeCaerBajoReservas() =>
        Assert.False(DurableAssistantQuotaPolicyStore.FitsOutstanding(
            new(AssistantMode.ExternalApi, true, 14, 4), 6, 9, 2));

    [Fact]
    public void LimiteConcurrenteNoPuedeCaerBajoRunning() =>
        Assert.False(DurableAssistantQuotaPolicyStore.FitsOutstanding(
            new(AssistantMode.ExternalApi, true, 100, 1), 6, 9, 2));

    [Fact]
    public void LimitesQueConservanContadoresSonValidos() =>
        Assert.True(DurableAssistantQuotaPolicyStore.FitsOutstanding(
            new(AssistantMode.ExternalApi, true, 15, 2), 6, 9, 2));

    [Fact]
    public void FaqConMotorDeshabilitadoPermiteRevocacionDeNuevosConsumos() =>
        Assert.True(DurableAssistantQuotaPolicyStore.FitsOutstanding(
            new((AssistantMode)0, false, 15, 2), 6, 9, 2));

    [Fact]
    public void SumaDeContadoresNoDesbordaPorUsarRestaSegura() =>
        Assert.False(DurableAssistantQuotaPolicyStore.FitsOutstanding(
            new(AssistantMode.ExternalApi, true, long.MaxValue, 2),
            long.MaxValue, 1, 0));
}
