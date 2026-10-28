using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;
using Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

namespace Sillar.Modules.Payments.Infrastructure.Persistence;

/// <summary>
/// Almacén técnico de M11. Cada operación guarda evento, recepción e intento en
/// un único <c>SaveChangesAsync</c>; puede participar en una transacción externa.
/// </summary>
internal sealed class EfProviderInboxRepository(PaymentsDbContext database)
    : IProviderReceptionLedger,
      IProviderInboxRepository,
      IProviderSettlementFactRepository,
      IProviderVerifiedEvidenceReferenceSource
{
    public async ValueTask<IReadOnlyList<VerifiedExternalPaymentEvidence>>
        LoadKnownVerifiedEvidenceAsync(CancellationToken cancellationToken)
    {
        var persisted = await QueryAuthenticatedEvidenceHistory(database)
            .OrderBy(item => item.Provider)
            .ThenBy(item => item.ProviderEventId)
            .ThenBy(item => item.ReceivedAt)
            .ThenBy(item => item.ReceiptId)
            .Select(item => new PersistedEvidence(
                item.Provider,
                item.ProviderEventId,
                item.ExternalSettlementId,
                item.BodySha256,
                item.VerifiedAt!.Value))
            .ToArrayAsync(cancellationToken);
        return Array.AsReadOnly(
            persisted.Select(item => item.ToDomain()).ToArray());
    }

    /// <summary>
    /// El historial de replay incluye cada recepción autenticada, también las
    /// que dejaron el inbox en HOLD. Consultar sólo la fila canónica del inbox
    /// ocultaría contradicciones durables en reclasificaciones posteriores.
    /// </summary>
    internal static IQueryable<ProviderEvidenceReceipt>
        QueryAuthenticatedEvidenceHistory(PaymentsDbContext source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.EvidenceReceipts
            .AsNoTracking()
            .Where(item => item.VerificationState ==
                ProviderVerificationStates.Authenticated);
    }

    public async ValueTask<IReadOnlyList<ProviderVerifiedEvidenceReference>>
        LoadAsync(
            Guid inboxEventId,
            CancellationToken cancellationToken)
    {
        if (inboxEventId == Guid.Empty)
        {
            return [];
        }

        var persisted = await QueryVerifiedEvidenceReferences(
                database,
                inboxEventId)
            .OrderBy(item => item.ReceivedAt)
            .ThenBy(item => item.ReceiptId)
            .Select(item => new PersistedVerifiedEvidenceReference(
                item.ReceiptId,
                item.Provider,
                item.ProviderEventId,
                item.ExternalSettlementId,
                item.BodySha256,
                item.VerifiedAt!.Value,
                item.AttestationReference!))
            .ToArrayAsync(cancellationToken);

        return Array.AsReadOnly(
            persisted.Select(item => item.ToContract()).ToArray());
    }

    /// <summary>
    /// Proyección de sólo lectura. Una referencia sólo es elegible cuando la
    /// recepción fue autenticada y quedó vinculada al inbox solicitado.
    /// </summary>
    internal static IQueryable<ProviderEvidenceReceipt>
        QueryVerifiedEvidenceReferences(
            PaymentsDbContext source,
            Guid inboxEventId)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (inboxEventId == Guid.Empty)
        {
            throw new ArgumentException(
                "La proyección exige inbox.",
                nameof(inboxEventId));
        }

        return source.EvidenceReceipts
            .AsNoTracking()
            .Where(item => item.InboxEventId == inboxEventId
                && item.VerificationState ==
                    ProviderVerificationStates.Authenticated
                && item.AttestationReference != null);
    }

    public async ValueTask<ProviderSettlementFacts?> LoadCandidateFactsAsync(
        Guid inboxEventId,
        CancellationToken cancellationToken)
    {
        if (inboxEventId == Guid.Empty)
        {
            return null;
        }

        var observations = await (
                from facts in database.SettlementFactObservations.AsNoTracking()
                join receipt in database.EvidenceReceipts.AsNoTracking()
                    on facts.ReceiptId equals receipt.ReceiptId
                where receipt.InboxEventId == inboxEventId
                select facts)
            .ToArrayAsync(cancellationToken);
        if (observations.Length == 0)
        {
            return null;
        }

        var candidate = observations[0].ToDomain();
        var classification = ProviderSettlementFactClassifier.Classify(
            candidate,
            observations.Select(item => item.ToDomain()));
        if (classification != ProviderSettlementFactDecision.Duplicate)
        {
            throw new InvalidOperationException(
                "Un inbox con hechos monetarios divergentes no puede procesarse.");
        }

        return candidate;
    }

    public async ValueTask<ProviderSettlementFactDecision>
        RecordSettlementFactsAsync(
            Guid receiptId,
            ProviderSettlementFacts facts,
            DateTimeOffset recordedAt,
            CancellationToken cancellationToken)
    {
        const int maxOwnedTransactionAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await RecordSettlementFactsOnceAsync(
                    receiptId,
                    facts,
                    recordedAt,
                    cancellationToken);
            }
            catch (ProviderInboxConcurrencyException) when (
                attempt < maxOwnedTransactionAttempts
                && database.Database.CurrentTransaction is null)
            {
                // La transacción propia ya fue revertida. Releer bajo una nueva
                // valla convierte la carrera en Duplicate/Conflict/HOLD según
                // los hechos durables que ganó la ronda anterior.
            }
        }
    }

    private async ValueTask<ProviderSettlementFactDecision>
        RecordSettlementFactsOnceAsync(
            Guid receiptId,
            ProviderSettlementFacts facts,
            DateTimeOffset recordedAt,
            CancellationToken cancellationToken)
    {
        if (receiptId == Guid.Empty)
        {
            throw new ArgumentException(
                "Los hechos monetarios exigen recepción.",
                nameof(receiptId));
        }

        ArgumentNullException.ThrowIfNull(facts);
        ProviderPersistenceGuard.RequiredInstant(recordedAt, nameof(recordedAt));

        IDbContextTransaction? ownedTransaction = null;
        try
        {
            if (database.Database.CurrentTransaction is null)
            {
                ownedTransaction = await database.Database.BeginTransactionAsync(
                    cancellationToken);
            }

            var fence = await AcquireSettlementFenceAsync(
                facts.Evidence.ExternalSettlementId,
                recordedAt,
                cancellationToken);

            // Toda lectura que decide New/Duplicate/HOLD ocurre después de
            // tomar la valla transaccional. Dos inboxes con el mismo literal
            // de liquidación no pueden clasificar simultáneamente un snapshot
            // anterior. La valla no equivale a identidad comercial M03.
            var receipt = await database.EvidenceReceipts.SingleOrDefaultAsync(
                item => item.ReceiptId == receiptId,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "La recepción de los hechos monetarios no existe.");
            EnsureFactsBelongToAuthenticatedReceipt(receipt, facts);
            if (recordedAt < receipt.ReceivedAt)
            {
                throw new ArgumentException(
                    "Los hechos no pueden preceder a su recepción.",
                    nameof(recordedAt));
            }

            var existing = await database.SettlementFactObservations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.ReceiptId == receiptId,
                    cancellationToken);
            if (existing is not null)
            {
                if (!existing.HasSameFacts(facts))
                {
                    throw new InvalidOperationException(
                        "Una recepción no puede cambiar sus hechos monetarios.");
                }

                if (ownedTransaction is not null)
                {
                    await ownedTransaction.CommitAsync(cancellationToken);
                }

                return ProviderSettlementFactDecision.Duplicate;
            }

            // El clasificador sólo observa la misma identidad de evento o el
            // mismo literal de liquidación. Este predicado es equivalente al
            // recorrido global y evita cargar hechos no relacionados.
            var knownRows = await database.SettlementFactObservations
                .AsNoTracking()
                .Where(item =>
                    (item.Provider == facts.Evidence.Provider
                        && item.ProviderEventId ==
                            facts.Evidence.ProviderEventId)
                    || item.ExternalSettlementId ==
                        facts.Evidence.ExternalSettlementId)
                .ToArrayAsync(cancellationToken);
            var classification = ProviderSettlementFactClassifier.Classify(
                facts,
                knownRows.Select(item => item.ToDomain()));
            var inbox = await database.InboxEvents.SingleAsync(
                item => item.InboxEventId == receipt.InboxEventId,
                cancellationToken);
            var changedAt = recordedAt < inbox.UpdatedAt
                ? inbox.UpdatedAt
                : recordedAt;

            database.SettlementFactObservations.Add(
                ProviderSettlementFactObservation.Create(
                    receiptId,
                    facts,
                    classification,
                    recordedAt));

            if (classification is ProviderSettlementFactDecision.Conflict
                or ProviderSettlementFactDecision.DecisionRequired)
            {
                var reason = classification ==
                    ProviderSettlementFactDecision.Conflict
                    ? "settlement_facts_conflict"
                    : "settlement_facts_decision_required";
                await HoldAffectedInboxesAsync(
                    inbox,
                    knownRows.Select(item => item.ReceiptId).ToArray(),
                    changedAt,
                    reason,
                    cancellationToken);
            }
            else
            {
                inbox.RegisterSettlementFactObservation(changedAt);
            }

            fence.RegisterObservation(
                facts.Evidence.ExternalSettlementId,
                recordedAt);
            await database.SaveChangesAsync(cancellationToken);
            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync(cancellationToken);
            }

            return classification;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            database.ChangeTracker.Clear();
            throw new ProviderInboxConcurrencyException(
                "El alcance de liquidación cambió durante la conservación de hechos.",
                exception);
        }
        catch (DbUpdateException exception) when (
            IsSettlementFactReceiptUniquenessViolation(exception))
        {
            database.ChangeTracker.Clear();
            throw new ProviderInboxConcurrencyException(
                "Otra operación conservó hechos para la misma recepción.",
                exception);
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
        }
    }

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

        if (!Enum.IsDefined(record.VerificationOutcome))
        {
            throw new InvalidOperationException(
                "La recepción contiene un resultado de verificación desconocido.");
        }

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
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            database.ChangeTracker.Clear();
            throw new ProviderInboxConcurrencyException(
                "El inbox cambió durante la conservación de la recepción.",
                exception);
        }
        catch (DbUpdateException exception) when (
            database.Database.CurrentTransaction is null
            && IsProviderEventUniquenessViolation(exception))
        {
            database.ChangeTracker.Clear();
            throw new ProviderInboxConcurrencyException(
                "Otra recepción creó la misma identidad durante el análisis.",
                exception);
        }
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
        if (entity is null
            || entity.Revision != expectedRevision
            || entity.ProcessingState != ProviderInboxStates.PendingReview)
        {
            return false;
        }

        var hasSettlementFacts = await (
                from facts in database.SettlementFactObservations.AsNoTracking()
                join receipt in database.EvidenceReceipts.AsNoTracking()
                    on facts.ReceiptId equals receipt.ReceiptId
                where receipt.InboxEventId == inboxEventId
                select facts.ReceiptId)
            .AnyAsync(cancellationToken);
        if (!hasSettlementFacts)
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
            case ProviderInboxCompletionOutcome.Applied:
                entity.MarkCommercialEffectApplied(
                    completion.LeaseToken,
                    ProviderCommercialEffectStates.Applied,
                    completion.Reference,
                    completion.CompletedAt);
                attempt.Complete(
                    ProviderAttemptOutcomes.Succeeded,
                    completion.CompletedAt,
                    null);
                break;
            case ProviderInboxCompletionOutcome.AlreadyApplied:
                entity.MarkCommercialEffectApplied(
                    completion.LeaseToken,
                    ProviderCommercialEffectStates.AlreadyApplied,
                    completion.Reference,
                    completion.CompletedAt);
                attempt.Complete(
                    ProviderAttemptOutcomes.Succeeded,
                    completion.CompletedAt,
                    null);
                break;
            case ProviderInboxCompletionOutcome.Rejected:
                entity.MarkCommercialHold(
                    completion.LeaseToken,
                    ProviderCommercialEffectStates.Rejected,
                    completion.Reason,
                    completion.CompletedAt);
                attempt.Complete(
                    ProviderAttemptOutcomes.Held,
                    completion.CompletedAt,
                    completion.Reason);
                break;
            case ProviderInboxCompletionOutcome.Conflict:
                entity.MarkCommercialHold(
                    completion.LeaseToken,
                    ProviderCommercialEffectStates.Conflict,
                    completion.Reason,
                    completion.CompletedAt);
                attempt.Complete(
                    ProviderAttemptOutcomes.Held,
                    completion.CompletedAt,
                    completion.Reason);
                break;
            case ProviderInboxCompletionOutcome.DecisionRequired:
                entity.MarkCommercialHold(
                    completion.LeaseToken,
                    ProviderCommercialEffectStates.DecisionRequired,
                    completion.Reason,
                    completion.CompletedAt);
                attempt.Complete(
                    ProviderAttemptOutcomes.Held,
                    completion.CompletedAt,
                    completion.Reason);
                break;
            case ProviderInboxCompletionOutcome.Indeterminate:
                entity.MarkIndeterminate(
                    completion.LeaseToken,
                    completion.Reason,
                    completion.RetryAt,
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
        DateTimeOffset? retryAt,
        CancellationToken cancellationToken)
    {
        if (inboxEventId == Guid.Empty
            || (retryAt is not null && retryAt <= recoveredAt))
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
        else if (record.State == ProviderReceptionState.PendingDurableReview)
        {
            throw new ProviderInboxConcurrencyException(
                "El evento apareció en el inbox después del análisis.",
                new InvalidOperationException(
                    "La clasificación debe repetirse con la evidencia durable."));
        }
        else if (record.State == ProviderReceptionState.Held)
        {
            await HoldForReviewAsync(
                inbox,
                record.ReceivedAt,
                "evidence_conflict",
                cancellationToken);
        }

        receipt.MarkAuthenticated(
            inbox.InboxEventId,
            evidence.VerifiedAt,
            record.AttestationReference,
            ToPersistenceState(record.State),
            record.AnalysisSha256);
    }

    private async ValueTask<ProviderSettlementConcurrencyFence>
        AcquireSettlementFenceAsync(
            string externalSettlementId,
            DateTimeOffset acquiredAt,
            CancellationToken cancellationToken)
    {
        if (database.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "La valla de liquidación exige una transacción activa.");
        }

        var candidate = ProviderSettlementConcurrencyFence.Create(
            externalSettlementId,
            acquiredAt);

        // El upsert sólo materializa una fila técnica. FOR UPDATE mantiene la
        // exclusión hasta el commit de la observación y de todos los HOLD.
        // No se ejecuta SQL sobre sales.* ni se declara unicidad comercial.
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO payments.provider_settlement_concurrency_fences
                (fence_sha256, external_settlement_id, revision,
                 created_at, updated_at)
            VALUES
                ({candidate.FenceSha256}, {candidate.ExternalSettlementId},
                 {candidate.Revision}, {candidate.CreatedAt},
                 {candidate.UpdatedAt})
            ON CONFLICT (fence_sha256) DO NOTHING
            """, cancellationToken);

        var fences = await database.SettlementConcurrencyFences
            .FromSqlInterpolated($"""
                SELECT fence_sha256, external_settlement_id, revision,
                       created_at, updated_at
                FROM payments.provider_settlement_concurrency_fences
                WHERE fence_sha256 = {candidate.FenceSha256}
                FOR UPDATE
                """)
            .AsTracking()
            .ToArrayAsync(cancellationToken);
        var fence = AssertSingleSettlementFence(fences);
        if (!StringComparer.Ordinal.Equals(
                fence.ExternalSettlementId,
                externalSettlementId))
        {
            throw new InvalidOperationException(
                "La clave de coordinación colisionó con otra identidad opaca.");
        }

        return fence;
    }

    private async ValueTask HoldAffectedInboxesAsync(
        ProviderInboxEvent incomingInbox,
        IReadOnlyCollection<Guid> knownReceiptIds,
        DateTimeOffset changedAt,
        string reason,
        CancellationToken cancellationToken)
    {
        await HoldForReviewAsync(
            incomingInbox,
            changedAt,
            reason,
            cancellationToken);
        if (knownReceiptIds.Count == 0)
        {
            return;
        }

        var relatedInboxIds = await database.EvidenceReceipts
            .AsNoTracking()
            .Where(item => knownReceiptIds.Contains(item.ReceiptId)
                && item.InboxEventId != null
                && item.InboxEventId != incomingInbox.InboxEventId)
            .Select(item => item.InboxEventId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (relatedInboxIds.Length == 0)
        {
            return;
        }

        var relatedInboxes = await database.InboxEvents
            .Where(item => relatedInboxIds.Contains(item.InboxEventId))
            .ToArrayAsync(cancellationToken);
        foreach (var relatedInbox in relatedInboxes)
        {
            // Un efecto ya confirmado no se reescribe retroactivamente. La
            // observación entrante queda en HOLD y la resolución corresponde
            // a la futura autoridad comercial M03.
            if (relatedInbox.ProcessingState == ProviderInboxStates.Completed)
            {
                continue;
            }

            await HoldForReviewAsync(
                relatedInbox,
                changedAt,
                reason,
                cancellationToken);
        }
    }

    private static ProviderSettlementConcurrencyFence AssertSingleSettlementFence(
        IReadOnlyList<ProviderSettlementConcurrencyFence> fences)
        => fences.Count == 1
            ? fences[0]
            : throw new InvalidOperationException(
                "La valla de liquidación no pudo materializarse de forma única.");

    private async ValueTask HoldForReviewAsync(
        ProviderInboxEvent inbox,
        DateTimeOffset receivedAt,
        string reason,
        CancellationToken cancellationToken)
    {
        var changedAt = receivedAt < inbox.UpdatedAt
            ? inbox.UpdatedAt
            : receivedAt;
        if (inbox.ProcessingState == ProviderInboxStates.Processing)
        {
            var activeLease = inbox.LeaseToken!.Value;
            var attempt = await database.ProcessingAttempts.SingleOrDefaultAsync(
                item => item.InboxEventId == inbox.InboxEventId
                    && item.LeaseToken == activeLease
                    && item.Outcome == ProviderAttemptOutcomes.Running,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "Un inbox en proceso carece de intento activo.");
            attempt.Complete(
                ProviderAttemptOutcomes.Held,
                changedAt,
                $"{reason}_during_processing");
            inbox.HoldActiveAttempt(
                activeLease,
                reason,
                changedAt);
            return;
        }

        inbox.Hold(reason, changedAt);
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
        catch (DbUpdateException exception) when (
            database.Database.CurrentTransaction is null
            && IsAttemptConcurrencyViolation(exception))
        {
            database.ChangeTracker.Clear();
            return false;
        }
    }

    private static bool IsProviderEventUniquenessViolation(
        DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_payment_inbox_provider_event"
        };

    private static bool IsAttemptConcurrencyViolation(
        DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_payment_attempt_inbox_number"
                or "uq_payment_attempt_running_inbox"
        };

    private static bool IsSettlementFactReceiptUniquenessViolation(
        DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "pk_provider_settlement_fact_observations"
        };

    private static void EnsureFactsBelongToAuthenticatedReceipt(
        ProviderEvidenceReceipt receipt,
        ProviderSettlementFacts facts)
    {
        if (receipt.VerificationState != ProviderVerificationStates.Authenticated
            || receipt.InboxEventId is null)
        {
            throw new InvalidOperationException(
                "Sólo una recepción autenticada puede aportar hechos monetarios.");
        }

        if (!StringComparer.Ordinal.Equals(
                receipt.Provider,
                facts.Evidence.Provider)
            || !StringComparer.Ordinal.Equals(
                receipt.ProviderEventId,
                facts.Evidence.ProviderEventId)
            || !StringComparer.Ordinal.Equals(
                receipt.ExternalSettlementId,
                facts.Evidence.ExternalSettlementId)
            || !StringComparer.Ordinal.Equals(
                receipt.BodySha256,
                facts.Evidence.BodySha256)
            || receipt.VerifiedAt != facts.Evidence.VerifiedAt)
        {
            throw new InvalidOperationException(
                "Los hechos monetarios no corresponden a la recepción autenticada.");
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
            ProviderCommercialEffectStates.Rejected =>
                ProviderInboxCommercialState.Rejected,
            ProviderCommercialEffectStates.Conflict =>
                ProviderInboxCommercialState.Conflict,
            ProviderCommercialEffectStates.DecisionRequired =>
                ProviderInboxCommercialState.DecisionRequired,
            ProviderCommercialEffectStates.Applied =>
                ProviderInboxCommercialState.Applied,
            ProviderCommercialEffectStates.AlreadyApplied =>
                ProviderInboxCommercialState.AlreadyApplied,
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

    private sealed record PersistedVerifiedEvidenceReference(
        Guid ReceiptId,
        string Provider,
        string ProviderEventId,
        string ExternalSettlementId,
        string BodySha256,
        DateTimeOffset VerifiedAt,
        string AttestationReference)
    {
        internal ProviderVerifiedEvidenceReference ToContract()
        {
            var evidence = VerifiedExternalPaymentEvidence.FromVerifiedWebhook(
                Provider,
                ProviderEventId,
                ExternalSettlementId,
                BodySha256,
                VerifiedAt);
            return ProviderVerifiedEvidenceReference.Create(
                ReceiptId,
                evidence,
                AttestationReference);
        }
    }
}
