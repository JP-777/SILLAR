using Sillar.Host.AssistantBridge;

namespace Sillar.Host.AssistantBridge.Tests;

public sealed class TypedAssistantAuthorityProbeTests
{
    [Fact]
    public async Task Crear_la_sonda_tipada_no_evalua_la_autoridad()
    {
        var calls = 0;
        var now = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var adapter = new TypedAssistantAuthorityProbe<DecisionDePrueba>(
            _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(new DecisionDePrueba(DecisionResultado.Denied, HasInternalKey: false));
            },
            Project,
            new FixedTimeProvider(now));

        var probe = adapter.CreateProbe();

        Assert.Equal(0, calls);
        var observation = await probe(TestContext.Current.CancellationToken);
        Assert.Equal(1, calls);
        Assert.Equal(AssistantAuthorityVerdict.Denied, observation.Verdict);
        Assert.Equal(AssistantInstallationIdentityEvidence.Missing, observation.InstallationIdentity);
    }

    [Theory]
    [InlineData(DecisionResultado.Denied, false, AssistantAuthorityVerdict.Denied,
        AssistantInstallationIdentityEvidence.Missing)]
    [InlineData(DecisionResultado.Pending, true, AssistantAuthorityVerdict.PendingDecision,
        AssistantInstallationIdentityEvidence.InternalKeyObservedButUnattested)]
    [InlineData(DecisionResultado.Allowed, true, AssistantAuthorityVerdict.Allowed,
        AssistantInstallationIdentityEvidence.InternalKeyObservedButUnattested)]
    public async Task La_proyeccion_tipada_solo_transporta_evidencia_no_atestada(
        DecisionResultado result,
        bool hasInternalKey,
        AssistantAuthorityVerdict expectedVerdict,
        AssistantInstallationIdentityEvidence expectedIdentity)
    {
        var now = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var adapter = new TypedAssistantAuthorityProbe<DecisionDePrueba>(
            _ => Task.FromResult(new DecisionDePrueba(result, hasInternalKey)),
            Project,
            new FixedTimeProvider(now));

        var observation = await adapter.CreateProbe()(TestContext.Current.CancellationToken);

        Assert.Equal(expectedVerdict, observation.Verdict);
        Assert.Equal(expectedIdentity, observation.InstallationIdentity);
    }

    private static AssistantAuthorityObservation Project(
        DecisionDePrueba decision,
        DateTimeOffset evaluatedAt)
    {
        Assert.Equal(
            new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero),
            evaluatedAt);

        return new AssistantAuthorityObservation(
            decision.Result switch
            {
                DecisionResultado.Denied => AssistantAuthorityVerdict.Denied,
                DecisionResultado.Pending => AssistantAuthorityVerdict.PendingDecision,
                DecisionResultado.Allowed => AssistantAuthorityVerdict.Allowed,
                _ => throw new ArgumentOutOfRangeException(nameof(decision))
            },
            decision.HasInternalKey
                ? AssistantInstallationIdentityEvidence.InternalKeyObservedButUnattested
                : AssistantInstallationIdentityEvidence.Missing);
    }

    public enum DecisionResultado
    {
        Denied,
        Pending,
        Allowed
    }

    private sealed record DecisionDePrueba(DecisionResultado Result, bool HasInternalKey);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
