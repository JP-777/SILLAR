using Microsoft.Extensions.DependencyInjection;
using Sillar.Host.AssistantBridge;

namespace Sillar.Host.AssistantBridge.Tests;

public sealed class AssistantEditorialFacadeCompositionTests
{
    [Fact]
    public void El_contrato_expone_solo_la_fachada_y_no_el_servicio_EF()
    {
        var services = new ServiceCollection();
        services.AddSingleRevocableAssistantEditorialFacade<
            IEditorialContract,
            FakeEfEditorialService,
            FakeRevocableEditorialFacade>(
            _ => new FakeEfEditorialService(),
            (_, inner) => new FakeRevocableEditorialFacade(inner));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var contract = scope.ServiceProvider.GetRequiredService<IEditorialContract>();
        Assert.IsType<FakeRevocableEditorialFacade>(contract);
        Assert.Same(contract,
            scope.ServiceProvider.GetRequiredService<FakeRevocableEditorialFacade>());
        Assert.IsNotType<FakeEfEditorialService>(contract);
        Assert.Single(services, x => x.ServiceType == typeof(IEditorialContract));
    }

    [Fact]
    public void Registro_directo_o_duplicado_del_servicio_editorial_se_rechaza()
    {
        var services = new ServiceCollection();
        services.AddScoped<IEditorialContract, FakeEfEditorialService>();

        var error = Assert.Throws<InvalidOperationException>(() =>
            services.AddSingleRevocableAssistantEditorialFacade<
                IEditorialContract,
                FakeEfEditorialService,
                FakeRevocableEditorialFacade>(
                _ => new FakeEfEditorialService(),
                (_, inner) => new FakeRevocableEditorialFacade(inner)));

        Assert.Contains("exactamente una fachada editorial revocable", error.Message);
    }

    [Fact]
    public void Persistencia_y_fachada_no_pueden_ser_el_mismo_tipo()
    {
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(() =>
            services.AddSingleRevocableAssistantEditorialFacade<
                IEditorialContract,
                FakeEfEditorialService,
                FakeEfEditorialService>(
                _ => new FakeEfEditorialService(),
                (_, inner) => inner));

        Assert.Contains("no puede ser el servicio de persistencia", error.Message);
    }

    private interface IEditorialContract;

    private sealed class FakeEfEditorialService : IEditorialContract;

    private sealed class FakeRevocableEditorialFacade(FakeEfEditorialService inner)
        : IEditorialContract
    {
        public FakeEfEditorialService Inner { get; } = inner;
    }
}
