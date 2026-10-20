using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Modules.Assistant.Composition;
using Sillar.Modules.Assistant.Data;
using Sillar.Modules.Assistant.Data.Repositories;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public sealed class ComposicionInternaAssistantTests
{
    private static readonly Guid Instalacion = Guid.Parse("a1a1a1a1-1111-4444-8888-a1a1a1a1a1a1");

    private static EditorialPersistencePolicy Politica() => new(
        new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [Instalacion] = new[] { "ejemplo.test" }
        });

    private static void Configurar(DbContextOptionsBuilder options) =>
        options.UseNpgsql("Host=127.0.0.1;Database=no_abrir;Username=no_abrir;Password=no_abrir");

    private static ServiceProvider CrearProveedor() => new ServiceCollection()
        .AddAssistantInternalServices(Configurar, Politica())
        .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    [Fact]
    public void ExigeColeccionDeServicios()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(() => services!.AddAssistantInternalServices(
            Configurar, Politica()));
    }

    [Fact]
    public void ExigeConfiguracionDePersistencia()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection()
            .AddAssistantInternalServices(null!, Politica()));
    }

    [Fact]
    public void ExigePoliticaEditorialConfiable()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection()
            .AddAssistantInternalServices(Configurar, null!));
    }

    [Fact]
    public void RegistraAdaptadorEditorialYRepositorioEf()
    {
        using var provider = CrearProveedor();
        using var scope = provider.CreateScope();
        var editorial = scope.ServiceProvider.GetRequiredService<IAssistantEditorialAdministration>();
        Assert.IsType<EfAssistantEditorialAdministration>(editorial);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<EfEditorialArticleStore>());
    }

    [Fact]
    public void RegistraLectorFaqEfSinFuenteVolatil()
    {
        using var provider = CrearProveedor();
        using var scope = provider.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAsyncPublishedFaqReader>();
        Assert.IsType<EfPublishedFaqReader>(reader);
    }

    [Fact]
    public void NoFabricaIdentidadesDeInstalacionNiDeAdministracion()
    {
        using var provider = CrearProveedor();
        using var scope = provider.CreateScope();
        Assert.Null(scope.ServiceProvider.GetService<ITrustedAssistantInstallationScope>());
        Assert.Null(scope.ServiceProvider.GetService<ITrustedEditorialAdminScope>());
    }

    [Fact]
    public void NoSustituyeAuditoriaCore()
    {
        using var provider = CrearProveedor();
        using var scope = provider.CreateScope();
        Assert.Null(scope.ServiceProvider.GetService<IAuditWriter>());
    }

    [Fact]
    public void ContextoEfEsAisladoPorAmbito()
    {
        using var provider = CrearProveedor();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<AssistantDbContext>();
        var b = first.ServiceProvider.GetRequiredService<AssistantDbContext>();
        var c = second.ServiceProvider.GetRequiredService<AssistantDbContext>();
        Assert.Same(a, b);
        Assert.NotSame(a, c);
    }

    [Fact]
    public void ConservaRelojInyectadoPorElHost()
    {
        var clock = new Reloj();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock);
        services.AddAssistantInternalServices(Configurar, Politica());
        using var provider = services.BuildServiceProvider();
        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void MantienePoliticaConfiableSinCrearInstalacionesNuevas()
    {
        var policy = Politica();
        using var provider = new ServiceCollection()
            .AddAssistantInternalServices(Configurar, policy)
            .BuildServiceProvider();
        Assert.Same(policy, provider.GetRequiredService<EditorialPersistencePolicy>());
        Assert.True(policy.HasInstallation(Instalacion));
        Assert.False(policy.HasInstallation(Guid.NewGuid()));
    }

    private sealed class Reloj : TimeProvider { }
}
