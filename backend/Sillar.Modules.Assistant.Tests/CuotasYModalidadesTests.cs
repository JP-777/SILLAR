using Sillar.Modules.Assistant.Domain;

namespace Sillar.Modules.Assistant.Tests;

public class CuotasYModalidadesTests
{
    private readonly Guid _installation = Guid.NewGuid();

    private ResourceBudgetSnapshot Snapshot(AssistantMode mode = AssistantMode.Local) =>
        new(_installation, mode, true, 100, 10, 20, 30, 2, 1);

    [Fact]
    public void FaqNoNecesitaGpuNiApi()
    {
        Assert.Equal(AdmissionPlan.FaqOnly,
            AssistantUsePolicy.Evaluate(_installation, Snapshot(AssistantMode.Faq) with { EngineConfigured = false }));
    }

    [Fact]
    public void LocalConfiguradoPermiteSoloPlanLocal()
    {
        Assert.Equal(AdmissionPlan.LocalAllowedBySnapshot,
            AssistantUsePolicy.Evaluate(_installation, Snapshot()));
    }

    [Fact]
    public void ApiConfiguradaPermiteSoloPlanApi()
    {
        Assert.Equal(AdmissionPlan.ApiAllowedBySnapshot,
            AssistantUsePolicy.Evaluate(_installation, Snapshot(AssistantMode.ExternalApi)));
    }

    [Fact]
    public void SinMotorSoloFaq()
    {
        Assert.Equal(AdmissionPlan.FaqFallback,
            AssistantUsePolicy.Evaluate(_installation, Snapshot() with { EngineConfigured = false }));
    }

    [Fact]
    public void CuotaAgotadaSoloFaq()
    {
        Assert.Equal(AdmissionPlan.FaqFallback,
            AssistantUsePolicy.Evaluate(_installation, Snapshot() with { ConsumedUnits = 70, ReservedUnits = 20 }));
    }

    [Fact]
    public void SaturacionConcurrenteSoloFaq()
    {
        Assert.Equal(AdmissionPlan.FaqFallback,
            AssistantUsePolicy.Evaluate(_installation, Snapshot() with { RunningRequests = 2 }));
    }

    [Fact]
    public void NoConfundeInstalaciones()
    {
        Assert.Equal(AdmissionPlan.InvalidConfiguration,
            AssistantUsePolicy.Evaluate(Guid.NewGuid(), Snapshot()));
    }

    [Fact]
    public void NumerosNegativosNoAutorizanGasto()
    {
        Assert.Equal(AdmissionPlan.InvalidConfiguration,
            AssistantUsePolicy.Evaluate(_installation, Snapshot() with { ReservedUnits = -1 }));
    }

    [Fact]
    public void EnterosExtremosNoDesbordanPresupuesto()
    {
        Assert.Equal(AdmissionPlan.FaqFallback,
            AssistantUsePolicy.Evaluate(_installation, Snapshot() with
            { LimitUnits = long.MaxValue, ConsumedUnits = long.MaxValue, ReservedUnits = 1, WorstCaseNewUnits = 2 }));
    }
}
