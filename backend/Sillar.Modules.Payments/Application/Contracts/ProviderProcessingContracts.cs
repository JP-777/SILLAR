namespace Sillar.Modules.Payments.Application.Contracts;

internal enum ProviderInboxProcessOutcome
{
    Applied,
    AlreadyApplied,
    Conflict,
    DecisionRequired,
    Indeterminate,
    NotFound,
    NotReady,
    Contended,
    ConfirmationNotPersisted
}

internal sealed record ProviderInboxProcessResult(
    ProviderInboxProcessOutcome Outcome,
    Guid? LeaseToken,
    int? AttemptNumber,
    string? Detail,
    bool? OrderWasExpired = null);

internal interface IProviderInboxProcessingService
{
    ValueTask<ProviderInboxProcessResult> ProcessAsync(
        Guid inboxEventId,
        DateTimeOffset attemptedAt,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default);

    ValueTask<bool> TryReleaseAfterReviewAsync(
        Guid inboxEventId,
        long expectedRevision,
        DateTimeOffset reviewedAt,
        CancellationToken cancellationToken = default);

    ValueTask<bool> TryRecoverExpiredLeaseAsync(
        Guid inboxEventId,
        DateTimeOffset recoveredAt,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default);
}
