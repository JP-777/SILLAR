using System.Collections.Concurrent;
using Sillar.Modules.Payments.Domain;
using Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

namespace Sillar.Modules.Payments.Tests;

/// <summary>
/// Pruebas lógicas de la sección crítica. No sustituyen el ensayo pendiente de
/// SELECT FOR UPDATE sobre PostgreSQL real.
/// </summary>
public sealed class ProviderSettlementConcurrencyTests
{
    [Fact]
    public async Task Dos_inboxes_misma_liquidacion_no_obtienen_doble_new()
    {
        var ledger = new LogicalSerializedLedger();
        var first = PaymentTestData.Facts(PaymentTestData.Evidence(
            eventId: "event-a",
            settlementId: "settlement-shared"));
        var second = PaymentTestData.Facts(PaymentTestData.Evidence(
            eventId: "event-b",
            settlementId: "settlement-shared"));

        var results = await RecordConcurrentlyAsync(ledger, first, second);

        Assert.Single(results, result =>
            result == ProviderSettlementFactDecision.New);
        Assert.Single(results, result =>
            result == ProviderSettlementFactDecision.DecisionRequired);
        Assert.Equal(2, ledger.ObservationCount);
    }

    [Fact]
    public async Task Replay_simultaneo_del_mismo_evento_es_idempotente()
    {
        var ledger = new LogicalSerializedLedger();
        var original = PaymentTestData.Facts();
        var replay = PaymentTestData.Facts(PaymentTestData.Evidence(
            verifiedAt: PaymentTestData.VerifiedAt.AddSeconds(1)));

        var results = await RecordConcurrentlyAsync(ledger, original, replay);

        Assert.Single(results, result =>
            result == ProviderSettlementFactDecision.New);
        Assert.Single(results, result =>
            result == ProviderSettlementFactDecision.Duplicate);
    }

    [Fact]
    public async Task Hechos_divergentes_simultaneos_terminan_en_conflicto()
    {
        var ledger = new LogicalSerializedLedger();
        var original = PaymentTestData.Facts(amount: 125m);
        var divergent = PaymentTestData.Facts(amount: 126m);

        var results = await RecordConcurrentlyAsync(
            ledger,
            original,
            divergent);

        Assert.Single(results, result =>
            result == ProviderSettlementFactDecision.New);
        Assert.Single(results, result =>
            result == ProviderSettlementFactDecision.Conflict);
    }

    [Fact]
    public async Task Colision_interproveedor_permanece_decision_required()
    {
        var ledger = new LogicalSerializedLedger();
        var first = PaymentTestData.Facts(PaymentTestData.Evidence(
            provider: "provider-a",
            eventId: "event-a",
            settlementId: "provider-local-id"));
        var second = PaymentTestData.Facts(PaymentTestData.Evidence(
            provider: "provider-b",
            eventId: "event-b",
            settlementId: "provider-local-id"));

        var results = await RecordConcurrentlyAsync(ledger, first, second);

        Assert.Single(results, result =>
            result == ProviderSettlementFactDecision.New);
        Assert.Single(results, result =>
            result == ProviderSettlementFactDecision.DecisionRequired);
    }

    private static async Task<ProviderSettlementFactDecision[]>
        RecordConcurrentlyAsync(
            LogicalSerializedLedger ledger,
            ProviderSettlementFacts first,
            ProviderSettlementFacts second)
    {
        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstTask = Task.Run(async () =>
        {
            await start.Task;
            return await ledger.RecordAsync(first);
        });
        var secondTask = Task.Run(async () =>
        {
            await start.Task;
            return await ledger.RecordAsync(second);
        });
        start.SetResult();
        return await Task.WhenAll(firstTask, secondTask);
    }

    private sealed class LogicalSerializedLedger
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fences =
            new(StringComparer.Ordinal);
        private readonly List<ProviderSettlementFacts> _observations = [];

        internal int ObservationCount
        {
            get
            {
                lock (_observations)
                {
                    return _observations.Count;
                }
            }
        }

        internal async Task<ProviderSettlementFactDecision> RecordAsync(
            ProviderSettlementFacts facts)
        {
            var key = ProviderSettlementConcurrencyFence.CreateFenceSha256(
                facts.Evidence.ExternalSettlementId);
            var fence = _fences.GetOrAdd(
                key,
                _ => new SemaphoreSlim(1, 1));
            await fence.WaitAsync(TestContext.Current.CancellationToken);
            try
            {
                ProviderSettlementFactDecision decision;
                lock (_observations)
                {
                    decision = ProviderSettlementFactClassifier.Classify(
                        facts,
                        _observations);
                    _observations.Add(facts);
                }

                return decision;
            }
            finally
            {
                fence.Release();
            }
        }
    }
}
