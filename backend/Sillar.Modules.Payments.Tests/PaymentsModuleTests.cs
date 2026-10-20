using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.Payments.Tests;

/// <summary>Construcción y composición aislada del módulo M11.</summary>
public sealed class PaymentsModuleTests
{
    private static IConfiguration Configuration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] =
                    "Host=localhost;Database=sillar_tests;Username=sillar;Password=sillar"
            })
            .Build();

    [Fact]
    public void Manifiesto_declara_core_y_sales_como_dependencias_duras()
    {
        var module = new PaymentsModule();

        Assert.Equal("payments", module.Code);
        Assert.Equal("payments", ((IModule)module).Schema);
        Assert.Equal(["core", "sales"], module.HardDependencies);
        Assert.Empty(module.SoftDependencies);
        Assert.Equal(110, module.DisplayOrder);
    }

    [Fact]
    public void Registro_expone_la_frontera_interna_de_analisis()
    {
        var services = new ServiceCollection();
        var configuration = Configuration();

        new PaymentsModule().RegisterServices(services, configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var analyzer = scope.ServiceProvider
            .GetRequiredService<IProviderSettlementAnalysisService>();
        var evidence = PaymentTestData.Evidence();
        var result = analyzer.Analyze(
            [evidence],
            [],
            [PaymentTestData.Attested(evidence)]);

        Assert.Single(result.Trace);
        Assert.Equal(
            ProviderSettlementReviewLane.RequiresSeparateDurableGate,
            result.Trace[0].ReviewLane);
    }

    [Fact]
    public void Registro_no_publica_puertos_operativos_ni_simuladores()
    {
        var services = new ServiceCollection();

        new PaymentsModule().RegisterServices(
            services,
            Configuration());

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType.Namespace?.Contains(
                ".Simulation",
                StringComparison.Ordinal) == true);
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType.Name.Contains(
                "Provider",
                StringComparison.Ordinal)
                && descriptor.ServiceType.Name.Contains(
                    "Client",
                    StringComparison.Ordinal));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IProviderEvidenceVerifier)
                || descriptor.ServiceType == typeof(IProviderReceptionLedger)
                || descriptor.ServiceType == typeof(IProviderCommercialEffectPort));
    }
}
