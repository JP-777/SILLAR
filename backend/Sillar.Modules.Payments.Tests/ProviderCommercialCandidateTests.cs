using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Tests;

public sealed class ProviderCommercialCandidateTests
{
    [Fact]
    public void Mapper_conserva_identidades_opacas_order_y_evidencia_sin_normalizar()
    {
        var evidence = PaymentTestData.Evidence(
            provider: " Provider:#/Á ",
            eventId: " Event:?&=ñ ",
            settlementId: " Settlement:+%@ü ");
        var candidate = PaymentTestData.CommercialCandidate(
            evidence: evidence,
            evidenceReference: " attestation://Ref A?x=1 ");

        var request = M03CandidateRequestMapper.Map(candidate);

        Assert.Equal(" Provider:#/Á ", request.Provider);
        Assert.Equal(" Event:?&=ñ ", request.ProviderEventId);
        Assert.Equal(" Settlement:+%@ü ", request.ExternalSettlementId);
        Assert.Equal(PaymentTestData.OrderId, request.OrderId);
        Assert.Equal(candidate.Facts.GrossAmount, request.Amount);
        Assert.Equal("PEN", request.Currency);
        Assert.Equal(
            " attestation://Ref A?x=1 ",
            request.VerifiedEvidenceReference);
    }

    [Fact]
    public async Task Fuente_sin_correlacion_autorizada_cierra_sin_inventar_order()
    {
        var evidence = PaymentTestData.Evidence();
        var source = Source(
            evidence,
            correlation: null);

        var result = await source.PrepareAsync(
            Guid.CreateVersion7(),
            CancellationToken);

        Assert.Equal(
            ProviderCommercialCandidatePreparationOutcome.DecisionRequired,
            result.Outcome);
        Assert.Equal("authorized_order_correlation_missing", result.Reason);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public async Task Correlacion_de_otra_identidad_se_bloquea_fail_closed()
    {
        var evidence = PaymentTestData.Evidence();
        var other = PaymentTestData.Evidence(eventId: "event-other");
        var source = Source(
            evidence,
            ProviderAuthorizedOrderCorrelation.Create(
                PaymentTestData.OrderId,
                PaymentIdempotencyKeys.From(other),
                "sales-authority-ref"));

        var result = await source.PrepareAsync(
            Guid.CreateVersion7(),
            CancellationToken);

        Assert.Equal(
            ProviderCommercialCandidatePreparationOutcome.DecisionRequired,
            result.Outcome);
        Assert.Equal(
            "authorized_order_correlation_identity_mismatch",
            result.Reason);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public async Task Referencia_persistida_ausente_o_ambigua_no_elige_una_arbitrariamente()
    {
        var evidence = PaymentTestData.Evidence();
        var correlation = Correlation(evidence);
        var missing = new ProviderCommercialCandidateSource(
            new FactsSource(PaymentTestData.Facts(evidence)),
            new EvidenceReferenceSource([]),
            new CorrelationSource(correlation));
        var ambiguous = new ProviderCommercialCandidateSource(
            new FactsSource(PaymentTestData.Facts(evidence)),
            new EvidenceReferenceSource([
                Reference(evidence, "attestation-a"),
                Reference(evidence, "attestation-b")
            ]),
            new CorrelationSource(correlation));

        var missingResult = await missing.PrepareAsync(
            Guid.CreateVersion7(),
            CancellationToken);
        var ambiguousResult = await ambiguous.PrepareAsync(
            Guid.CreateVersion7(),
            CancellationToken);

        Assert.Equal(
            "verified_evidence_reference_missing",
            missingResult.Reason);
        Assert.Equal(
            "verified_evidence_reference_ambiguous",
            ambiguousResult.Reason);
        Assert.Null(missingResult.Candidate);
        Assert.Null(ambiguousResult.Candidate);
    }

    [Fact]
    public async Task Candidato_completo_vincula_order_hechos_y_referencia_a_una_identidad()
    {
        var evidence = PaymentTestData.Evidence(
            provider: "Provider-A ",
            eventId: " Event/01 ",
            settlementId: " Settlement/01 ");
        var source = Source(evidence, Correlation(evidence));

        var result = await source.PrepareAsync(
            Guid.CreateVersion7(),
            CancellationToken);

        Assert.Equal(
            ProviderCommercialCandidatePreparationOutcome.Ready,
            result.Outcome);
        Assert.Null(result.Reason);
        var candidate = Assert.IsType<ProviderCommercialEffectCandidate>(
            result.Candidate);
        Assert.Equal(PaymentIdempotencyKeys.From(evidence), candidate.Identity);
        Assert.Equal(PaymentTestData.OrderId, candidate.OrderCorrelation.OrderId);
        Assert.True(candidate.VerifiedEvidenceReference.Matches(evidence));
    }

    [Fact]
    public void Contratos_tipados_rechazan_order_y_referencia_vacios()
    {
        var evidence = PaymentTestData.Evidence();
        var identity = PaymentIdempotencyKeys.From(evidence);

        Assert.Throws<ArgumentException>(() =>
            ProviderAuthorizedOrderCorrelation.Create(
                Guid.Empty,
                identity,
                "authority"));
        Assert.Throws<ArgumentException>(() =>
            ProviderVerifiedEvidenceReference.Create(
                Guid.Empty,
                evidence,
                "attestation"));
        Assert.Throws<ArgumentException>(() =>
            ProviderVerifiedEvidenceReference.Create(
                Guid.CreateVersion7(),
                evidence,
                " "));
    }

    private static ProviderCommercialCandidateSource Source(
        VerifiedExternalPaymentEvidence evidence,
        ProviderAuthorizedOrderCorrelation? correlation)
        => new(
            new FactsSource(PaymentTestData.Facts(evidence)),
            new EvidenceReferenceSource([Reference(evidence)]),
            new CorrelationSource(correlation));

    private static ProviderAuthorizedOrderCorrelation Correlation(
        VerifiedExternalPaymentEvidence evidence)
        => ProviderAuthorizedOrderCorrelation.Create(
            PaymentTestData.OrderId,
            PaymentIdempotencyKeys.From(evidence),
            "sales-authority-ref");

    private static ProviderVerifiedEvidenceReference Reference(
        VerifiedExternalPaymentEvidence evidence,
        string value = "attestation-ref")
        => ProviderVerifiedEvidenceReference.Create(
            Guid.CreateVersion7(),
            evidence,
            value);

    private static CancellationToken CancellationToken
        => TestContext.Current.CancellationToken;

    private sealed class FactsSource(ProviderSettlementFacts? facts)
        : IProviderSettlementFactsSource
    {
        public ValueTask<ProviderSettlementFacts?> LoadCandidateFactsAsync(
            Guid inboxEventId,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(facts);
    }

    private sealed class EvidenceReferenceSource(
        IReadOnlyList<ProviderVerifiedEvidenceReference> references)
        : IProviderVerifiedEvidenceReferenceSource
    {
        public ValueTask<IReadOnlyList<ProviderVerifiedEvidenceReference>>
            LoadAsync(
                Guid inboxEventId,
                CancellationToken cancellationToken)
            => ValueTask.FromResult(references);
    }

    private sealed class CorrelationSource(
        ProviderAuthorizedOrderCorrelation? correlation)
        : IProviderAuthorizedOrderCorrelationSource
    {
        public ValueTask<ProviderAuthorizedOrderCorrelation?> LoadAsync(
            Guid inboxEventId,
            PaymentIdempotencyKeys identity,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(correlation);
    }
}
