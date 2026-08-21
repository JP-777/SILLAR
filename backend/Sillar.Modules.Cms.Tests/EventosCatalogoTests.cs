using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Catalog.Contracts.Events;
using Sillar.Shared.Events;

namespace Sillar.Modules.Cms.Tests;

public sealed class EventosCatalogoTests
{
    [Fact]
    public void Los_tres_manejadores_se_registran_como_singleton_para_el_bus_raiz()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=sillar_cms_tests"
            })
            .Build();
        var services = new ServiceCollection();

        new CmsModule().RegisterServices(services, configuration);

        AssertHandlerIsSingleton<ProductoActualizado>(services);
        AssertHandlerIsSingleton<ProductoDesactivado>(services);
        AssertHandlerIsSingleton<CategoriaDesactivada>(services);
    }

    private static void AssertHandlerIsSingleton<TEvent>(IServiceCollection services)
        where TEvent : notnull
    {
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IEventHandler<TEvent>));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }
}
