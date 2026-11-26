using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;
using Xunit;

namespace Sillar.Modules.Assistant.Tests;

/// <summary>
/// P7: impide validar enumeraciones inválidas por truncamiento de int a short.
/// No requiere base de datos, host real, DI ni API pública.
/// </summary>
public sealed class DurableQuotaPolicyP7Tests
{
    [Theory]
    [InlineData(65536)] // Int32 -> short 0, se haría pasar por FAQ
    [InlineData(65537)] // Int32 -> short 1, se haría pasar por Local
    [InlineData(65538)] // Int32 -> short 2, se haría pasar por ExternalApi
    [InlineData(-65536)] // Int32 -> short 0
    [InlineData(-65535)] // Int32 -> short 1
    [InlineData(131072)] // otro múltiplo que truncaba a 0
    public void ModosNoDefinidosNuncaSeAdmiten(int raw)
    {
        var policy = new DurableQuotaPolicyChange((AssistantMode)raw, false, 100, 2);
        Assert.False(DurableAssistantQuotaPolicyStore.ValidPolicy(policy));
    }

    [Fact]
    public void CASNoPuedeAplicarUnModoQueSeTruncariaAFaq()
    {
        var policy = new DurableQuotaPolicyChange((AssistantMode)65536, false, 100, 2);
        Assert.False(DurableAssistantQuotaPolicyStore.FitsOutstanding(policy, 0, 0, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ModosDefinidosSiguenSiendoAdmisibles(int raw)
    {
        var policy = new DurableQuotaPolicyChange((AssistantMode)raw, false, 100, 2);
        Assert.True(DurableAssistantQuotaPolicyStore.ValidPolicy(policy));
    }
}
