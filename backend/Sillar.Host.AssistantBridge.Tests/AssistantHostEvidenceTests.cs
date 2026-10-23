using Microsoft.Extensions.DependencyInjection;
using Sillar.Host.AssistantBridge;

namespace Sillar.Host.AssistantBridge.Tests;

public sealed class AssistantHostEvidenceTests
{
    [Fact]
    public void Sin_productor_la_foto_es_denegacion_completa()
    {
        var services = new ServiceCollection();
        services.AddAssistantHostCandidates();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var evidence = scope.ServiceProvider.GetRequiredService<AssistantHostEvidenceAdapter>();

        Assert.False(evidence.IsInstallationConfigured);
        Assert.False(evidence.IsAssistantActive);
        Assert.False(evidence.IsAssistantLicensed);
        Assert.Null(evidence.InstallationId);
        Assert.Null(scope.ServiceProvider.GetService<AssistantHostEvidenceResolver>());
    }

    [Fact]
    public void Leer_getters_sin_resolver_no_ejecuta_la_sonda()
    {
        var calls = 0;
        var services = new ServiceCollection();
        services.AddAssistantHostAuthorityProbe(_ =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(new AssistantAuthorityObservation(
                AssistantAuthorityVerdict.Denied,
                AssistantInstallationIdentityEvidence.Missing));
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var evidence = scope.ServiceProvider.GetRequiredService<AssistantHostEvidenceAdapter>();

        Assert.False(evidence.IsInstallationConfigured);
        Assert.False(evidence.IsAssistantActive);
        Assert.False(evidence.IsAssistantLicensed);
        Assert.Null(evidence.InstallationId);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(AssistantAuthorityVerdict.Denied, AssistantHostDenialReason.AuthorityDenied)]
    [InlineData(AssistantAuthorityVerdict.PendingDecision, AssistantHostDenialReason.PendingDecision)]
    public async Task Veredicto_no_autorizado_publica_denegacion_completa(
        AssistantAuthorityVerdict verdict,
        AssistantHostDenialReason expectedReason)
    {
        await using var provider = Provider(_ =>
            ValueTask.FromResult(new AssistantAuthorityObservation(
                verdict,
                AssistantInstallationIdentityEvidence.InternalKeyObservedButUnattested)));
        await using var scope = provider.CreateAsyncScope();

        var resolver = scope.ServiceProvider.GetRequiredService<AssistantHostEvidenceResolver>();
        var snapshot = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        AssertDenied(snapshot, expectedReason);
        AssertDenied(scope.ServiceProvider.GetRequiredService<AssistantHostEvidenceAdapter>());
    }

    [Fact]
    public async Task Identidad_ausente_se_deniega()
    {
        await using var provider = Provider(_ => ValueTask.FromResult(
            new AssistantAuthorityObservation(
                AssistantAuthorityVerdict.Allowed,
                AssistantInstallationIdentityEvidence.Missing)));
        await using var scope = provider.CreateAsyncScope();

        var snapshot = await scope.ServiceProvider
            .GetRequiredService<AssistantHostEvidenceResolver>()
            .ResolveAsync(TestContext.Current.CancellationToken);

        AssertDenied(snapshot, AssistantHostDenialReason.InstallationIdentityInvalid);
    }

    [Fact]
    public async Task Clave_interna_observada_no_se_convierte_en_identidad_M12()
    {
        await using var provider = Provider(_ => ValueTask.FromResult(
            new AssistantAuthorityObservation(
                AssistantAuthorityVerdict.Allowed,
                AssistantInstallationIdentityEvidence.InternalKeyObservedButUnattested)));
        await using var scope = provider.CreateAsyncScope();

        var snapshot = await scope.ServiceProvider
            .GetRequiredService<AssistantHostEvidenceResolver>()
            .ResolveAsync(TestContext.Current.CancellationToken);

        AssertDenied(snapshot, AssistantHostDenialReason.InstallationIdentityUnattested);
    }

    [Fact]
    public async Task Excepcion_de_autoridad_se_convierte_en_denegacion()
    {
        await using var provider = Provider(_ =>
            throw new InvalidOperationException("autoridad no disponible"));
        await using var scope = provider.CreateAsyncScope();

        var snapshot = await scope.ServiceProvider
            .GetRequiredService<AssistantHostEvidenceResolver>()
            .ResolveAsync(TestContext.Current.CancellationToken);

        AssertDenied(snapshot, AssistantHostDenialReason.AuthorityUnavailable);
    }

    [Fact]
    public async Task Cancelacion_o_timeout_se_convierte_en_denegacion()
    {
        await using var provider = Provider(_ =>
            ValueTask.FromCanceled<AssistantAuthorityObservation>(new CancellationToken(canceled: true)));
        await using var scope = provider.CreateAsyncScope();

        var snapshot = await scope.ServiceProvider
            .GetRequiredService<AssistantHostEvidenceResolver>()
            .ResolveAsync(TestContext.Current.CancellationToken);

        AssertDenied(snapshot, AssistantHostDenialReason.AuthorityCancelled);
    }

    [Fact]
    public async Task Cada_ambito_conserva_su_propia_foto()
    {
        var services = new ServiceCollection();
        services.AddAssistantHostAuthorityProbe(_ => ValueTask.FromResult(
            new AssistantAuthorityObservation(
                AssistantAuthorityVerdict.Denied,
                AssistantInstallationIdentityEvidence.Missing)));
        await using var provider = services.BuildServiceProvider();
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();

        await first.ServiceProvider.GetRequiredService<AssistantHostEvidenceResolver>()
            .ResolveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            AssistantHostDenialReason.AuthorityDenied,
            first.ServiceProvider.GetRequiredService<AssistantHostEvidenceScope>().Current.DenialReason);
        Assert.Equal(
            AssistantHostDenialReason.Unresolved,
            second.ServiceProvider.GetRequiredService<AssistantHostEvidenceScope>().Current.DenialReason);
    }

    [Fact]
    public async Task Resoluciones_concurrentes_comparten_una_sola_evaluacion()
    {
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        services.AddAssistantHostAuthorityProbe(async _ =>
        {
            Interlocked.Increment(ref calls);
            await release.Task;
            return new AssistantAuthorityObservation(
                AssistantAuthorityVerdict.Denied,
                AssistantInstallationIdentityEvidence.Missing);
        });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<AssistantHostEvidenceResolver>();

        var first = resolver.ResolveAsync(TestContext.Current.CancellationToken).AsTask();
        var second = resolver.ResolveAsync(TestContext.Current.CancellationToken).AsTask();
        release.SetResult();

        var results = await Task.WhenAll(first, second);
        Assert.Equal(1, calls);
        Assert.Same(results[0], results[1]);
    }

    private static ServiceProvider Provider(AssistantAuthorityProbe probe)
    {
        var services = new ServiceCollection();
        services.AddAssistantHostAuthorityProbe(probe);
        return services.BuildServiceProvider();
    }

    private static void AssertDenied(
        AssistantHostEvidenceSnapshot snapshot,
        AssistantHostDenialReason reason)
    {
        Assert.False(snapshot.IsInstallationConfigured);
        Assert.False(snapshot.IsAssistantActive);
        Assert.False(snapshot.IsAssistantLicensed);
        Assert.Null(snapshot.InstallationId);
        Assert.Equal(reason, snapshot.DenialReason);
    }

    private static void AssertDenied(AssistantHostEvidenceAdapter evidence)
    {
        Assert.False(evidence.IsInstallationConfigured);
        Assert.False(evidence.IsAssistantActive);
        Assert.False(evidence.IsAssistantLicensed);
        Assert.Null(evidence.InstallationId);
    }
}
