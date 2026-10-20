using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;
using Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

namespace Sillar.Modules.Payments.Infrastructure.Persistence;

/// <summary>
/// Almacén técnico de M11. Cada operación guarda evento, recepción e intento en
/// un único <c>SaveChangesAsync</c>; puede participar en una transacción externa.
/// </summary>
internal sealed class EfProviderInboxRepository(PaymentsDbContext database)
    : IProviderReceptionLedger, IProviderInboxRepository
{
    public async ValueTask<IReadOnlyList<VerifiedExternalPaymentEvidence>>
        LoadKnownVerifiedEvidenceAsync(CancellationToken cancellationToken)
        => await database.InboxEvents
            .AsNoTracking()
            .OrderBy(item => item.Provider)
            .ThenBy(item => item.ProviderEventId)
            .Select(item => new PersistedEvidence(
                item.Provider,
                item.ProviderEventId,
                item.ExternalSettlementId,
                item.BodySha256,
                item.VerifiedAt))
            .Select(item => item.ToDomain())
            .ToArrayAsync(cancellationToken);

    public async ValueTask RecordAsync(
        ProviderReceptionRecord record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var receipt = ProviderEvidenceReceipt.Receive(
            record.ReceiptId,
            record.Provider,
            record.ProviderEventId,
            record.ExternalSettlementId,
            record.BodySha256,
            record.ReceivedAt);

        if (record.VerificationOutcome == ProviderVerificationOutcome.Rejected)
        {
            receipt.MarkRejected(record.VerificationReason);
        }
        else if (record.VerificationOutcome ==
                 ProviderVerificationOutcome.Indeterminate)
        {
            receipt.MarkIndeterminate(record.VerificationReason);
        }
        else
        {
            await AttachAuthenticatedAsync(
                receipt,
                record,
                cancellationToken);
        }

        database.EvidenceReceipts.Add(receipt);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<ProviderInboxStatus?> GetStatusAsync(
        Guid inboxEventId,
        CancellationToken cancellationToken)
    {
        if (inboxEventId == Guid.Empty)
        {
            return null;
        }

        var entity = await database.InboxEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.InboxEventId == inboxEventId,
                cancellationToken);
        return entity is null ? null : ToStatus(entity);
    }

    public async ValueTask<IReadOnlyList<ProviderInboxAttemptStatus>>
        LoadAttemptsAsync(
            Guid inboxEventId,
            CancellationToken cancellationToken)
    {
        if (inboxEventId == Guid.Empty)
        {
            return [];
        }

        return await database.ProcessingAttempts
            .AsNoTracking()
            .Where(item => item.InboxEventId == inboxEventId)
            .OrderBy(item => item.AttemptNumber)
            .Select(item => new ProviderInboxAttemptStatus(
                item.ProcessingAttemptId,
                item.InboxEventId,
                item.AttemptNumber,
                item.LeaseToken,
                item.StartedAt,
                item.CompletedAt,
                item.Outcome,
                item.FailureCode))
            .ToArrayAsync(cancellationToken);
    }

    public async ValueTask<bool> TryMarkReadyAsync(
        Guid inboxEventId,
        long expectedRevision,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        if (inboxEventId == Guid.Empty || expectedRevision <= 0)
        {
            return false;
        }

        var entity = await database.InboxEvents.SingleOrDefaultAsync(
            item => item.InboxEventId == inboxEventId,
            cancellationToken);
        if (entity is null || entity.Revision != expectedRevision)
        {
            return false;
        }

        entity.MarkReady(changedAt);
        return await TrySaveAsync(cancellationToken);
    }

    public async ValueTask<ProviderInboxClaim> TryClaimAsync(
        Guid inboxEventId,
        Guid leaseToken,
        DateTimeOffset claimedAt,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken)
    {
        if (inboxEventId == Guid.Empty || leaseToken == Guid.Empty)
        {
            throw new ArgumentException("El claim exige inbox y lease.");
        }

        var entity = await database.InboxEvents.SingleOrDefaultAsync(
            item => item.InboxEventId == inboxEventId,
            cancellationToken);
        if (entity is null)
        {
            return ProviderInboxClaim.From(ProviderInboxClaimOutcome.NotFound);
        }

        if (!entity.CanBeClaimedAt(claimedAt))
        {
            return ProviderInboxClaim.From(
                ProviderInboxClaimOutcome.NotReady,
                ToStatus(entity));
        }

        entity.BeginAttempt(leaseToken, leaseUntil, claimedAt);
        database.ProcessingAttempts.Add(ProviderProcessingAttempt.Start(
            entity.InboxEventId,
            entity.AttemptCount,
            leaseToken,
            claimedAt));

        if (!await TrySaveAsync(cancellationToken))
        {
            return ProviderInboxClaim.From(ProviderInboxClaimOutcome.Contended);
        }

        return ProviderInboxClaim.From(
            ProviderInboxClaimOutcome.Acquired,
            ToStatus(entity));
    }

    public async ValueTask<bool> TryCompleteAsync(
        ProviderInboxCompletion completion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completion);
        var entity = await database.InboxEvents.SingleOrDefaultAsync(
            item => item.InboxEventId == completion.InboxEventId,
            cancellationToken);
        if (entity is null
            || entity.LeaseToken != completion.LeaseToken
            || entity.ProcessingState != ProviderInboxStates.Processing)
        {
            return false;
        }

        var attempt = await database.ProcessingAttempts.SingleOrDefaultAsync(
            item => item.InboxEventId == completion.InboxEventId
                && item.LeaseToken == completion.LeaseToken
                && item.Outcome == ProviderAttemptOutcomes.Running,
            cancellationToken);
        if (attempt is null)
        {
            return false;
        }

        switch (completion.Outcome)
        {
            case ProviderInboxCompletionOutcome.Accredited:
                entity.MarkCommercialEffectAccredited(
                    completion.LeaseToken,
                    completion.Reference,
                    completion.CompletedAt);
                attempt.Complete(
                    ProviderAttemptOutcomes.Succeeded,
                    completion.CompletedAt,
                    null);
                break;
            case ProviderInboxCompletionOutcome.Rejected:
                entity.MarkRejected(
                    completion.LeaseToken,
                    completion.Reason,
                    completion.CompletedAt);
                attempt.Complete(
                    ProviderAttemptOutcomes.Failed,
                    completion.CompletedAt,
                    completion.Reason);
                break;
            case ProviderInboxCompletionOutcome.Indeterminate:
                entity.MarkIndeterminate(
                    completion.LeaseToken,
                    completion.Reason,
                    completion.RetryAt!.Value,
                    completion.CompletedAt);
                attempt.Complete(
                    ProviderAttemptOutcomes.Indeterminate,
                    completion.CompletedAt,
                    completion.Reason);
                break;
            default:
                throw new InvalidOperationException(
                    "La finalización no tiene transición persistente.");
        }

        return await TrySaveAsync(cancellationToken);
    }

    public async ValueTask<bool> TryRecoverExpiredLeaseAsync(
        Guid inboxEventId,
        DateTimeOffset recoveredAt,
        DateTimeOffset retryAt,
        CancellationToken cancellationToken)
    {
        if (inboxEventId == Guid.Empty || retryAt <= recoveredAt)
        {
            return false;
        }

        var entity = await database.InboxEvents.SingleOrDefaultAsync(
            item => item.InboxEventId == inboxEventId,
            cancellationToken);
        if (entity is null || !entity.HasExpiredLeaseAt(recoveredAt))
        {
            return false;
        }

        var leaseToken = entity.LeaseToken!.Value;
        var attempt = await database.ProcessingAttempts.SingleOrDefaultAsync(
            item => item.InboxEventId == inboxEventId
                && item.LeaseToken == leaseToken
                && item.Outcome == ProviderAttemptOutcomes.Running,
            cancellationToken);
        if (attempt is null)
        {
            return false;
        }

        const string failureCode = "lease_expired_before_confirmation";
        entity.MarkIndeterminate(
            leaseToken,
            failureCode,
            retryAt,
            recoveredAt);
        attempt.Complete(
            ProviderAttemptOutcomes.Indeterminate,
            recoveredAt,
            failureCode);
        return await TrySaveAsync(cancellationToken);
    }

    private async ValueTask AttachAuthenticatedAsync(
        ProviderEvidenceReceipt receipt,
        ProviderReceptionRecord record,
        CancellationToken cancellationToken)
    {
        var evidence = record.VerifiedEvidence
            ?? throw new InvalidOperationException(
                "La recepción autenticada exige evidencia verificada.");
        var inbox = await database.InboxEvents.SingleOrDefaultAsync(
            item => item.Provider == record.Provider
                && item.ProviderEventId == record.ProviderEventId,
            cancellationToken);

        if (inbox is null)
        {
            if (record.State == ProviderReceptionState.Duplicate)
            {
                throw new InvalidOperationException(
                    "Un duplicado exige un evento previo en el inbox.");
            }

            inbox = ProviderInboxEvent.Create(evidence, record.ReceivedAt);
            if (record.State == ProviderReceptionState.Held)
            {
                inbox.Hold("evidence_conflict", record.ReceivedAt);
            }
            else if (record.State != ProviderReceptionState.PendingDurableReview)
            {
                throw new InvalidOperationException(
                    "La recepción autenticada no tiene estado durable válido.");
            }

            database.InboxEvents.Add(inbox);
        }
        else if (record.State == ProviderReceptionState.Held)
        {
            inbox.Hold("evidence_conflict", record.ReceivedAt);
        }

        receipt.MarkAuthenticated(
            inbox.InboxEventId,
            evidence.VerifiedAt,
            record.AttestationReference,
            ToPersistenceState(record.State),
            record.AnalysisSha256);
    }

    private async ValueTask<bool> TrySaveAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            return false;
        }
    }

    private static string ToPersistenceState(ProviderReceptionState state)
        => state switch
        {
            ProviderReceptionState.Held => ProviderReceptionStates.Held,
            ProviderReceptionState.Duplicate => ProviderReceptionStates.Duplicate,
            ProviderReceptionState.PendingDurableReview =>
                ProviderReceptionStates.PendingDurableReview,
            _ => throw new InvalidOperationException(
                "El estado autenticado no tiene representación persistente.")
        };

    private static ProviderInboxStatus ToStatus(ProviderInboxEvent entity)
        => new(
            entity.InboxEventId,
            VerifiedExternalPaymentEvidence.FromVerifiedWebhook(
                entity.Provider,
                entity.ProviderEventId,
                entity.ExternalSettlementId,
                entity.BodySha256,
                entity.VerifiedAt),
            ToProcessingState(entity.ProcessingState),
            ToCommercialState(entity.CommercialEffectState),
            entity.HoldReason,
            entity.AttemptCount,
            entity.NextAttemptAt,
            entity.LeaseToken,
            entity.LeaseUntil,
            entity.CommercialEffectReference,
            entity.LastFailureCode,
            entity.Revision);

    private static ProviderInboxProcessingState ToProcessingState(string state)
        => state switch
        {
            ProviderInboxStates.PendingReview =>
                ProviderInboxProcessingState.PendingReview,
            ProviderInboxStates.Held => ProviderInboxProcessingState.Held,
            ProviderInboxStates.Rejected => ProviderInboxProcessingState.Rejected,
            ProviderInboxStates.Ready => ProviderInboxProcessingState.Ready,
            ProviderInboxStates.Processing => ProviderInboxProcessingState.Processing,
            ProviderInboxStates.Indeterminate =>
                ProviderInboxProcessingState.Indeterminate,
            ProviderInboxStates.Completed => ProviderInboxProcessingState.Completed,
            _ => throw new InvalidOperationException(
                "El inbox contiene un estado de proceso desconocido.")
        };

    private static ProviderInboxCommercialState ToCommercialState(string state)
        => state switch
        {
            ProviderCommercialEffectStates.NotRequested =>
                ProviderInboxCommercialState.NotRequested,
            ProviderCommercialEffectStates.Indeterminate =>
                ProviderInboxCommercialState.Indeterminate,
            ProviderCommercialEffectStates.Accredited =>
                ProviderInboxCommercialState.Accredited,
            _ => throw new InvalidOperationException(
                "El inbox contiene un estado comercial desconocido.")
        };

    private sealed record PersistedEvidence(
        string Provider,
        string ProviderEventId,
        string ExternalSettlementId,
        string BodySha256,
        DateTimeOffset VerifiedAt)
    {
        internal VerifiedExternalPaymentEvidence ToDomain()
            => VerifiedExternalPaymentEvidence.FromVerifiedWebhook(
                Provider,
                ProviderEventId,
                ExternalSettlementId,
                BodySha256,
                VerifiedAt);
    }
}
