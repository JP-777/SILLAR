// COPIA LITERAL de backend/Sillar.Modules.Payments.Tests/ProviderInboxProcessorTests.cs:605-877 @ 799cfe82,
// fuera de la clase contenedora. Doble de persistencia del inbox: aísla PostgreSQL, que no está autorizado.
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Tests;

internal sealed class MemoryInboxRepository(ProviderInboxStatus status)
    : IProviderInboxRepository
{
    private readonly object _gate = new();
    private readonly List<ProviderInboxAttemptStatus> _attempts = [];

    internal ProviderInboxStatus Status { get; private set; } = status;
    internal bool RejectCompletion { get; set; }

    public ValueTask<ProviderInboxStatus?> GetStatusAsync(
        Guid inboxEventId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<ProviderInboxStatus?>(
                inboxEventId == Status.InboxEventId ? Status : null);
        }
    }

    public ValueTask<IReadOnlyList<ProviderInboxAttemptStatus>>
        LoadAttemptsAsync(
            Guid inboxEventId,
            CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<IReadOnlyList<
                ProviderInboxAttemptStatus>>(_attempts.ToArray());
        }
    }

    public ValueTask<bool> TryMarkReadyAsync(
        Guid inboxEventId,
        long expectedRevision,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (inboxEventId != Status.InboxEventId
                || expectedRevision != Status.Revision
                || Status.ProcessingState !=
                    ProviderInboxProcessingState.PendingReview)
            {
                return ValueTask.FromResult(false);
            }

            Status = Status with
            {
                ProcessingState = ProviderInboxProcessingState.Ready,
                NextAttemptAt = null,
                Revision = Status.Revision + 1
            };
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<ProviderInboxClaim> TryClaimAsync(
        Guid inboxEventId,
        Guid leaseToken,
        DateTimeOffset claimedAt,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (inboxEventId != Status.InboxEventId)
            {
                return ValueTask.FromResult(ProviderInboxClaim.From(
                    ProviderInboxClaimOutcome.NotFound));
            }

            var ready = Status.ProcessingState ==
                    ProviderInboxProcessingState.Ready
                || (Status.ProcessingState ==
                        ProviderInboxProcessingState.Indeterminate
                    && Status.NextAttemptAt <= claimedAt);
            if (!ready)
            {
                return ValueTask.FromResult(ProviderInboxClaim.From(
                    ProviderInboxClaimOutcome.NotReady,
                    Status));
            }

            var attemptNumber = Status.AttemptCount + 1;
            Status = Status with
            {
                ProcessingState = ProviderInboxProcessingState.Processing,
                AttemptCount = attemptNumber,
                NextAttemptAt = null,
                LeaseToken = leaseToken,
                LeaseUntil = leaseUntil,
                Revision = Status.Revision + 1
            };
            _attempts.Add(new(
                Guid.CreateVersion7(),
                inboxEventId,
                attemptNumber,
                leaseToken,
                claimedAt,
                null,
                "running",
                null));
            return ValueTask.FromResult(ProviderInboxClaim.From(
                ProviderInboxClaimOutcome.Acquired,
                Status));
        }
    }

    public ValueTask<bool> TryCompleteAsync(
        ProviderInboxCompletion completion,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (RejectCompletion
                || Status.InboxEventId != completion.InboxEventId
                || Status.LeaseToken != completion.LeaseToken
                || Status.ProcessingState !=
                    ProviderInboxProcessingState.Processing)
            {
                return ValueTask.FromResult(false);
            }

            Status = completion.Outcome switch
            {
                ProviderInboxCompletionOutcome.Applied => Status with
                {
                    ProcessingState = ProviderInboxProcessingState.Completed,
                    CommercialState = ProviderInboxCommercialState.Applied,
                    CommercialEffectReference = completion.Reference,
                    LastFailureCode = null,
                    LeaseToken = null,
                    LeaseUntil = null,
                    Revision = Status.Revision + 1
                },
                ProviderInboxCompletionOutcome.AlreadyApplied => Status with
                {
                    ProcessingState = ProviderInboxProcessingState.Completed,
                    CommercialState =
                        ProviderInboxCommercialState.AlreadyApplied,
                    CommercialEffectReference = completion.Reference,
                    LastFailureCode = null,
                    LeaseToken = null,
                    LeaseUntil = null,
                    Revision = Status.Revision + 1
                },
                ProviderInboxCompletionOutcome.Rejected => Status with
                {
                    ProcessingState = ProviderInboxProcessingState.Held,
                    CommercialState = ProviderInboxCommercialState.Rejected,
                    HoldReason = completion.Reason,
                    LastFailureCode = completion.Reason,
                    LeaseToken = null,
                    LeaseUntil = null,
                    Revision = Status.Revision + 1
                },
                ProviderInboxCompletionOutcome.Conflict => Status with
                {
                    ProcessingState = ProviderInboxProcessingState.Held,
                    CommercialState = ProviderInboxCommercialState.Conflict,
                    HoldReason = completion.Reason,
                    LastFailureCode = completion.Reason,
                    LeaseToken = null,
                    LeaseUntil = null,
                    Revision = Status.Revision + 1
                },
                ProviderInboxCompletionOutcome.DecisionRequired => Status with
                {
                    ProcessingState = ProviderInboxProcessingState.Held,
                    CommercialState =
                        ProviderInboxCommercialState.DecisionRequired,
                    HoldReason = completion.Reason,
                    LastFailureCode = completion.Reason,
                    LeaseToken = null,
                    LeaseUntil = null,
                    Revision = Status.Revision + 1
                },
                ProviderInboxCompletionOutcome.Indeterminate => Status with
                {
                    ProcessingState = completion.RetryAt is null
                        ? ProviderInboxProcessingState.Held
                        : ProviderInboxProcessingState.Indeterminate,
                    CommercialState =
                        ProviderInboxCommercialState.Indeterminate,
                    HoldReason = completion.RetryAt is null
                        ? completion.Reason
                        : null,
                    LastFailureCode = completion.Reason,
                    NextAttemptAt = completion.RetryAt,
                    LeaseToken = null,
                    LeaseUntil = null,
                    Revision = Status.Revision + 1
                },
                _ => throw new InvalidOperationException()
            };
            CompleteAttempt(completion);
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<bool> TryRecoverExpiredLeaseAsync(
        Guid inboxEventId,
        DateTimeOffset recoveredAt,
        DateTimeOffset? retryAt,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (inboxEventId != Status.InboxEventId
                || Status.ProcessingState !=
                    ProviderInboxProcessingState.Processing
                || Status.LeaseUntil > recoveredAt)
            {
                return ValueTask.FromResult(false);
            }

            var lease = Status.LeaseToken!.Value;
            Status = Status with
            {
                ProcessingState = retryAt is null
                    ? ProviderInboxProcessingState.Held
                    : ProviderInboxProcessingState.Indeterminate,
                CommercialState = ProviderInboxCommercialState.Indeterminate,
                LastFailureCode = "lease_expired_before_confirmation",
                HoldReason = retryAt is null
                    ? "lease_expired_before_confirmation"
                    : null,
                NextAttemptAt = retryAt,
                LeaseToken = null,
                LeaseUntil = null,
                Revision = Status.Revision + 1
            };
            var completion = ProviderInboxCompletion.Indeterminate(
                inboxEventId,
                lease,
                recoveredAt,
                retryAt,
                "lease_expired_before_confirmation");
            CompleteAttempt(completion);
            return ValueTask.FromResult(true);
        }
    }

    private void CompleteAttempt(ProviderInboxCompletion completion)
    {
        var index = _attempts.FindIndex(item =>
            item.InboxEventId == completion.InboxEventId
            && item.LeaseToken == completion.LeaseToken
            && item.Outcome == "running");
        if (index < 0)
        {
            throw new InvalidOperationException("No existe intento activo.");
        }

        var current = _attempts[index];
        _attempts[index] = current with
        {
            CompletedAt = completion.CompletedAt,
            Outcome = completion.Outcome is ProviderInboxCompletionOutcome.Applied
                    or ProviderInboxCompletionOutcome.AlreadyApplied
                    ? "succeeded"
                    : completion.Outcome is ProviderInboxCompletionOutcome.Conflict
                        or ProviderInboxCompletionOutcome.DecisionRequired
                        or ProviderInboxCompletionOutcome.Rejected
                        ? "held"
                        : "indeterminate",
            FailureCode = completion.Reason
        };
    }
}
