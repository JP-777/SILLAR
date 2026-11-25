using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Adaptador candidato no registrado para consultar la frontera de liquidación
/// de M03. Toda respuesta atraviesa el mapper fail-closed de M11 y ningún
/// desenlace positivo candidato se expone como efecto comercial acreditado.
/// </summary>
internal sealed class M03ExternalSettlementCandidatePortAdapter(
    IExternalSettlementCandidate candidate)
    : IProviderCommercialEffectPort
{
    public async ValueTask<ProviderCommercialEffectResult> TryApplyAsync(
        ProviderCommercialEffectCandidate commercialCandidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commercialCandidate);
        var request = M03CandidateRequestMapper.Map(commercialCandidate);

        ExternalSettlementCandidateResult? source;
        try
        {
            source = await candidate.TryApplyAsync(
                new ExternalSettlementCandidateEvent(
                    request.Provider,
                    request.ProviderEventId,
                    request.ExternalSettlementId,
                    request.OrderId,
                    request.Amount,
                    request.Currency,
                    request.VerifiedEvidenceReference),
                cancellationToken);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return M03CandidateResultMapper.MapFailure(
                exception.GetType().FullName).Result!;
        }

        var mapping = M03CandidateResultMapper.Map(
            commercialCandidate,
            ToSnapshot(source, request));
        return mapping.Disposition switch
        {
            M03CandidateMappingDisposition.MappedWithoutCommercialEffect =>
                mapping.Result!,
            M03CandidateMappingDisposition.Blocked =>
                ProviderCommercialEffectResult.DecisionRequired(
                    mapping.BlockReason),
            _ => ProviderCommercialEffectResult.Indeterminate(
                "m03_candidate_mapping_disposition_unknown")
        };
    }

    private static M03CandidateResultSnapshot? ToSnapshot(
        ExternalSettlementCandidateResult? source,
        M03CandidateRequestSnapshot request)
    {
        if (source is null)
        {
            return null;
        }

        return new(
            ToLocalOutcome(source.Outcome),
            ToLocalReason(source.Reason),
            new(
                request.Provider,
                request.ProviderEventId,
                request.ExternalSettlementId),
            source.DurableEffectReference);
    }

    private static M03CandidateOutcome ToLocalOutcome(
        ExternalSettlementOutcome outcome)
        => outcome switch
        {
            ExternalSettlementOutcome.AppliedDurable =>
                M03CandidateOutcome.AppliedDurable,
            ExternalSettlementOutcome.AlreadyAppliedEquivalent =>
                M03CandidateOutcome.AlreadyAppliedEquivalent,
            ExternalSettlementOutcome.Rejected =>
                M03CandidateOutcome.Rejected,
            ExternalSettlementOutcome.PendingDecision =>
                M03CandidateOutcome.PendingDecision,
            _ => (M03CandidateOutcome)(int)outcome
        };

    private static M03CandidateReason ToLocalReason(
        ExternalSettlementReason reason)
        => reason switch
        {
            ExternalSettlementReason.InvalidIdentity =>
                M03CandidateReason.InvalidIdentity,
            ExternalSettlementReason.InvalidAmount =>
                M03CandidateReason.InvalidAmount,
            ExternalSettlementReason.MissingCurrency =>
                M03CandidateReason.MissingCurrency,
            ExternalSettlementReason.UnauthenticatedEvent =>
                M03CandidateReason.UnauthenticatedEvent,
            ExternalSettlementReason.CommercialPolicyUnratified =>
                M03CandidateReason.CommercialPolicyUnratified,
            ExternalSettlementReason.DurableIdentityPolicyUnratified =>
                M03CandidateReason.DurableIdentityPolicyUnratified,
            ExternalSettlementReason.DurableWriteUnavailable =>
                M03CandidateReason.DurableWriteUnavailable,
            ExternalSettlementReason.InconsistentReplay =>
                M03CandidateReason.InconsistentReplay,
            ExternalSettlementReason.RatifiedAndPersisted =>
                M03CandidateReason.RatifiedAndPersisted,
            ExternalSettlementReason.EquivalentReplayWithoutDurableEffect =>
                M03CandidateReason.EquivalentReplayWithoutDurableEffect,
            _ => (M03CandidateReason)(int)reason
        };
}
