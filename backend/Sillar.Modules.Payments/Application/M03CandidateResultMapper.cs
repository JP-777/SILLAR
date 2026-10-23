using Sillar.Modules.Payments.Application.Contracts;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Traduce respuestas candidatas de M03 sin habilitar efectos comerciales. La
/// integración futura deberá adaptar los tipos públicos reales a esta vista y
/// seguirá bloqueada hasta que exista autorización para resultados positivos.
/// </summary>
internal static class M03CandidateResultMapper
{
    private const string PositiveOutcomeBlocked =
        "m03_candidate_positive_outcome_not_authorized";
    private const string RejectionCauseUnavailable =
        "m03_candidate_rejection_cause_not_preservable";
    private const string UnknownOutcome =
        "m03_candidate_unknown_outcome";
    private const string InvalidOutcomeReasonPair =
        "m03_candidate_invalid_outcome_reason_pair";
    private const string IdentityUnverifiable =
        "m03_candidate_identity_unverifiable";
    private const string UnexpectedDurableReference =
        "m03_candidate_unexpected_durable_reference";
    private const string MissingResponse =
        "m03_candidate_response_missing";
    private const string CallFailed =
        "m03_candidate_call_failed";

    internal static M03CandidateMapping Map(
        ProviderCommercialEffectCandidate candidate,
        M03CandidateResultSnapshot? source)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (source is null)
        {
            return Indeterminate(MissingResponse, null);
        }

        // Incluso una respuesta que afirme durabilidad queda bloqueada: FASE A
        // carece de contrato ratificado y de evidencia durable entre procesos.
        if (source.Outcome is M03CandidateOutcome.AppliedDurable
            or M03CandidateOutcome.AlreadyAppliedEquivalent)
        {
            return M03CandidateMapping.Blocked(
                PositiveOutcomeBlocked,
                source);
        }

        if (!Enum.IsDefined(source.Outcome))
        {
            return Indeterminate(UnknownOutcome, source);
        }

        // Un rechazo sin una razón publicada no puede degradarse sin perder su
        // causa original. Las demás combinaciones inválidas se tratan abajo
        // como indeterminadas, conservando la respuesta fuente completa.
        if (source.Outcome == M03CandidateOutcome.Rejected
            && !Enum.IsDefined(source.Reason))
        {
            return M03CandidateMapping.Blocked(
                RejectionCauseUnavailable,
                source);
        }

        if (!Enum.IsDefined(source.Reason))
        {
            return Indeterminate(InvalidOutcomeReasonPair, source);
        }

        if (!IdentityMatches(candidate, source.Identity))
        {
            return Indeterminate(IdentityUnverifiable, source);
        }

        if (source.DurableEffectReference is not null)
        {
            return Indeterminate(UnexpectedDurableReference, source);
        }

        if (source.Outcome == M03CandidateOutcome.PendingDecision
            && source.Reason == M03CandidateReason.InconsistentReplay)
        {
            return M03CandidateMapping.WithoutCommercialEffect(
                ProviderCommercialEffectResult.Conflict(
                    source.Reason.ToString()),
                source,
                contradictoryFacts: candidate.Facts);
        }

        if (source.Outcome == M03CandidateOutcome.PendingDecision
            && IsPendingReason(source.Reason))
        {
            return M03CandidateMapping.WithoutCommercialEffect(
                ProviderCommercialEffectResult.DecisionRequired(
                    source.Reason.ToString()),
                source);
        }

        if (source.Outcome == M03CandidateOutcome.Rejected
            && IsPreservableRejection(source.Reason))
        {
            return M03CandidateMapping.WithoutCommercialEffect(
                ProviderCommercialEffectResult.Indeterminate(
                    source.Reason.ToString()),
                source);
        }

        return Indeterminate(InvalidOutcomeReasonPair, source);
    }

    internal static M03CandidateMapping MapFailure(string? originalCause)
    {
        var preservedCause = string.IsNullOrWhiteSpace(originalCause)
            ? null
            : originalCause;
        return M03CandidateMapping.WithoutCommercialEffect(
            ProviderCommercialEffectResult.Indeterminate(CallFailed),
            null,
            preservedCause);
    }

    private static M03CandidateMapping Indeterminate(
        string reason,
        M03CandidateResultSnapshot? source)
        => M03CandidateMapping.WithoutCommercialEffect(
            ProviderCommercialEffectResult.Indeterminate(reason),
            source);

    private static bool IdentityMatches(
        ProviderCommercialEffectCandidate candidate,
        M03CandidateIdentity? observed)
        => observed is not null
            && StringComparer.Ordinal.Equals(
                observed.Provider,
                candidate.Identity.Reception.Provider)
            && StringComparer.Ordinal.Equals(
                observed.ProviderEventId,
                candidate.Identity.Reception.ProviderEventId)
            && StringComparer.Ordinal.Equals(
                observed.ExternalSettlementId,
                candidate.Identity.Settlement.Value);

    private static bool IsPreservableRejection(M03CandidateReason reason)
        => reason is M03CandidateReason.InvalidIdentity
            or M03CandidateReason.InvalidAmount
            or M03CandidateReason.UnauthenticatedEvent;

    private static bool IsPendingReason(M03CandidateReason reason)
        => reason is M03CandidateReason.MissingCurrency
            or M03CandidateReason.CommercialPolicyUnratified
            or M03CandidateReason.DurableIdentityPolicyUnratified
            or M03CandidateReason.DurableWriteUnavailable
            or M03CandidateReason.EquivalentReplayWithoutDurableEffect;
}
