using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Reúne únicamente datos ya acreditados. La ausencia o ambigüedad de una
/// costura deja el inbox en decisión requerida antes de invocar a M03.
/// </summary>
internal sealed class ProviderCommercialCandidateSource(
    IProviderSettlementFactsSource factsSource,
    IProviderVerifiedEvidenceReferenceSource evidenceReferenceSource,
    IProviderAuthorizedOrderCorrelationSource orderCorrelationSource)
    : IProviderCommercialCandidateSource
{
    public async ValueTask<ProviderCommercialCandidatePreparation> PrepareAsync(
        Guid inboxEventId,
        CancellationToken cancellationToken)
    {
        if (inboxEventId == Guid.Empty)
        {
            throw new ArgumentException(
                "La preparación exige inbox.",
                nameof(inboxEventId));
        }

        var facts = await factsSource.LoadCandidateFactsAsync(
            inboxEventId,
            cancellationToken);
        if (facts is null)
        {
            return ProviderCommercialCandidatePreparation.DecisionRequired(
                "verified_settlement_facts_missing");
        }

        var references = await evidenceReferenceSource.LoadAsync(
            inboxEventId,
            cancellationToken);
        if (references is null)
        {
            return ProviderCommercialCandidatePreparation.DecisionRequired(
                "verified_evidence_reference_source_invalid");
        }

        var matchingReferences = references
            .Where(reference => reference is not null
                && reference.Matches(facts.Evidence))
            .ToArray();
        if (matchingReferences.Length == 0)
        {
            return ProviderCommercialCandidatePreparation.DecisionRequired(
                "verified_evidence_reference_missing");
        }

        if (matchingReferences.Length != 1)
        {
            return ProviderCommercialCandidatePreparation.DecisionRequired(
                "verified_evidence_reference_ambiguous");
        }

        var identity = PaymentIdempotencyKeys.From(facts.Evidence);
        var correlation = await orderCorrelationSource.LoadAsync(
            inboxEventId,
            identity,
            cancellationToken);
        if (correlation is null)
        {
            return ProviderCommercialCandidatePreparation.DecisionRequired(
                "authorized_order_correlation_missing");
        }

        if (correlation.Identity != identity)
        {
            return ProviderCommercialCandidatePreparation.DecisionRequired(
                "authorized_order_correlation_identity_mismatch");
        }

        return ProviderCommercialCandidatePreparation.Ready(
            ProviderCommercialEffectCandidate.From(
                inboxEventId,
                facts.Evidence,
                facts,
                correlation,
                matchingReferences[0]));
    }
}

/// <summary>
/// Barrera explícita de FASE A. Será sustituida sólo cuando exista una fuente
/// autorizada de correlación; nunca fabrica un OrderId.
/// </summary>
internal sealed class UnavailableProviderAuthorizedOrderCorrelationSource
    : IProviderAuthorizedOrderCorrelationSource
{
    public ValueTask<ProviderAuthorizedOrderCorrelation?> LoadAsync(
        Guid inboxEventId,
        PaymentIdempotencyKeys identity,
        CancellationToken cancellationToken)
    {
        if (inboxEventId == Guid.Empty)
        {
            throw new ArgumentException(
                "La correlación exige inbox.",
                nameof(inboxEventId));
        }

        ArgumentNullException.ThrowIfNull(identity);
        return ValueTask.FromResult<ProviderAuthorizedOrderCorrelation?>(null);
    }
}
