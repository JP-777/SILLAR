using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application.Contracts;

internal enum ProviderInboxProcessingState
{
    PendingReview,
    Held,
    Ready,
    Processing,
    Indeterminate,
    Completed
}

internal enum ProviderInboxCommercialState
{
    NotRequested,
    Indeterminate,
    Conflict,
    DecisionRequired,
    Applied,
    AlreadyApplied
}

internal sealed record ProviderInboxStatus(
    Guid InboxEventId,
    VerifiedExternalPaymentEvidence Evidence,
    ProviderInboxProcessingState ProcessingState,
    ProviderInboxCommercialState CommercialState,
    string? HoldReason,
    int AttemptCount,
    DateTimeOffset? NextAttemptAt,
    Guid? LeaseToken,
    DateTimeOffset? LeaseUntil,
    string? CommercialEffectReference,
    string? LastFailureCode,
    long Revision);

internal sealed record ProviderInboxAttemptStatus(
    Guid ProcessingAttemptId,
    Guid InboxEventId,
    int AttemptNumber,
    Guid LeaseToken,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Outcome,
    string? FailureCode);

internal enum ProviderInboxClaimOutcome
{
    Acquired,
    NotFound,
    NotReady,
    Contended
}

internal sealed record ProviderInboxClaim(
    ProviderInboxClaimOutcome Outcome,
    ProviderInboxStatus? Status)
{
    internal static ProviderInboxClaim From(
        ProviderInboxClaimOutcome outcome,
        ProviderInboxStatus? status = null)
        => outcome == ProviderInboxClaimOutcome.Acquired && status is null
            ? throw new ArgumentException(
                "Un lease adquirido exige estado.",
                nameof(status))
            : new(outcome, status);
}

internal enum ProviderInboxCompletionOutcome
{
    Applied,
    AlreadyApplied,
    Conflict,
    DecisionRequired,
    Indeterminate
}

internal sealed record ProviderInboxCompletion
{
    private ProviderInboxCompletion(
        Guid inboxEventId,
        Guid leaseToken,
        ProviderInboxCompletionOutcome outcome,
        DateTimeOffset completedAt,
        DateTimeOffset? retryAt,
        string? reference,
        string? reason)
    {
        InboxEventId = inboxEventId;
        LeaseToken = leaseToken;
        Outcome = outcome;
        CompletedAt = completedAt;
        RetryAt = retryAt;
        Reference = reference;
        Reason = reason;
    }

    internal Guid InboxEventId { get; }
    internal Guid LeaseToken { get; }
    internal ProviderInboxCompletionOutcome Outcome { get; }
    internal DateTimeOffset CompletedAt { get; }
    internal DateTimeOffset? RetryAt { get; }
    internal string? Reference { get; }
    internal string? Reason { get; }

    internal static ProviderInboxCompletion Applied(
        Guid inboxEventId,
        Guid leaseToken,
        DateTimeOffset completedAt,
        string? reference)
        => Create(
            inboxEventId,
            leaseToken,
            ProviderInboxCompletionOutcome.Applied,
            completedAt,
            null,
            reference,
            null);

    internal static ProviderInboxCompletion AlreadyApplied(
        Guid inboxEventId,
        Guid leaseToken,
        DateTimeOffset completedAt,
        string? reference)
        => Create(
            inboxEventId,
            leaseToken,
            ProviderInboxCompletionOutcome.AlreadyApplied,
            completedAt,
            null,
            reference,
            null);

    internal static ProviderInboxCompletion Conflict(
        Guid inboxEventId,
        Guid leaseToken,
        DateTimeOffset completedAt,
        string? reason)
        => Create(
            inboxEventId,
            leaseToken,
            ProviderInboxCompletionOutcome.Conflict,
            completedAt,
            null,
            null,
            reason);

    internal static ProviderInboxCompletion DecisionRequired(
        Guid inboxEventId,
        Guid leaseToken,
        DateTimeOffset completedAt,
        string? reason)
        => Create(
            inboxEventId,
            leaseToken,
            ProviderInboxCompletionOutcome.DecisionRequired,
            completedAt,
            null,
            null,
            reason);

    internal static ProviderInboxCompletion Indeterminate(
        Guid inboxEventId,
        Guid leaseToken,
        DateTimeOffset completedAt,
        DateTimeOffset retryAt,
        string? reason)
        => Create(
            inboxEventId,
            leaseToken,
            ProviderInboxCompletionOutcome.Indeterminate,
            completedAt,
            retryAt,
            null,
            reason);

    private static ProviderInboxCompletion Create(
        Guid inboxEventId,
        Guid leaseToken,
        ProviderInboxCompletionOutcome outcome,
        DateTimeOffset completedAt,
        DateTimeOffset? retryAt,
        string? reference,
        string? reason)
    {
        if (inboxEventId == Guid.Empty
            || leaseToken == Guid.Empty
            || completedAt == default)
        {
            throw new ArgumentException(
                "La finalización exige inbox, lease e instante.");
        }

        if (outcome is ProviderInboxCompletionOutcome.Applied
            or ProviderInboxCompletionOutcome.AlreadyApplied)
        {
            if (string.IsNullOrWhiteSpace(reference)
                || retryAt is not null
                || reason is not null)
            {
                throw new ArgumentException(
                    "La acreditación exige referencia y no admite fallo.");
            }
        }
        else if (string.IsNullOrWhiteSpace(reason) || reference is not null)
        {
            throw new ArgumentException(
                "Un resultado no acreditado exige razón y no admite referencia.");
        }

        if (outcome == ProviderInboxCompletionOutcome.Indeterminate)
        {
            if (retryAt is null || retryAt <= completedAt)
            {
                throw new ArgumentException(
                    "Un resultado indeterminado exige reintento futuro.");
            }
        }
        else if (retryAt is not null)
        {
            throw new ArgumentException(
                "Un resultado terminal no admite reintento.");
        }

        return new(
            inboxEventId,
            leaseToken,
            outcome,
            completedAt,
            retryAt,
            reference,
            reason);
    }
}

internal interface IProviderInboxRepository
{
    ValueTask<ProviderInboxStatus?> GetStatusAsync(
        Guid inboxEventId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ProviderInboxAttemptStatus>> LoadAttemptsAsync(
        Guid inboxEventId,
        CancellationToken cancellationToken);

    ValueTask<bool> TryMarkReadyAsync(
        Guid inboxEventId,
        long expectedRevision,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken);

    ValueTask<ProviderInboxClaim> TryClaimAsync(
        Guid inboxEventId,
        Guid leaseToken,
        DateTimeOffset claimedAt,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken);

    ValueTask<bool> TryCompleteAsync(
        ProviderInboxCompletion completion,
        CancellationToken cancellationToken);

    ValueTask<bool> TryRecoverExpiredLeaseAsync(
        Guid inboxEventId,
        DateTimeOffset recoveredAt,
        DateTimeOffset retryAt,
        CancellationToken cancellationToken);
}
