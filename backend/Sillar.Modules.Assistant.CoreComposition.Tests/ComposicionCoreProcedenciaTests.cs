using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Authorization;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Modules.Assistant.Composition;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Assistant.CoreComposition.Tests;

/// <summary>
/// Composición real de los registros candidatos CORE y M12, sin host ni servidor.
/// El interceptor aborta antes de abrir cualquier conexión. Los hechos observados
/// del doble de prueba no se convierten en identidad M12 ni atestación.
/// </summary>
public sealed class ComposicionCoreProcedenciaTests
{
    private static ServiceCollection Services(NoConnections blocker)
    {
        var services = new ServiceCollection();
        services.AddLogging(); // Sin proveedores de logs: no registrar datos de conexión.
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(new NodeIdentity("principal"));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [NodeIdentity.SettingKey] = "principal" })
            .Build());
        services.AddDbContext<CoreDbContext>(options => options
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=no_connection;Username=unused;Password=unused;Pooling=false")
            .AddInterceptors(blocker));
        services.AddCandidateInstallationProvenance();
        services.AddCandidateAssistantAuthority();
        services.AddAssistantQuotaInternals();
        return services;
    }

    [Fact]
    public async Task ProductorCoreSinLecturaDevuelveFalloYNoHabilitaM12()
    {
        var blocker = new NoConnections();
        await using var provider = Services(blocker).BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IInstallationProvenance>()
            .ReadAsync(TestContext.Current.CancellationToken);
        Assert.False(result.IsObserved);
        Assert.Null(result.Provenance);
        Assert.Equal(ProvenanceFailure.AuthorityUnavailable, result.Failure);
        Assert.Equal(1, blocker.Blocked);
        AssertMissingAuthorities(scope.ServiceProvider);
    }

    [Fact]
    public async Task AutoridadCoreSinLecturaDeniegaSinAportarIdentidadM12()
    {
        var blocker = new NoConnections();
        await using var provider = Services(blocker).BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAssistantAccessAuthority>()
            .EvaluateCurrentInstallationAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AssistantAccessOutcome.Denied, result.Outcome);
        Assert.Equal(AssistantAccessReason.AuthorityUnavailable, result.Reason);
        Assert.False(result.IsAllowed);
        Assert.Null(result.InstallationKey);
        Assert.Equal(1, blocker.Blocked);
        AssertMissingAuthorities(scope.ServiceProvider);
    }

    [Fact]
    public async Task ProcedenciaObservadaNoSeConvierteEnAtestacionNiPermisoDeCuota()
    {
        var blocker = new NoConnections();
        var services = Services(blocker);
        // Solo datos sintéticos de contrato. No implementar ni registrar este doble
        // en Host: no aporta entitlement, autoridad ni traducción de identificadores.
        services.AddScoped<IInstallationProvenance>(_ => new ObservedForTest());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IInstallationProvenance>()
            .ReadAsync(TestContext.Current.CancellationToken);
        Assert.True(result.IsObserved);
        Assert.Null(result.Failure);
        AssertMissingAuthorities(scope.ServiceProvider);
        Assert.Equal(0, blocker.Blocked);
    }

    [Fact]
    public async Task CancelacionPreviaDeAmbosContratosNoIntentaAbrirConexion()
    {
        var blocker = new NoConnections();
        await using var provider = Services(blocker).BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.ServiceProvider
            .GetRequiredService<IInstallationProvenance>().ReadAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.ServiceProvider
            .GetRequiredService<IAssistantAccessAuthority>().EvaluateCurrentInstallationAsync(cancellation.Token));
        Assert.Equal(0, blocker.Blocked);
    }

    private static void AssertMissingAuthorities(IServiceProvider provider)
    {
        Assert.Null(provider.GetService<ITrustedAssistantHostEvidence>());
        Assert.Null(provider.GetService<ITrustedAssistantQuotaPolicyWriter>());
        var error = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<ITrustedAssistantInstallationScope>());
        Assert.Contains(nameof(ITrustedAssistantHostEvidence), error.Message);
    }

    private sealed class NoConnections : DbConnectionInterceptor
    {
        public int Blocked { get; private set; }
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Blocked++;
            throw new InvalidOperationException("Lectura física prohibida en esta prueba de composición.");
        }
        public override InterceptionResult ConnectionOpening(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result)
        {
            Blocked++;
            throw new InvalidOperationException("Lectura física prohibida en esta prueba de composición.");
        }
    }

    private sealed class ObservedForTest : IInstallationProvenance
    {
        public Task<InstallationProvenanceResult> ReadAsync(CancellationToken cancellationToken)
            => Task.FromResult(InstallationProvenanceResult.Observed(new(
                Guid.Parse("01920000-0000-7000-8000-000000000001"), "principal", "1.0.0", "1.0.0", "test:observed"),
                new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero)));
    }
}
