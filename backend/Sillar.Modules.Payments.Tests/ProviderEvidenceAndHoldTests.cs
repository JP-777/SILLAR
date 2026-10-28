using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Tests;

/// <summary>Evidencia, riesgos y propagacion fail-closed de P5-P7.</summary>
public sealed class ProviderEvidenceAndHoldTests
{
    [Fact]
    public void Atestacion_coherente_deja_candidato_solo_para_revision_durable()
    {
        var incoming = PaymentTestData.Evidence();

        var result = ProviderEvidenceReview.Review(
            [incoming],
            [],
            [PaymentTestData.Attested(incoming)]);

        var item = Assert.Single(result.Items);
        Assert.Equal(
            ProviderEvidenceReviewDisposition.CandidateForDurableInboxReview,
            item.Disposition);
        Assert.Equal("verifier-ref-001", item.AttestationReference);
        Assert.True(item.RequiresIndependentAuthentication);
        Assert.Equal(1, result.CandidatesForDurableInboxReview);
    }

    [Fact]
    public void Referencias_de_atestacion_contradictorias_se_preservan_y_retienen()
    {
        var incoming = PaymentTestData.Evidence();
        var identity = PaymentIdempotencyKeys.From(incoming);

        var result = ProviderEvidenceReview.Review(
            [incoming],
            [],
            [
                new(identity, ProviderEvidenceVerdict.AttestedByVerifier, "ref-a"),
                new(identity, ProviderEvidenceVerdict.AttestedByVerifier, "ref-b")
            ]);

        var item = Assert.Single(result.Items);
        Assert.Equal(
            ProviderEvidenceReviewDisposition.HoldInconsistentEvidence,
            item.Disposition);
        Assert.Null(item.AttestationReference);
        Assert.Equal(2, item.Observations.Count);
        Assert.True(result.HasAnyHolds);
    }

    [Fact]
    public void Atestacion_no_rebaja_un_conflicto_de_replay()
    {
        var incoming = PaymentTestData.Evidence(hashDigit: 'a');
        var conflicting = PaymentTestData.Evidence(hashDigit: 'b');

        var result = ProviderEvidenceReview.Review(
            [incoming],
            [conflicting],
            [PaymentTestData.Attested(incoming)]);

        Assert.Equal(
            ProviderEvidenceReviewDisposition.HoldReplayConflict,
            Assert.Single(result.Items).Disposition);
    }

    [Fact]
    public void Evidencia_rechazada_no_se_convierte_en_candidato()
    {
        var incoming = PaymentTestData.Evidence();
        var observation = new ProviderEvidenceObservation(
            PaymentIdempotencyKeys.From(incoming),
            ProviderEvidenceVerdict.RejectedByVerifier,
            null);

        var result = ProviderEvidenceReview.Review([incoming], [], [observation]);

        Assert.Equal(
            ProviderEvidenceReviewDisposition.RejectUntrustedEvidence,
            Assert.Single(result.Items).Disposition);
        Assert.Equal(0, result.CandidatesForDurableInboxReview);
        Assert.True(result.HasAnyHolds);
    }

    [Fact]
    public void Colision_interproveedor_permanece_en_hold_en_riesgo_y_cola()
    {
        var providerA = PaymentTestData.Evidence(
            provider: "provider-a", eventId: "event-a", hashDigit: 'a');
        var providerB = PaymentTestData.Evidence(
            provider: "provider-b", eventId: "event-b", hashDigit: 'b');
        var reviewed = ProviderEvidenceReview.Review(
            [providerA, providerB],
            [],
            [PaymentTestData.Attested(providerA), PaymentTestData.Attested(providerB)]);

        var risks = ProviderSettlementRiskAssessment.Summarize(reviewed);
        var queue = ProviderSettlementReviewQueue.Create(risks);

        Assert.Equal(ProviderSettlementIdentityScope.Unresolved, risks.IdentityScope);
        Assert.All(
            risks.Groups,
            group => Assert.True(group.Risk is
                ProviderSettlementRisk.HoldReplayConflict
                    or ProviderSettlementRisk.HoldIdentityScopeUnresolved));
        Assert.All(queue.Items, item =>
        {
            Assert.Equal(ProviderSettlementReviewLane.Hold, item.Lane);
            Assert.Equal(
                ProviderSettlementReviewHoldReason.CrossProviderIdentityUnresolved,
                item.HoldReason);
        });
        Assert.Equal(["settlement-001"], queue.CrossProviderSettlementIds);
    }

    [Fact]
    public void Colision_historica_interproveedor_retiene_el_unico_grupo_entrante()
    {
        var incoming = PaymentTestData.Evidence(provider: "provider-a");
        var history = PaymentTestData.Evidence(provider: "provider-b");

        var result = ProviderSettlementAnalysisPipeline.Analyze(
            [incoming],
            [history],
            [PaymentTestData.Attested(incoming)]);

        var trace = Assert.Single(result.Trace);
        Assert.True(trace.IsHeld);
        Assert.Equal(
            ProviderSettlementReviewHoldReason.CrossProviderIdentityUnresolved,
            trace.HoldReason);
        Assert.Contains(
            ProviderEvidenceConflictKind.CrossProviderSettlementIdentityUnresolved,
            trace.ConflictKinds);
    }

    [Fact]
    public void Observacion_ajena_al_lote_es_invalida()
    {
        var incoming = PaymentTestData.Evidence();
        var unrelated = PaymentTestData.Evidence(eventId: "other-event");

        Assert.Throws<ArgumentException>(() => ProviderEvidenceReview.Review(
            [incoming],
            [],
            [PaymentTestData.Attested(unrelated)]));
    }

    [Fact]
    public void Atestacion_sin_referencia_y_no_atestada_con_referencia_son_invalidas()
    {
        var incoming = PaymentTestData.Evidence();
        var identity = PaymentIdempotencyKeys.From(incoming);

        Assert.Throws<ArgumentException>(() => ProviderEvidenceReview.Review(
            [incoming],
            [],
            [new(identity, ProviderEvidenceVerdict.AttestedByVerifier, " ")]));
        Assert.Throws<ArgumentException>(() => ProviderEvidenceReview.Review(
            [incoming],
            [],
            [new(identity, ProviderEvidenceVerdict.Unverified, "ref-ficticia")]));
    }
}
