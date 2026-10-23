using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;

namespace Sillar.Modules.Payments.Tests;

public sealed class M03CandidateResultMapperTests
{
    [Fact]
    public void Replay_inconsistente_es_conflicto_y_conserva_razon_y_hechos()
    {
        var candidate = Candidate();
        var source = Snapshot(
            M03CandidateOutcome.PendingDecision,
            M03CandidateReason.InconsistentReplay,
            candidate);

        var mapping = M03CandidateResultMapper.Map(candidate, source);

        Assert.Equal(
            M03CandidateMappingDisposition.MappedWithoutCommercialEffect,
            mapping.Disposition);
        Assert.Equal(
            ProviderCommercialEffectOutcome.Conflict,
            mapping.Result!.Outcome);
        Assert.Equal("InconsistentReplay", mapping.Result.Reason);
        Assert.Same(candidate.Facts, mapping.ContradictoryFacts);
        Assert.Same(source, mapping.Source);
        Assert.Null(mapping.Result.Reference);
    }

    [Fact]
    public void Pendiente_general_exige_decision_sin_efecto()
    {
        var candidate = Candidate();
        var mapping = M03CandidateResultMapper.Map(
            candidate,
            Snapshot(
                M03CandidateOutcome.PendingDecision,
                M03CandidateReason.CommercialPolicyUnratified,
                candidate));

        Assert.Equal(
            ProviderCommercialEffectOutcome.DecisionRequired,
            mapping.Result!.Outcome);
        Assert.Equal("CommercialPolicyUnratified", mapping.Result.Reason);
        Assert.Null(mapping.Result.Reference);
        Assert.Null(mapping.ContradictoryFacts);
    }

    [Fact]
    public void Rechazo_con_causa_conservable_es_indeterminado_provisional()
    {
        var candidate = Candidate();
        var mapping = M03CandidateResultMapper.Map(
            candidate,
            Snapshot(
                M03CandidateOutcome.Rejected,
                M03CandidateReason.UnauthenticatedEvent,
                candidate));

        Assert.Equal(
            ProviderCommercialEffectOutcome.Indeterminate,
            mapping.Result!.Outcome);
        Assert.Equal("UnauthenticatedEvent", mapping.Result.Reason);
        Assert.Null(mapping.Result.Reference);
    }

    [Fact]
    public void Rechazo_sin_causa_conservable_queda_bloqueado()
    {
        var candidate = Candidate();
        var mapping = M03CandidateResultMapper.Map(
            candidate,
            Snapshot(
                M03CandidateOutcome.Rejected,
                (M03CandidateReason)999,
                candidate));

        Assert.Equal(M03CandidateMappingDisposition.Blocked, mapping.Disposition);
        Assert.Equal(
            "m03_candidate_rejection_cause_not_preservable",
            mapping.BlockReason);
        Assert.Null(mapping.Result);
    }

    [Fact]
    public void Resultado_desconocido_es_indeterminado_sin_efecto()
    {
        var candidate = Candidate();
        var mapping = M03CandidateResultMapper.Map(
            candidate,
            Snapshot(
                (M03CandidateOutcome)999,
                M03CandidateReason.InvalidAmount,
                candidate));

        Assert.Equal(
            ProviderCommercialEffectOutcome.Indeterminate,
            mapping.Result!.Outcome);
        Assert.Equal("m03_candidate_unknown_outcome", mapping.Result.Reason);
        Assert.Null(mapping.Result.Reference);
    }

    [Fact]
    public void Identidad_no_equivalente_es_indeterminada_y_no_se_normaliza()
    {
        var candidate = Candidate();
        var source = Snapshot(
            M03CandidateOutcome.PendingDecision,
            M03CandidateReason.CommercialPolicyUnratified,
            candidate) with
        {
            Identity = new(
                candidate.Identity.Reception.Provider.ToUpperInvariant(),
                $" {candidate.Identity.Reception.ProviderEventId}",
                candidate.Identity.Settlement.Value)
        };

        var mapping = M03CandidateResultMapper.Map(candidate, source);

        Assert.Equal(
            ProviderCommercialEffectOutcome.Indeterminate,
            mapping.Result!.Outcome);
        Assert.Equal(
            "m03_candidate_identity_unverifiable",
            mapping.Result.Reason);
        Assert.Null(mapping.Result.Reference);
    }

    [Fact]
    public void Liquidacion_no_equivalente_es_identidad_no_verificable()
    {
        var candidate = Candidate();
        var source = Snapshot(
            M03CandidateOutcome.PendingDecision,
            M03CandidateReason.CommercialPolicyUnratified,
            candidate) with
        {
            Identity = new(
                candidate.Identity.Reception.Provider,
                candidate.Identity.Reception.ProviderEventId,
                $"{candidate.Identity.Settlement.Value} ")
        };

        var mapping = M03CandidateResultMapper.Map(candidate, source);

        Assert.Equal(
            ProviderCommercialEffectOutcome.Indeterminate,
            mapping.Result!.Outcome);
        Assert.Equal(
            "m03_candidate_identity_unverifiable",
            mapping.Result.Reason);
        Assert.Null(mapping.Result.Reference);
    }

    [Fact]
    public void Desenlace_y_razon_incompatibles_no_se_mapean_por_desenlace()
    {
        var candidate = Candidate();
        var mapping = M03CandidateResultMapper.Map(
            candidate,
            Snapshot(
                M03CandidateOutcome.PendingDecision,
                M03CandidateReason.InvalidAmount,
                candidate));

        Assert.Equal(
            ProviderCommercialEffectOutcome.Indeterminate,
            mapping.Result!.Outcome);
        Assert.Equal(
            "m03_candidate_invalid_outcome_reason_pair",
            mapping.Result.Reason);
        Assert.Equal(
            M03CandidateReason.InvalidAmount,
            mapping.Source!.Reason);
    }

    [Theory]
    [InlineData((int)M03CandidateOutcome.AppliedDurable)]
    [InlineData((int)M03CandidateOutcome.AlreadyAppliedEquivalent)]
    public void Todo_desenlace_positivo_queda_bloqueado(
        int outcomeValue)
    {
        var candidate = Candidate();
        var mapping = M03CandidateResultMapper.Map(
            candidate,
            Snapshot(
                (M03CandidateOutcome)outcomeValue,
                M03CandidateReason.RatifiedAndPersisted,
                candidate,
                "forbidden-durable-reference"));

        Assert.Equal(M03CandidateMappingDisposition.Blocked, mapping.Disposition);
        Assert.Equal(
            "m03_candidate_positive_outcome_not_authorized",
            mapping.BlockReason);
        Assert.Null(mapping.Result);
    }

    [Fact]
    public void Referencia_durable_en_resultado_no_positivo_es_invalida()
    {
        var candidate = Candidate();
        var mapping = M03CandidateResultMapper.Map(
            candidate,
            Snapshot(
                M03CandidateOutcome.PendingDecision,
                M03CandidateReason.CommercialPolicyUnratified,
                candidate,
                "must-not-be-used"));

        Assert.Equal(
            ProviderCommercialEffectOutcome.Indeterminate,
            mapping.Result!.Outcome);
        Assert.Equal(
            "m03_candidate_unexpected_durable_reference",
            mapping.Result.Reason);
        Assert.Null(mapping.Result.Reference);
    }

    [Fact]
    public void Fallo_conserva_causa_opaca_sin_acreditar_efecto()
    {
        const string cause = "m03-timeout / attempt 01";

        var mapping = M03CandidateResultMapper.MapFailure(cause);

        Assert.Equal(
            ProviderCommercialEffectOutcome.Indeterminate,
            mapping.Result!.Outcome);
        Assert.Equal("m03_candidate_call_failed", mapping.Result.Reason);
        Assert.Equal(cause, mapping.SourceFailure);
        Assert.Null(mapping.Result.Reference);
    }

    private static ProviderCommercialEffectCandidate Candidate()
    {
        var evidence = PaymentTestData.Evidence(
            provider: "Provider-A ",
            eventId: " Event/01#A ",
            settlementId: " Settlement/01#A ");
        return ProviderCommercialEffectCandidate.From(
            Guid.CreateVersion7(),
            evidence,
            PaymentTestData.Facts(evidence));
    }

    private static M03CandidateResultSnapshot Snapshot(
        M03CandidateOutcome outcome,
        M03CandidateReason reason,
        ProviderCommercialEffectCandidate candidate,
        string? durableEffectReference = null)
        => new(
            outcome,
            reason,
            new(
                candidate.Identity.Reception.Provider,
                candidate.Identity.Reception.ProviderEventId,
                candidate.Identity.Settlement.Value),
            durableEffectReference);
}
