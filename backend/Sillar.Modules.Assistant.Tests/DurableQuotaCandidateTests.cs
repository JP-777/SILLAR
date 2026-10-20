using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public class DurableQuotaCandidateTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("key\nnew-line")]
    [InlineData("key\twith-tab")]
    public void ClaveIdempotenciaInvalidaSeRechaza(string? key) =>
        Assert.False(DurableAssistantQuotaLedger.ValidIdempotencyKey(key));

    [Fact]
    public void ClaveIdempotenciaDe128CaracteresEsValida() =>
        Assert.True(DurableAssistantQuotaLedger.ValidIdempotencyKey(new string('a', 128)));

    [Fact]
    public void ClaveIdempotenciaDe129CaracteresSeRechaza() =>
        Assert.False(DurableAssistantQuotaLedger.ValidIdempotencyKey(new string('a', 129)));

    [Fact]
    public void PresupuestoExcedidoNuncaAutorizaMotor()
    {
        var id = Guid.NewGuid();
        var result = AssistantUsePolicy.Evaluate(id,
            new(id, AssistantMode.ExternalApi, true, 100, 45, 50, 6, 2, 1));
        Assert.Equal(AdmissionPlan.FaqFallback, result);
    }

    [Fact]
    public void ConcurrenciaSaturadaNuncaAutorizaMotor()
    {
        var id = Guid.NewGuid();
        var result = AssistantUsePolicy.Evaluate(id,
            new(id, AssistantMode.Local, true, 100, 0, 10, 5, 1, 1));
        Assert.Equal(AdmissionPlan.FaqFallback, result);
    }

    [Fact]
    public void InstalacionAjenaNoPuedeAprobarPlanDeConsumo()
    {
        var id = Guid.NewGuid();
        var result = AssistantUsePolicy.Evaluate(Guid.NewGuid(),
            new(id, AssistantMode.ExternalApi, true, 100, 0, 0, 5, 3, 0));
        Assert.Equal(AdmissionPlan.InvalidConfiguration, result);
    }
}
