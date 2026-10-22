using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Assistant.Composition;
using Sillar.Modules.Assistant.Endpoints;
using Xunit;

namespace Sillar.Modules.Assistant.Tests;

/// <summary>
/// P11: prueba exclusivamente el cableado opt-in, sin DB ni autoridad CORE real.
/// La integración comercial y CAS transaccional permanecen en HOLD.
/// </summary>
public sealed class AssistantQuotaCompositionP11Tests
{
    private static readonly Guid Installation =
        Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    private sealed class FixedHostEvidence(Guid? installation, bool configured, bool active, bool licensed)
        : ITrustedAssistantHostEvidence
    {
        public bool IsInstallationConfigured => configured;
        public bool IsAssistantActive => active;
        public bool IsAssistantLicensed => licensed;
        public Guid? InstallationId => installation;
    }

    [Fact]
    public void Sin_productor_host_no_inventa_autoridad()
    {
        using var provider = new ServiceCollection()
            .AddAssistantQuotaInternals()
            .BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<ITrustedAssistantInstallationScope>());
    }

    [Fact]
    public void Evidencia_host_negativa_deniega_sin_fallback()
    {
        var services = new ServiceCollection().AddAssistantQuotaInternals();
        services.AddScoped<ITrustedAssistantHostEvidence>(_ =>
            new FixedHostEvidence(Installation, true, true, false));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var authorized = scope.ServiceProvider
            .GetRequiredService<ITrustedAssistantInstallationScope>()
            .TryGetEnabledInstallation(out var id);
        Assert.False(authorized);
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void Evidencia_host_positiva_es_por_scope_y_no_activa_rutas()
    {
        var services = new ServiceCollection().AddAssistantQuotaInternals();
        services.AddScoped<ITrustedAssistantHostEvidence>(_ =>
            new FixedHostEvidence(Installation, true, true, true));
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider
            .GetRequiredService<ITrustedAssistantInstallationScope>();
        var second = secondScope.ServiceProvider
            .GetRequiredService<ITrustedAssistantInstallationScope>();
        Assert.NotSame(first, second);
        Assert.True(first.TryGetEnabledInstallation(out var id));
        Assert.Equal(Installation, id);
    }
}
