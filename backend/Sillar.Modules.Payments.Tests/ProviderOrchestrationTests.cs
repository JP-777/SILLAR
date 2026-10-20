using System.Text;
using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Tests;

/// <summary>Recepción y recuperación con puertos enteramente simulados.</summary>
public sealed class ProviderOrchestrationTests
{
    [Fact]
    public async Task Segunda_recepcion_exacta_es_replay_y_ambas_quedan_registradas()
    {
        var ledger = new MemoryLedger();
        var orchestrator = new ProviderReceptionOrchestrator(
            AuthenticatedVerifier(),
            ledger);
        var request = Request("same-body");

        var first = await orchestrator.ReceiveAsync(request, CancellationToken);
        var replay = await orchestrator.ReceiveAsync(request, CancellationToken);

        Assert.Equal(
            ProviderReceptionState.PendingDurableReview,
            first.Record.State);
        Assert.Equal(ProviderReceptionState.Duplicate, replay.Record.State);
        Assert.Equal(2, ledger.Records.Count);
        Assert.NotEqual(
            ledger.Records[0].ReceiptId,
            ledger.Records[1].ReceiptId);
        Assert.Equal(2, ledger.LoadCalls);
        Assert.NotNull(replay.Analysis);
    }

    [Fact]
    public async Task Mismo_evento_con_otro_body_queda_en_hold()
    {
        var ledger = new MemoryLedger();
        var orchestrator = new ProviderReceptionOrchestrator(
            AuthenticatedVerifier(),
            ledger);

        await orchestrator.ReceiveAsync(Request("body-one"), CancellationToken);
        var conflict = await orchestrator.ReceiveAsync(
            Request("body-two"),
            CancellationToken);

        Assert.Equal(ProviderReceptionState.Held, conflict.Record.State);
        var trace = Assert.Single(conflict.Analysis!.Trace);
        Assert.True(trace.IsHeld);
        Assert.Contains(
            ProviderEvidenceConflictKind.ReceptionHasContradictoryBodies,
            trace.ConflictKinds);
    }

    [Fact]
    public async Task Verificacion_rechazada_registra_hash_sin_crear_evidencia_autenticada()
    {
        var ledger = new MemoryLedger();
        var orchestrator = new ProviderReceptionOrchestrator(
            new FakeVerifier(_ => ProviderVerificationResult.Rejected(
                "invalid_signature")),
            ledger);

        var result = await orchestrator.ReceiveAsync(
            Request("untrusted-body"),
            CancellationToken);

        Assert.Equal(ProviderReceptionState.Rejected, result.Record.State);
        Assert.Equal(
            ProviderVerificationOutcome.Rejected,
            result.Record.VerificationOutcome);
        Assert.Null(result.Record.VerifiedEvidence);
        Assert.Null(result.Analysis);
        Assert.Equal(0, ledger.LoadCalls);
        Assert.Matches("^[0-9a-f]{64}$", result.Record.BodySha256);
        Assert.DoesNotContain(
            result.Record.GetType().GetProperties(),
            property => property.Name.Contains("Body", StringComparison.Ordinal)
                && property.Name != nameof(ProviderReceptionRecord.BodySha256));
    }

    [Fact]
    public async Task Verificacion_indeterminada_no_se_presenta_como_rechazo_ni_autenticacion()
    {
        var ledger = new MemoryLedger();
        var orchestrator = new ProviderReceptionOrchestrator(
            new FakeVerifier(_ => ProviderVerificationResult.Indeterminate(
                "verifier_unavailable")),
            ledger);

        var result = await orchestrator.ReceiveAsync(
            Request("pending-body"),
            CancellationToken);

        Assert.Equal(ProviderReceptionState.Indeterminate, result.Record.State);
        Assert.Equal("verifier_unavailable", result.Record.VerificationReason);
        Assert.Null(result.Record.AttestationReference);
        Assert.Null(result.Record.AnalysisSha256);
    }

    [Fact]
    public async Task Identidades_opacas_llegan_sin_recorte_al_ledger_y_pipeline()
    {
        var ledger = new MemoryLedger();
        var orchestrator = new ProviderReceptionOrchestrator(
            AuthenticatedVerifier(),
            ledger);
        var request = ProviderReceptionRequest.Create(
            " provider ",
            " event ",
            " settlement ",
            Encoding.UTF8.GetBytes("body"),
            PaymentTestData.VerifiedAt);

        var result = await orchestrator.ReceiveAsync(request, CancellationToken);

        Assert.Equal(" provider ", result.Record.Provider);
        Assert.Equal(" event ", result.Record.ProviderEventId);
        Assert.Equal(" settlement ", result.Record.ExternalSettlementId);
        Assert.Equal(" provider ", result.Record.VerifiedEvidence!.Provider);
    }

    [Fact]
    public async Task Recepcion_conserva_una_copia_inmutable_del_body_entregado()
    {
        var body = Encoding.UTF8.GetBytes("original");
        var request = ProviderReceptionRequest.Create(
            "provider-a",
            "event-001",
            "settlement-001",
            body,
            PaymentTestData.VerifiedAt);
        body[0] = (byte)'X';
        var ledger = new MemoryLedger();
        var orchestrator = new ProviderReceptionOrchestrator(
            AuthenticatedVerifier(),
            ledger);

        var result = await orchestrator.ReceiveAsync(request, CancellationToken);

        Assert.Equal(
            "0682c5f2076f099c34cfdd15a9e06384"
                + "9ed437a49677e6fcc5b4198c76575be5",
            result.Record.BodySha256);
    }

    [Fact]
    public async Task Fallo_indeterminado_se_reintenta_sin_declarar_acreditacion()
    {
        var port = new SequencedCommercialPort(
            ProviderCommercialEffectResult.Indeterminate("response_lost"),
            ProviderCommercialEffectResult.Accredited("commercial-ref-001"));
        var service = new ProviderCommercialRecoveryService(port);
        var evidence = PaymentTestData.Evidence();
        var candidate = ProviderCommercialEffectCandidate.From(
            Guid.CreateVersion7(),
            evidence);
        var now = PaymentTestData.VerifiedAt;

        var uncertain = await service.AttemptAsync(
            candidate,
            ProviderCommercialRecoverySnapshot.Initial,
            now,
            TimeSpan.FromMinutes(1),
            CancellationToken);

        Assert.Equal(
            ProviderCommercialRecoveryState.Indeterminate,
            uncertain.State);
        Assert.Null(uncertain.AccreditedReference);
        Assert.Equal(now.AddMinutes(1), uncertain.NextAttemptAt);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.AttemptAsync(
                candidate,
                uncertain,
                now.AddSeconds(30),
                TimeSpan.FromMinutes(2),
                CancellationToken));

        var accredited = await service.AttemptAsync(
            candidate,
            uncertain,
            now.AddMinutes(1),
            TimeSpan.FromMinutes(2),
            CancellationToken);

        Assert.Equal(
            ProviderCommercialRecoveryState.Accredited,
            accredited.State);
        Assert.Equal("commercial-ref-001", accredited.AccreditedReference);
        Assert.Equal(2, accredited.Attempts);
        Assert.Null(accredited.NextAttemptAt);
    }

    [Fact]
    public async Task Rechazo_comercial_es_terminal_y_no_se_reintenta()
    {
        var service = new ProviderCommercialRecoveryService(
            new SequencedCommercialPort(
                ProviderCommercialEffectResult.Rejected("not_payable")));
        var candidate = ProviderCommercialEffectCandidate.From(
            Guid.CreateVersion7(),
            PaymentTestData.Evidence());
        var rejected = await service.AttemptAsync(
            candidate,
            ProviderCommercialRecoverySnapshot.Initial,
            PaymentTestData.VerifiedAt,
            TimeSpan.FromMinutes(1),
            CancellationToken);

        Assert.Equal(ProviderCommercialRecoveryState.Rejected, rejected.State);
        Assert.Null(rejected.AccreditedReference);
        Assert.Equal("not_payable", rejected.LastReason);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.AttemptAsync(
                candidate,
                rejected,
                PaymentTestData.VerifiedAt.AddMinutes(1),
                TimeSpan.FromMinutes(1),
                CancellationToken));
    }

    private static FakeVerifier AuthenticatedVerifier()
        => new(_ => ProviderVerificationResult.Authenticated(
            PaymentTestData.VerifiedAt.AddSeconds(1),
            "simulated-attestation"));

    private static ProviderReceptionRequest Request(string body)
        => ProviderReceptionRequest.Create(
            "provider-a",
            "event-001",
            "settlement-001",
            Encoding.UTF8.GetBytes(body),
            PaymentTestData.VerifiedAt);

    private static CancellationToken CancellationToken
        => TestContext.Current.CancellationToken;

    private sealed class FakeVerifier(
        Func<ProviderReceptionRequest, ProviderVerificationResult> verify)
        : IProviderEvidenceVerifier
    {
        public ValueTask<ProviderVerificationResult> VerifyAsync(
            ProviderReceptionRequest request,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(verify(request));
    }

    private sealed class MemoryLedger : IProviderReceptionLedger
    {
        private readonly List<VerifiedExternalPaymentEvidence> _known = [];

        internal List<ProviderReceptionRecord> Records { get; } = [];
        internal int LoadCalls { get; private set; }

        public ValueTask<IReadOnlyList<VerifiedExternalPaymentEvidence>>
            LoadKnownVerifiedEvidenceAsync(CancellationToken cancellationToken)
        {
            LoadCalls++;
            return ValueTask.FromResult<IReadOnlyList<
                VerifiedExternalPaymentEvidence>>(_known.ToArray());
        }

        public ValueTask RecordAsync(
            ProviderReceptionRecord record,
            CancellationToken cancellationToken)
        {
            Records.Add(record);
            if (record.VerifiedEvidence is not null)
            {
                _known.Add(record.VerifiedEvidence);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class SequencedCommercialPort(
        params ProviderCommercialEffectResult[] results)
        : IProviderCommercialEffectPort
    {
        private readonly Queue<ProviderCommercialEffectResult> _results =
            new(results);

        public ValueTask<ProviderCommercialEffectResult> TryApplyAsync(
            ProviderCommercialEffectCandidate candidate,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(_results.Dequeue());
    }
}
