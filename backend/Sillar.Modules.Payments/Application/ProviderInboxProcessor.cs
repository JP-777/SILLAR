using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Coordina un único intento del inbox. El resultado sólo se presenta como
/// acreditado cuando el puerto lo confirma y el repositorio conserva esa
/// confirmación bajo el mismo lease.
/// </summary>
internal sealed class ProviderInboxProcessor(
    IProviderInboxRepository repository,
    IProviderSettlementFactsSource factsSource,
    IProviderCommercialEffectPort commercialPort)
    : IProviderInboxProcessingService
{
    public async ValueTask<ProviderInboxProcessResult> ProcessAsync(
        Guid inboxEventId,
        DateTimeOffset attemptedAt,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        if (inboxEventId == Guid.Empty
            || attemptedAt == default
            || leaseDuration <= TimeSpan.Zero
            || retryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "El procesamiento exige inbox, instante y demoras positivas.");
        }

        var leaseToken = Guid.CreateVersion7();
        var claim = await repository.TryClaimAsync(
            inboxEventId,
            leaseToken,
            attemptedAt,
            attemptedAt.Add(leaseDuration),
            cancellationToken);
        if (claim.Outcome != ProviderInboxClaimOutcome.Acquired)
        {
            return new(
                MapClaimOutcome(claim.Outcome),
                null,
                claim.Status?.AttemptCount,
                claim.Status?.ProcessingState.ToString());
        }

        var claimed = claim.Status!;
        var facts = await factsSource.LoadCandidateFactsAsync(
            claimed.InboxEventId,
            cancellationToken);
        if (facts is null)
        {
            return await CompleteWithoutCommercialCallAsync(
                claimed,
                leaseToken,
                attemptedAt,
                "verified_settlement_facts_missing",
                cancellationToken);
        }

        var candidate = ProviderCommercialEffectCandidate.From(
            claimed.InboxEventId,
            claimed.Evidence,
            facts);
        var result = await commercialPort.TryApplyAsync(
            candidate,
            cancellationToken);
        if (result is null || !Enum.IsDefined(result.Outcome))
        {
            throw new InvalidOperationException(
                "El puerto comercial devolvió un resultado inválido.");
        }

        if (result.Outcome == ProviderCommercialEffectOutcome.AlreadyApplied
            && ProviderSettlementFactClassifier.Classify(
                candidate.Facts,
                [result.ConfirmedFacts!])
                != ProviderSettlementFactDecision.Duplicate)
        {
            result = ProviderCommercialEffectResult.Conflict(
                "already_applied_facts_divergent");
        }

        var completion = result.Outcome switch
        {
            ProviderCommercialEffectOutcome.Applied =>
                ProviderInboxCompletion.Applied(
                    inboxEventId,
                    leaseToken,
                    attemptedAt,
                    result.Reference),
            ProviderCommercialEffectOutcome.AlreadyApplied =>
                ProviderInboxCompletion.AlreadyApplied(
                    inboxEventId,
                    leaseToken,
                    attemptedAt,
                    result.Reference),
            ProviderCommercialEffectOutcome.Conflict =>
                ProviderInboxCompletion.Conflict(
                    inboxEventId,
                    leaseToken,
                    attemptedAt,
                    result.Reason),
            ProviderCommercialEffectOutcome.DecisionRequired =>
                ProviderInboxCompletion.DecisionRequired(
                    inboxEventId,
                    leaseToken,
                    attemptedAt,
                    result.Reason),
            ProviderCommercialEffectOutcome.Indeterminate =>
                ProviderInboxCompletion.Indeterminate(
                    inboxEventId,
                    leaseToken,
                    attemptedAt,
                    attemptedAt.Add(retryDelay),
                    result.Reason),
            _ => throw new InvalidOperationException(
                "El resultado comercial no tiene transición.")
        };

        if (!await repository.TryCompleteAsync(
                completion,
                cancellationToken))
        {
            return new(
                ProviderInboxProcessOutcome.ConfirmationNotPersisted,
                leaseToken,
                claimed.AttemptCount,
                "lease_or_concurrency_conflict");
        }

        return new(
            MapCommercialOutcome(result.Outcome),
            leaseToken,
            claimed.AttemptCount,
            result.Reason ?? result.Reference,
            result.OrderWasExpired);
    }

    public ValueTask<bool> TryReleaseAfterReviewAsync(
        Guid inboxEventId,
        long expectedRevision,
        DateTimeOffset reviewedAt,
        CancellationToken cancellationToken = default)
        => repository.TryMarkReadyAsync(
            inboxEventId,
            expectedRevision,
            reviewedAt,
            cancellationToken);

    public ValueTask<bool> TryRecoverExpiredLeaseAsync(
        Guid inboxEventId,
        DateTimeOffset recoveredAt,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        if (recoveredAt == default || retryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "La recuperación exige instante y demora positiva.");
        }

        return repository.TryRecoverExpiredLeaseAsync(
            inboxEventId,
            recoveredAt,
            recoveredAt.Add(retryDelay),
            cancellationToken);
    }

    private static ProviderInboxProcessOutcome MapClaimOutcome(
        ProviderInboxClaimOutcome outcome)
        => outcome switch
        {
            ProviderInboxClaimOutcome.NotFound =>
                ProviderInboxProcessOutcome.NotFound,
            ProviderInboxClaimOutcome.NotReady =>
                ProviderInboxProcessOutcome.NotReady,
            ProviderInboxClaimOutcome.Contended =>
                ProviderInboxProcessOutcome.Contended,
            _ => throw new InvalidOperationException(
                "Un claim adquirido debe continuar el procesamiento.")
        };

    private async ValueTask<ProviderInboxProcessResult>
        CompleteWithoutCommercialCallAsync(
            ProviderInboxStatus claimed,
            Guid leaseToken,
            DateTimeOffset completedAt,
            string reason,
            CancellationToken cancellationToken)
    {
        var completion = ProviderInboxCompletion.DecisionRequired(
            claimed.InboxEventId,
            leaseToken,
            completedAt,
            reason);
        if (!await repository.TryCompleteAsync(completion, cancellationToken))
        {
            return new(
                ProviderInboxProcessOutcome.ConfirmationNotPersisted,
                leaseToken,
                claimed.AttemptCount,
                "lease_or_concurrency_conflict");
        }

        return new(
            ProviderInboxProcessOutcome.DecisionRequired,
            leaseToken,
            claimed.AttemptCount,
            reason);
    }

    private static ProviderInboxProcessOutcome MapCommercialOutcome(
        ProviderCommercialEffectOutcome outcome)
        => outcome switch
        {
            ProviderCommercialEffectOutcome.Applied =>
                ProviderInboxProcessOutcome.Applied,
            ProviderCommercialEffectOutcome.AlreadyApplied =>
                ProviderInboxProcessOutcome.AlreadyApplied,
            ProviderCommercialEffectOutcome.Conflict =>
                ProviderInboxProcessOutcome.Conflict,
            ProviderCommercialEffectOutcome.DecisionRequired =>
                ProviderInboxProcessOutcome.DecisionRequired,
            ProviderCommercialEffectOutcome.Indeterminate =>
                ProviderInboxProcessOutcome.Indeterminate,
            _ => throw new InvalidOperationException(
                "El resultado comercial no tiene salida de proceso.")
        };
}
