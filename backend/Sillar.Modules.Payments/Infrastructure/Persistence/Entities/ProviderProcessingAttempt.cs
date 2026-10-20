namespace Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

/// <summary>Intento append-only asociado a un lease del inbox.</summary>
internal sealed class ProviderProcessingAttempt
{
    private ProviderProcessingAttempt()
    {
    }

    internal Guid ProcessingAttemptId { get; private set; }
    internal Guid InboxEventId { get; private set; }
    internal int AttemptNumber { get; private set; }
    internal Guid LeaseToken { get; private set; }
    internal DateTimeOffset StartedAt { get; private set; }
    internal DateTimeOffset? CompletedAt { get; private set; }
    internal string Outcome { get; private set; } = null!;
    internal string? FailureCode { get; private set; }

    internal static ProviderProcessingAttempt Start(
        Guid inboxEventId,
        int attemptNumber,
        Guid leaseToken,
        DateTimeOffset startedAt)
    {
        if (inboxEventId == Guid.Empty
            || attemptNumber <= 0
            || leaseToken == Guid.Empty)
        {
            throw new ArgumentException(
                "El intento exige inbox, número positivo y lease.");
        }

        return new ProviderProcessingAttempt
        {
            ProcessingAttemptId = Guid.CreateVersion7(),
            InboxEventId = inboxEventId,
            AttemptNumber = attemptNumber,
            LeaseToken = leaseToken,
            StartedAt = ProviderPersistenceGuard.RequiredInstant(
                startedAt, nameof(startedAt)),
            Outcome = ProviderAttemptOutcomes.Running
        };
    }

    internal void Complete(string outcome, DateTimeOffset completedAt, string? failureCode)
    {
        if (Outcome != ProviderAttemptOutcomes.Running)
        {
            throw new InvalidOperationException(
                "Un intento terminado no puede reescribirse.");
        }

        if (outcome is not ProviderAttemptOutcomes.Succeeded
            and not ProviderAttemptOutcomes.Failed
            and not ProviderAttemptOutcomes.Indeterminate)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (completedAt < StartedAt)
        {
            throw new ArgumentException(
                "Un intento no puede terminar antes de empezar.",
                nameof(completedAt));
        }

        if (outcome == ProviderAttemptOutcomes.Succeeded
            && failureCode is not null)
        {
            throw new ArgumentException(
                "Un intento exitoso no admite fallo.",
                nameof(failureCode));
        }

        if (outcome != ProviderAttemptOutcomes.Succeeded
            && string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException(
                "Un intento no exitoso exige código de fallo.",
                nameof(failureCode));
        }

        Outcome = outcome;
        CompletedAt = completedAt;
        FailureCode = failureCode;
    }
}
