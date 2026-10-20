using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;

namespace Sillar.Modules.Payments.Tests;

public sealed class ProviderInboxProcessorTests
{
    [Fact]
    public async Task Dos_procesadores_solo_obtienen_un_lease_y_un_intento_comercial()
    {
        var repository = new MemoryInboxRepository(ReadyStatus());
        var port = new SequencedPort(
            ProviderCommercialEffectResult.Accredited("simulated-reference"));
        var processor = new ProviderInboxProcessor(repository, port);
        var now = PaymentTestData.VerifiedAt.AddMinutes(1);

        var attempts = await Task.WhenAll(
            processor.ProcessAsync(
                repository.Status.InboxEventId,
                now,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(2),
                CancellationToken).AsTask(),
            processor.ProcessAsync(
                repository.Status.InboxEventId,
                now,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(2),
                CancellationToken).AsTask());

        Assert.Single(
            attempts,
            item => item.Outcome == ProviderInboxProcessOutcome.Accredited);
        Assert.Single(
            attempts,
            item => item.Outcome == ProviderInboxProcessOutcome.NotReady);
        Assert.Equal(1, port.Calls);
        Assert.Equal(
            ProviderInboxProcessingState.Completed,
            repository.Status.ProcessingState);
        Assert.Equal(1, repository.Status.AttemptCount);
    }

    [Fact]
    public async Task Resultado_indeterminado_solo_reintenta_al_vencer_la_demora()
    {
        var repository = new MemoryInboxRepository(ReadyStatus());
        var port = new SequencedPort(
            ProviderCommercialEffectResult.Indeterminate("timeout"),
            ProviderCommercialEffectResult.Accredited("simulated-reference"));
        var processor = new ProviderInboxProcessor(repository, port);
        var now = PaymentTestData.VerifiedAt.AddMinutes(1);

        var first = await processor.ProcessAsync(
            repository.Status.InboxEventId,
            now,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            CancellationToken);
        var early = await processor.ProcessAsync(
            repository.Status.InboxEventId,
            now.AddMinutes(1),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            CancellationToken);
        var retry = await processor.ProcessAsync(
            repository.Status.InboxEventId,
            now.AddMinutes(2),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            CancellationToken);

        Assert.Equal(ProviderInboxProcessOutcome.Indeterminate, first.Outcome);
        Assert.Equal(ProviderInboxProcessOutcome.NotReady, early.Outcome);
        Assert.Equal(ProviderInboxProcessOutcome.Accredited, retry.Outcome);
        Assert.Equal(2, port.Calls);
        Assert.Equal(2, repository.Status.AttemptCount);
    }

    [Fact]
    public async Task Rechazo_comercial_es_terminal_y_distinto_de_incertidumbre()
    {
        var repository = new MemoryInboxRepository(ReadyStatus());
        var port = new SequencedPort(
            ProviderCommercialEffectResult.Rejected("candidate_invalid"));
        var processor = new ProviderInboxProcessor(repository, port);
        var now = PaymentTestData.VerifiedAt.AddMinutes(1);

        var rejected = await processor.ProcessAsync(
            repository.Status.InboxEventId,
            now,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            CancellationToken);
        var replay = await processor.ProcessAsync(
            repository.Status.InboxEventId,
            now.AddMinutes(3),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            CancellationToken);

        Assert.Equal(ProviderInboxProcessOutcome.Rejected, rejected.Outcome);
        Assert.Equal(ProviderInboxProcessOutcome.NotReady, replay.Outcome);
        Assert.Equal(
            ProviderInboxCommercialState.Rejected,
            repository.Status.CommercialState);
        Assert.Equal("candidate_invalid", repository.Status.LastFailureCode);
        Assert.Null(repository.Status.NextAttemptAt);
        Assert.Equal(1, port.Calls);
    }

    [Fact]
    public async Task Confirmacion_no_persistida_nunca_se_reporta_como_acreditada()
    {
        var repository = new MemoryInboxRepository(ReadyStatus())
        {
            RejectCompletion = true
        };
        var processor = new ProviderInboxProcessor(
            repository,
            new SequencedPort(
                ProviderCommercialEffectResult.Accredited("unpersisted")));

        var result = await processor.ProcessAsync(
            repository.Status.InboxEventId,
            PaymentTestData.VerifiedAt.AddMinutes(1),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            CancellationToken);

        Assert.Equal(
            ProviderInboxProcessOutcome.ConfirmationNotPersisted,
            result.Outcome);
        Assert.NotEqual(
            ProviderInboxCommercialState.Accredited,
            repository.Status.CommercialState);
    }

    [Fact]
    public async Task Caida_deja_lease_recuperable_pero_no_acredita_efecto()
    {
        var repository = new MemoryInboxRepository(ReadyStatus());
        var processor = new ProviderInboxProcessor(
            repository,
            new ThrowingPort());
        var now = PaymentTestData.VerifiedAt.AddMinutes(1);

        await Assert.ThrowsAsync<SimulatedCrashException>(async () =>
            await processor.ProcessAsync(
                repository.Status.InboxEventId,
                now,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(2),
                CancellationToken));

        Assert.Equal(
            ProviderInboxProcessingState.Processing,
            repository.Status.ProcessingState);
        Assert.False(await processor.TryRecoverExpiredLeaseAsync(
            repository.Status.InboxEventId,
            now.AddSeconds(30),
            TimeSpan.FromMinutes(2),
            CancellationToken));
        Assert.True(await processor.TryRecoverExpiredLeaseAsync(
            repository.Status.InboxEventId,
            now.AddMinutes(1),
            TimeSpan.FromMinutes(2),
            CancellationToken));
        Assert.Equal(
            ProviderInboxProcessingState.Indeterminate,
            repository.Status.ProcessingState);
        Assert.Equal(
            ProviderInboxCommercialState.Indeterminate,
            repository.Status.CommercialState);
        Assert.Null(repository.Status.CommercialEffectReference);
    }

    [Fact]
    public async Task Lease_vencido_no_acepta_confirmacion_tardia_y_exige_nuevo_intento()
    {
        var repository = new MemoryInboxRepository(ReadyStatus());
        var crashing = new ProviderInboxProcessor(repository, new ThrowingPort());
        var now = PaymentTestData.VerifiedAt.AddMinutes(1);

        await Assert.ThrowsAsync<SimulatedCrashException>(async () =>
            await crashing.ProcessAsync(
                repository.Status.InboxEventId,
                now,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(2),
                CancellationToken));
        var expiredLease = repository.Status.LeaseToken!.Value;
        Assert.True(await crashing.TryRecoverExpiredLeaseAsync(
            repository.Status.InboxEventId,
            now.AddMinutes(1),
            TimeSpan.FromMinutes(2),
            CancellationToken));

        var lateConfirmation = ProviderInboxCompletion.Accredited(
            repository.Status.InboxEventId,
            expiredLease,
            now.AddMinutes(1).AddSeconds(1),
            "late-confirmation");
        Assert.False(await repository.TryCompleteAsync(
            lateConfirmation,
            CancellationToken));
        Assert.NotEqual(
            ProviderInboxCommercialState.Accredited,
            repository.Status.CommercialState);

        var retryPort = new SequencedPort(
            ProviderCommercialEffectResult.Accredited("new-confirmation"));
        var retrying = new ProviderInboxProcessor(repository, retryPort);
        var retry = await retrying.ProcessAsync(
            repository.Status.InboxEventId,
            now.AddMinutes(3),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            CancellationToken);

        Assert.Equal(ProviderInboxProcessOutcome.Accredited, retry.Outcome);
        Assert.NotEqual(expiredLease, retry.LeaseToken);
        Assert.Equal("new-confirmation", repository.Status.CommercialEffectReference);
        Assert.Equal(2, repository.Status.AttemptCount);
    }

    [Fact]
    public async Task Revision_optimista_solo_libera_una_vez_la_revision_observada()
    {
        var pending = ReadyStatus() with
        {
            ProcessingState = ProviderInboxProcessingState.PendingReview
        };
        var repository = new MemoryInboxRepository(pending);
        var processor = new ProviderInboxProcessor(
            repository,
            new SequencedPort());
        var now = PaymentTestData.VerifiedAt.AddMinutes(1);

        var first = await processor.TryReleaseAfterReviewAsync(
            pending.InboxEventId,
            pending.Revision,
            now,
            CancellationToken);
        var stale = await processor.TryReleaseAfterReviewAsync(
            pending.InboxEventId,
            pending.Revision,
            now,
            CancellationToken);

        Assert.True(first);
        Assert.False(stale);
        Assert.Equal(ProviderInboxProcessingState.Ready, repository.Status.ProcessingState);
        Assert.Equal(pending.Revision + 1, repository.Status.Revision);
    }

    [Fact]
    public async Task Hold_no_puede_liberarse_ni_invocar_el_puerto_comercial()
    {
        var held = ReadyStatus() with
        {
            ProcessingState = ProviderInboxProcessingState.Held,
            HoldReason = "cross_provider_identity_unresolved"
        };
        var repository = new MemoryInboxRepository(held);
        var port = new SequencedPort(
            ProviderCommercialEffectResult.Accredited("must-not-run"));
        var processor = new ProviderInboxProcessor(repository, port);

        var released = await processor.TryReleaseAfterReviewAsync(
            held.InboxEventId,
            held.Revision,
            PaymentTestData.VerifiedAt.AddMinutes(1),
            CancellationToken);
        var result = await processor.ProcessAsync(
            held.InboxEventId,
            PaymentTestData.VerifiedAt.AddMinutes(1),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            CancellationToken);

        Assert.False(released);
        Assert.Equal(ProviderInboxProcessOutcome.NotReady, result.Outcome);
        Assert.Equal(0, port.Calls);
    }

    private static ProviderInboxStatus ReadyStatus()
        => new(
            Guid.CreateVersion7(),
            PaymentTestData.Evidence(),
            ProviderInboxProcessingState.Ready,
            ProviderInboxCommercialState.NotRequested,
            null,
            0,
            null,
            null,
            null,
            null,
            null,
            1);

    private static CancellationToken CancellationToken
        => TestContext.Current.CancellationToken;

    private sealed class MemoryInboxRepository(ProviderInboxStatus status)
        : IProviderInboxRepository
    {
        private readonly object _gate = new();
        private readonly List<ProviderInboxAttemptStatus> _attempts = [];

        internal ProviderInboxStatus Status { get; private set; } = status;
        internal bool RejectCompletion { get; init; }

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
                    ProviderInboxCompletionOutcome.Accredited => Status with
                    {
                        ProcessingState = ProviderInboxProcessingState.Completed,
                        CommercialState = ProviderInboxCommercialState.Accredited,
                        CommercialEffectReference = completion.Reference,
                        LastFailureCode = null,
                        LeaseToken = null,
                        LeaseUntil = null,
                        Revision = Status.Revision + 1
                    },
                    ProviderInboxCompletionOutcome.Rejected => Status with
                    {
                        ProcessingState = ProviderInboxProcessingState.Rejected,
                        CommercialState = ProviderInboxCommercialState.Rejected,
                        LastFailureCode = completion.Reason,
                        LeaseToken = null,
                        LeaseUntil = null,
                        Revision = Status.Revision + 1
                    },
                    ProviderInboxCompletionOutcome.Indeterminate => Status with
                    {
                        ProcessingState =
                            ProviderInboxProcessingState.Indeterminate,
                        CommercialState =
                            ProviderInboxCommercialState.Indeterminate,
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
            DateTimeOffset retryAt,
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
                    ProcessingState = ProviderInboxProcessingState.Indeterminate,
                    CommercialState = ProviderInboxCommercialState.Indeterminate,
                    LastFailureCode = "lease_expired_before_confirmation",
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
                Outcome = completion.Outcome ==
                    ProviderInboxCompletionOutcome.Accredited
                        ? "succeeded"
                        : completion.Outcome ==
                            ProviderInboxCompletionOutcome.Rejected
                            ? "failed"
                            : "indeterminate",
                FailureCode = completion.Reason
            };
        }
    }

    private sealed class SequencedPort(
        params ProviderCommercialEffectResult[] results)
        : IProviderCommercialEffectPort
    {
        private readonly Queue<ProviderCommercialEffectResult> _results =
            new(results);
        private int _calls;

        internal int Calls => _calls;

        public ValueTask<ProviderCommercialEffectResult> TryApplyAsync(
            ProviderCommercialEffectCandidate candidate,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            lock (_results)
            {
                return ValueTask.FromResult(_results.Dequeue());
            }
        }
    }

    private sealed class ThrowingPort : IProviderCommercialEffectPort
    {
        public ValueTask<ProviderCommercialEffectResult> TryApplyAsync(
            ProviderCommercialEffectCandidate candidate,
            CancellationToken cancellationToken)
            => ValueTask.FromException<ProviderCommercialEffectResult>(
                new SimulatedCrashException());
    }

    private sealed class SimulatedCrashException : Exception;
}
