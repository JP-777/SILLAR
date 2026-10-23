using Sillar.Host.AssistantBridge;

namespace Sillar.Host.AssistantBridge.Tests;

public sealed class DenyAllAssistantQuotaPolicyWriterTests
{
    [Fact]
    public void Cas_deniega_identidad_ausente_correcta_o_ajena()
    {
        var writer = new DenyAllAssistantQuotaPolicyWriter();

        Assert.False(writer.CanWritePolicy(Guid.Empty));
        Assert.False(writer.CanWritePolicy(Guid.NewGuid()));
        Assert.False(writer.CanWritePolicy(Guid.NewGuid()));
    }
}
