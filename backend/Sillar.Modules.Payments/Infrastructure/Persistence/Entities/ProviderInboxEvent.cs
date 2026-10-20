using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

/// <summary>Evento canónico del inbox técnico local de M11.</summary>
internal sealed class ProviderInboxEvent
{
    private ProviderInboxEvent()
    {
    }

    internal Guid InboxEventId { get; private set; }
    internal string Provider { get; private set; } = null!;
    internal string ProviderEventId { get; private set; } = null!;
    internal string ExternalSettlementId { get; private set; } = null!;
    internal string BodySha256 { get; private set; } = null!;
    internal DateTimeOffset VerifiedAt { get; private set; }
    internal string ProcessingState { get; private set; } = null!;
    internal string? HoldReason { get; private set; }
    internal int AttemptCount { get; private set; }
    internal DateTimeOffset? NextAttemptAt { get; private set; }
    internal Guid? LeaseToken { get; private set; }
    internal DateTimeOffset? LeaseUntil { get; private set; }
    internal string CommercialEffectState { get; private set; } = null!;
    internal string? CommercialEffectReference { get; private set; }
    internal string? LastFailureCode { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal long Revision { get; private set; }

    internal static ProviderInboxEvent Create(
        VerifiedExternalPaymentEvidence evidence,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var instant = ProviderPersistenceGuard.RequiredInstant(
            createdAt, nameof(createdAt));

        return new ProviderInboxEvent
        {
            InboxEventId = Guid.CreateVersion7(),
            Provider = evidence.Provider,
            ProviderEventId = evidence.ProviderEventId,
            ExternalSettlementId = evidence.ExternalSettlementId,
            BodySha256 = evidence.BodySha256,
            VerifiedAt = evidence.VerifiedAt,
            ProcessingState = ProviderInboxStates.PendingReview,
            CommercialEffectState = ProviderCommercialEffectStates.NotRequested,
            CreatedAt = instant,
            UpdatedAt = instant,
            Revision = 1
        };
    }

    internal void Hold(string? reason, DateTimeOffset changedAt)
    {
        ProcessingState = ProviderInboxStates.Held;
        HoldReason = ProviderPersistenceGuard.OpaqueRequired(
            reason, nameof(reason));
        ClearLease();
        Touch(changedAt);
    }

    internal void MarkReady(DateTimeOffset changedAt)
    {
        if (ProcessingState is ProviderInboxStates.Held
            or ProviderInboxStates.Rejected
            or ProviderInboxStates.Completed)
        {
            throw new InvalidOperationException(
                "Un evento terminal o retenido no puede habilitarse automáticamente.");
        }

        ProcessingState = ProviderInboxStates.Ready;
        HoldReason = null;
        NextAttemptAt = null;
        Touch(changedAt);
    }

    internal void BeginAttempt(
        Guid leaseToken,
        DateTimeOffset leaseUntil,
        DateTimeOffset changedAt)
    {
        if (ProcessingState is not ProviderInboxStates.Ready
            and not ProviderInboxStates.Indeterminate)
        {
            throw new InvalidOperationException(
                "Sólo un evento listo o indeterminado puede reclamarse.");
        }

        if (leaseToken == Guid.Empty || leaseUntil <= changedAt)
        {
            throw new ArgumentException("El lease debe ser válido y futuro.");
        }

        ProcessingState = ProviderInboxStates.Processing;
        LeaseToken = leaseToken;
        LeaseUntil = leaseUntil;
        AttemptCount = checked(AttemptCount + 1);
        NextAttemptAt = null;
        Touch(changedAt);
    }

    internal void MarkIndeterminate(
        Guid leaseToken,
        string? failureCode,
        DateTimeOffset retryAt,
        DateTimeOffset changedAt)
    {
        EnsureLease(leaseToken);
        if (retryAt <= changedAt)
        {
            throw new ArgumentException(
                "El reintento debe quedar en el futuro.",
                nameof(retryAt));
        }

        ProcessingState = ProviderInboxStates.Indeterminate;
        CommercialEffectState = ProviderCommercialEffectStates.Indeterminate;
        LastFailureCode = ProviderPersistenceGuard.OpaqueRequired(
            failureCode, nameof(failureCode));
        NextAttemptAt = retryAt;
        ClearLease();
        Touch(changedAt);
    }

    internal void MarkCommercialEffectAccredited(
        Guid leaseToken,
        string? reference,
        DateTimeOffset changedAt)
    {
        EnsureLease(leaseToken);
        ProcessingState = ProviderInboxStates.Completed;
        CommercialEffectState = ProviderCommercialEffectStates.Accredited;
        CommercialEffectReference = ProviderPersistenceGuard.OpaqueRequired(
            reference, nameof(reference));
        LastFailureCode = null;
        NextAttemptAt = null;
        ClearLease();
        Touch(changedAt);
    }

    internal void MarkRejected(
        Guid leaseToken,
        string? failureCode,
        DateTimeOffset changedAt)
    {
        EnsureLease(leaseToken);
        ProcessingState = ProviderInboxStates.Rejected;
        CommercialEffectState = ProviderCommercialEffectStates.NotRequested;
        CommercialEffectReference = null;
        LastFailureCode = ProviderPersistenceGuard.OpaqueRequired(
            failureCode, nameof(failureCode));
        NextAttemptAt = null;
        ClearLease();
        Touch(changedAt);
    }

    internal bool CanBeClaimedAt(DateTimeOffset instant)
        => ProcessingState == ProviderInboxStates.Ready
            || (ProcessingState == ProviderInboxStates.Indeterminate
                && NextAttemptAt <= instant);

    internal bool HasExpiredLeaseAt(DateTimeOffset instant)
        => ProcessingState == ProviderInboxStates.Processing
            && LeaseUntil <= instant;

    private void EnsureLease(Guid leaseToken)
    {
        if (ProcessingState != ProviderInboxStates.Processing
            || LeaseToken != leaseToken
            || leaseToken == Guid.Empty)
        {
            throw new InvalidOperationException(
                "El resultado no corresponde al lease activo.");
        }
    }

    private void ClearLease()
    {
        LeaseToken = null;
        LeaseUntil = null;
    }

    private void Touch(DateTimeOffset changedAt)
    {
        UpdatedAt = ProviderPersistenceGuard.RequiredInstant(
            changedAt, nameof(changedAt));
        Revision = checked(Revision + 1);
    }
}
