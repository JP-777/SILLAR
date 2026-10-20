using Sillar.Modules.Payments.Domain;
using Sillar.Modules.Payments.Simulation;

namespace Sillar.Modules.Payments.Tests;

/// <summary>Escenarios de extremo a extremo sobre dependencias simuladas.</summary>
public sealed class ProviderSettlementSimulationHarnessTests
{
    [Fact]
    public void Replay_atestado_converge_en_previamente_recibido_sin_aplicar_pago()
    {
        var incoming = PaymentTestData.Evidence();
        var harness = Harness(_ => Attested("simulation-ref"));

        var result = harness.Run([incoming], [incoming]);

        var trace = Assert.Single(result.Analysis.Trace);
        Assert.Equal(
            ProviderIntakeDisposition.AlreadyReceived,
            trace.IntakeDisposition);
        Assert.Equal(
            ProviderSettlementReviewLane.PreviouslyKnown,
            trace.ReviewLane);
        Assert.True(trace.RequiresIndependentAuthentication);
        Assert.False(trace.IsHeld);
        Assert.Equal(0, result.Casebook.SeparateDurableReviewCases);
    }

    [Fact]
    public void Conflicto_atestado_permanece_en_hold()
    {
        var incoming = PaymentTestData.Evidence(hashDigit: 'a');
        var contradictory = PaymentTestData.Evidence(hashDigit: 'b');
        var harness = Harness(_ => Attested("simulation-ref"));

        var result = harness.Run([incoming], [contradictory]);

        var trace = Assert.Single(result.Analysis.Trace);
        Assert.True(trace.IsHeld);
        Assert.Equal(
            ProviderEvidenceReviewDisposition.HoldReplayConflict,
            trace.EvidenceDisposition);
        Assert.Equal(1, result.Casebook.HeldCases);
    }

    [Fact]
    public void Evidencia_insuficiente_no_crea_candidato_durable()
    {
        var incoming = PaymentTestData.Evidence();
        var harness = Harness(_ => new(
            SimulatedProviderEvidenceOutcome.Insufficient,
            null));

        var result = harness.Run([incoming], []);

        var trace = Assert.Single(result.Analysis.Trace);
        Assert.Equal(
            ProviderEvidenceReviewDisposition.AwaitIndependentVerification,
            trace.EvidenceDisposition);
        Assert.Equal(
            ProviderSettlementReviewLane.AwaitIndependentEvidence,
            trace.ReviewLane);
        Assert.Equal(0, result.Analysis.EvidenceReview.CandidatesForDurableInboxReview);
        Assert.Equal(1, result.Casebook.EvidenceAttentionCases);
    }

    [Fact]
    public void Incertidumbre_interproveedor_retiene_todos_los_expedientes()
    {
        var providerA = PaymentTestData.Evidence(
            provider: "provider-a", eventId: "event-a", hashDigit: 'a');
        var providerB = PaymentTestData.Evidence(
            provider: "provider-b", eventId: "event-b", hashDigit: 'b');
        var harness = Harness(evidence =>
            Attested($"simulation-{evidence.Provider}"));

        var result = harness.Run([providerA, providerB], []);

        Assert.Equal(2, result.Casebook.HeldCases);
        Assert.All(result.Analysis.Trace, trace =>
        {
            Assert.True(trace.IsHeld);
            Assert.Equal(
                ProviderSettlementReviewHoldReason.CrossProviderIdentityUnresolved,
                trace.HoldReason);
        });
    }

    [Fact]
    public void Escenario_mixto_separa_candidato_rechazo_e_insuficiencia()
    {
        var candidate = PaymentTestData.Evidence(
            eventId: "candidate", settlementId: "settlement-a", hashDigit: 'a');
        var rejected = PaymentTestData.Evidence(
            eventId: "rejected", settlementId: "settlement-b", hashDigit: 'b');
        var insufficient = PaymentTestData.Evidence(
            eventId: "insufficient", settlementId: "settlement-c", hashDigit: 'c');
        var harness = Harness(evidence => evidence.ProviderEventId switch
        {
            "candidate" => Attested("simulation-candidate"),
            "rejected" => new(
                SimulatedProviderEvidenceOutcome.Rejected,
                null),
            _ => new(
                SimulatedProviderEvidenceOutcome.Insufficient,
                null)
        });

        var result = harness.Run([insufficient, candidate, rejected], []);

        Assert.Equal(3, result.Analysis.Trace.Count);
        Assert.Equal(1, result.Casebook.SeparateDurableReviewCases);
        Assert.Equal(2, result.Casebook.EvidenceAttentionCases);
        Assert.Equal(
            result.Integrity.ContentSha256,
            result.Casebook.SourceAnalysisSha256);
    }

    [Theory]
    [InlineData(SimulatedProviderEvidenceOutcome.Attested, null)]
    [InlineData(SimulatedProviderEvidenceOutcome.Attested, " ")]
    [InlineData(SimulatedProviderEvidenceOutcome.Insufficient, "false-ref")]
    [InlineData(SimulatedProviderEvidenceOutcome.Rejected, "false-ref")]
    public void Dependencia_simulada_incoherente_falla_cerrado(
        SimulatedProviderEvidenceOutcome outcome,
        string? reference)
    {
        var harness = Harness(_ => new(outcome, reference));

        Assert.Throws<InvalidOperationException>(() =>
            harness.Run([PaymentTestData.Evidence()], []));
    }

    [Fact]
    public void Resultado_desconocido_de_la_dependencia_falla_cerrado()
    {
        var harness = Harness(_ => new(
            (SimulatedProviderEvidenceOutcome)999,
            null));

        Assert.Throws<InvalidOperationException>(() =>
            harness.Run([PaymentTestData.Evidence()], []));
    }

    private static ProviderSettlementSimulationHarness Harness(
        Func<VerifiedExternalPaymentEvidence,
            SimulatedProviderEvidenceObservation> observe)
        => new(new ObservationSimulator(observe));

    private static SimulatedProviderEvidenceObservation Attested(
        string reference)
        => new(SimulatedProviderEvidenceOutcome.Attested, reference);

    private sealed class ObservationSimulator(
        Func<VerifiedExternalPaymentEvidence,
            SimulatedProviderEvidenceObservation> observe)
        : IProviderEvidenceObservationSimulator
    {
        public SimulatedProviderEvidenceObservation Observe(
            VerifiedExternalPaymentEvidence evidence)
            => observe(evidence);
    }
}
