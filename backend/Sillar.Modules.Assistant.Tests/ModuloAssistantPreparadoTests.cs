using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Modules.Assistant.Data;
using Sillar.Modules.Assistant.Data.Repositories;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.Assistant.Tests;

public sealed class ModuloAssistantPreparadoTests
{
    private static readonly Guid Instalacion = Guid.Parse("a1a1a1a1-1111-4444-8888-a1a1a1a1a1a1");

    private static IConfiguration Configuracion(bool conConexion = true)
    {
        var values = new Dictionary<string, string?>();
        if (conConexion)
            values["ConnectionStrings:Default"] =
                "Host=127.0.0.1;Database=m12_sin_conectar;Username=no_abrir;Password=no_abrir";
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static EditorialPersistencePolicy Politica() => new(
        new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [Instalacion] = ["ejemplo.test"]
        });

    private static ServiceCollection Servicios(bool conInstalacion = true, bool conPolitica = true)
    {
        var services = new ServiceCollection();
        if (conInstalacion)
            services.AddSingleton<ITrustedAssistantInstallationScope>(new AmbitoSinInstalacionActiva());
        if (conPolitica)
            services.AddSingleton(Politica());
        return services;
    }

    [Fact]
    public void ImplementaContratoModularDeSillar()
    {
        IModule module = new AssistantModule();
        Assert.Equal("assistant", module.Code);
        Assert.Equal("assistant", module.Schema);
    }

    [Fact]
    public void DependeDeCoreSinInventarDependencias()
    {
        IModule module = new AssistantModule();
        Assert.Equal("core", Assert.Single(module.HardDependencies));
        Assert.Empty(module.SoftDependencies);
    }

    [Fact]
    public void MetadatosAdministrativosSonLegibles()
    {
        IModule module = new AssistantModule();
        Assert.NotEmpty(module.DisplayName);
        Assert.NotEmpty(module.Description);
        Assert.InRange(module.Description.Length, 1, 300);
        Assert.Equal("0.1.0", module.Version);
        Assert.True(module.DisplayOrder > 0);
    }

    [Fact]
    public void SinCadenaDeConexionNoRegistraModulo()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new AssistantModule().RegisterServices(Servicios(), Configuracion(false)));
    }

    [Fact]
    public void SinAutoridadDeInstalacionNoRegistraModulo()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new AssistantModule().RegisterServices(Servicios(conInstalacion: false), Configuracion()));
    }

    [Fact]
    public void SinPoliticaEditorialNoRegistraModulo()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new AssistantModule().RegisterServices(Servicios(conPolitica: false), Configuracion()));
    }

    [Fact]
    public void RechazaPoliticasEditorialesDuplicadas()
    {
        var services = Servicios();
        services.AddSingleton(Politica());
        Assert.Throws<InvalidOperationException>(() =>
            new AssistantModule().RegisterServices(services, Configuracion()));
    }

    [Fact]
    public void RechazaPoliticaEditorialRegistradaComoFabrica()
    {
        var services = Servicios(conPolitica: false);
        services.AddSingleton<EditorialPersistencePolicy>(_ => Politica());
        Assert.Throws<InvalidOperationException>(() =>
            new AssistantModule().RegisterServices(services, Configuracion()));
    }

    [Fact]
    public void RegistroValidoConservaAutoridadDelHost()
    {
        var services = Servicios();
        var original = services.Single(x => x.ServiceType == typeof(EditorialPersistencePolicy))
            .ImplementationInstance;
        new AssistantModule().RegisterServices(services, Configuracion());
        Assert.Same(original,
            Assert.Single(services, x => x.ServiceType == typeof(EditorialPersistencePolicy))
                .ImplementationInstance);
        using var provider = services.BuildServiceProvider();
        Assert.Same(original, provider.GetRequiredService<EditorialPersistencePolicy>());
        Assert.Same(original, Assert.Single(provider.GetServices<EditorialPersistencePolicy>()));
        Assert.IsType<AmbitoSinInstalacionActiva>(
            provider.GetRequiredService<ITrustedAssistantInstallationScope>());
    }

    [Fact]
    public void RegistraContextoYLectorPublicoSinConectarBase()
    {
        var services = Servicios();
        new AssistantModule().RegisterServices(services, Configuracion());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssistantDbContext>();
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);
        Assert.IsType<EfPublishedFaqReader>(
            scope.ServiceProvider.GetRequiredService<IAsyncPublishedFaqReader>());
    }

    [Fact]
    public void NuncaFabricaActorAdministrativoNiAuditoria()
    {
        var services = Servicios();
        new AssistantModule().RegisterServices(services, Configuracion());
        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<ITrustedEditorialAdminScope>());
        Assert.Null(provider.GetService<IAuditWriter>());
    }

    [Fact]
    public void RegistroDobleSeRechazaSinSobrescribirContexto()
    {
        var services = Servicios();
        var module = new AssistantModule();
        module.RegisterServices(services, Configuracion());
        Assert.Throws<InvalidOperationException>(() => module.RegisterServices(services, Configuracion()));
    }

    [Fact]
    public void NoPuedeMontarRutasSobreUnBuilderNulo()
    {
        Assert.Throws<ArgumentNullException>(() => new AssistantModule().MapEndpoints(null!));
    }

    private sealed class AmbitoSinInstalacionActiva : ITrustedAssistantInstallationScope
    {
        public bool TryGetEnabledInstallation(out Guid installationId)
        {
            installationId = Guid.Empty;
            return false;
        }
    }
}
