using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Modules.Assistant.Authorization;
using Sillar.Modules.Assistant.Composition;
using Sillar.Modules.Assistant.Data;
using Sillar.Modules.Assistant.Data.Repositories;
using Sillar.Modules.Assistant.Domain;
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

    private static ServiceProvider CrearProveedor()
    {
        var policy = Politica();
        return new ServiceCollection()
            .AddSingleton(policy)
            .AddAssistantInternalServices(Configurar, policy)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

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
    public void SinPoliticaRegistradaPorHostFallaAntesDeAgregarServicios()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            services.AddAssistantInternalServices(Configurar, Politica()));
        Assert.Empty(services);
    }

    [Fact]
    public void PoliticaDistintaDeLaRegistradaPorHostSeRechaza()
    {
        var services = new ServiceCollection();
        var hostPolicy = Politica();
        services.AddSingleton(hostPolicy);
        Assert.Throws<InvalidOperationException>(() =>
            services.AddAssistantInternalServices(Configurar, Politica()));
        Assert.Same(hostPolicy,
            Assert.Single(services, x => x.ServiceType == typeof(EditorialPersistencePolicy))
                .ImplementationInstance);
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(AssistantDbContext));
    }

    [Fact]
    public void PoliticasDuplicadasDeHostSeRechazanAunqueSeanLaMismaInstancia()
    {
        var services = new ServiceCollection();
        var policy = Politica();
        services.AddSingleton(policy);
        services.AddSingleton(policy);
        Assert.Throws<InvalidOperationException>(() =>
            services.AddAssistantInternalServices(Configurar, policy));
        Assert.Equal(2, services.Count(x => x.ServiceType == typeof(EditorialPersistencePolicy)));
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(AssistantDbContext));
    }

    [Fact]
    public void RegistroDeFabricaNoSeConfundeConInstanciaAtestadaPorHost()
    {
        var services = new ServiceCollection();
        var policy = Politica();
        services.AddSingleton<EditorialPersistencePolicy>(_ => policy);
        Assert.Throws<InvalidOperationException>(() =>
            services.AddAssistantInternalServices(Configurar, policy));
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(AssistantDbContext));
    }

    [Fact]
    public void SoloExponeFachadaRevocableEditorial()
    {
        using var provider = CrearProveedor();
        using var scope = provider.CreateScope();
        var editorial = scope.ServiceProvider.GetRequiredService<IAssistantEditorialAdministration>();
        Assert.IsType<RevocableAssistantEditorialAdministration>(editorial);
        var direct = typeof(RevocableAssistantEditorialAdministration).Assembly.GetType(
            "Sillar.Modules.Assistant.Services.EfAssistantEditorialAdministration", throwOnError: true)!;
        Assert.Null(scope.ServiceProvider.GetService(direct));
        Assert.Null(scope.ServiceProvider.GetService<EfEditorialArticleStore>());
    }

    [Fact]
    public async Task SinAutoridadesLaFachadaNoLlegaAlRepositorioNiAbreConexion()
    {
        using var provider = CrearProveedor();
        using var scope = provider.CreateScope();
        var editorial = scope.ServiceProvider.GetRequiredService<IAssistantEditorialAdministration>();
        var actor = new EditorialPrincipal(Instalacion, EditorialRoles.Admin, "principal", true);
        Assert.Equal(EditorialResult.Forbidden,
            await editorial.CreateAsync(actor, Guid.CreateVersion7(), TestContext.Current.CancellationToken));
        var articleId = Guid.CreateVersion7();
        var revisionId = Guid.CreateVersion7();
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(EditorialResult.Forbidden,
            await editorial.SaveDraftAsync(actor, articleId,
                new KnowledgeDraft(revisionId, "Pregunta", "Respuesta", "Título",
                    new Uri("https://ejemplo.test/faq")), 0, ct));
        Assert.Equal(EditorialResult.Forbidden,
            await editorial.PublishAsync(actor, articleId, revisionId, 0, ct));
        Assert.Equal(EditorialResult.Forbidden,
            await editorial.WithdrawAsync(actor, articleId, revisionId, 0, ct));
    }

    [Fact]
    public void RechazaRegistroEditorialPrevioQuePodriaSuplantarLaFachada()
    {
        var services = new ServiceCollection();
        var policy = Politica();
        services.AddSingleton(policy);
        services.AddScoped<IAssistantEditorialAdministration>(_ => null!);
        Assert.Throws<InvalidOperationException>(() =>
            services.AddAssistantInternalServices(Configurar, policy));
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
        var policy = Politica();
        services.AddSingleton(policy);
        services.AddSingleton<TimeProvider>(clock);
        services.AddAssistantInternalServices(Configurar, policy);
        using var provider = services.BuildServiceProvider();
        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void MantienePoliticaConfiableSinCrearInstalacionesNuevas()
    {
        var policy = Politica();
        var services = new ServiceCollection()
            .AddSingleton(policy)
            .AddAssistantInternalServices(Configurar, policy);
        Assert.Same(policy,
            Assert.Single(services, x => x.ServiceType == typeof(EditorialPersistencePolicy))
                .ImplementationInstance);
        using var provider = services.BuildServiceProvider();
        Assert.Same(policy, provider.GetRequiredService<EditorialPersistencePolicy>());
        Assert.Same(policy, Assert.Single(provider.GetServices<EditorialPersistencePolicy>()));
        Assert.True(policy.HasInstallation(Instalacion));
        Assert.False(policy.HasInstallation(Guid.NewGuid()));
    }

    private sealed class Reloj : TimeProvider { }
}
