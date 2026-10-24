using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public sealed class IdempotenciaCuotasLocalesTests
{
    private readonly Guid installation = Guid.NewGuid();
    private LocalAssistantQuotaStore Store(long limit = 20, int parallel = 2) => new([
        new(installation, AssistantMode.Local, true, limit, parallel)
    ]);

    [Fact]
    public async Task ReintentosConcurrentesCreanUnaSolaReserva()
    {
        var store = Store();
        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
            store.TryReserve(installation, 20, "consulta-1"))));
        Assert.Single(results, r => r.Outcome == QuotaOperation.Reserved);
        Assert.Equal(49, results.Count(r => r.Outcome == QuotaOperation.Replay));
        Assert.Single(results.Select(r => r.ReservationId).Distinct());
        Assert.Equal(new LocalQuotaSnapshot(20, 0, 20, 1, AssistantMode.Local), store.Snapshot(installation));
    }

    [Fact]
    public void MismaClaveConOtroCosteNoModificaReserva()
    {
        var store = Store();
        var first = store.TryReserve(installation, 10, "consulta-1");
        var before = store.Snapshot(installation);
        var conflict = store.TryReserve(installation, 11, "consulta-1");
        Assert.Equal(QuotaOperation.Conflict, conflict.Outcome);
        Assert.Equal(first.ReservationId, conflict.ReservationId);
        Assert.Equal(before, store.Snapshot(installation));
    }

    [Fact]
    public void ClaveIgualEnOtraInstalacionTieneReservaIndependiente()
    {
        var other = Guid.NewGuid();
        var store = new LocalAssistantQuotaStore([
            new(installation, AssistantMode.Local, true, 20, 1),
            new(other, AssistantMode.ExternalApi, true, 20, 1)
        ]);
        var first = store.TryReserve(installation, 20, "consulta-1");
        var second = store.TryReserve(other, 20, "consulta-1");
        Assert.Equal(QuotaOperation.Reserved, second.Outcome);
        Assert.NotEqual(first.ReservationId, second.ReservationId);
        Assert.Equal(AssistantMode.ExternalApi, second.Mode);
        Assert.Equal(QuotaOperation.NotFound, store.MarkDispatched(other, first.ReservationId!.Value));
    }

    [Fact]
    public void ReintentoDespachadoNoHabilitaOtraInvocacion()
    {
        var store = Store();
        var first = store.TryReserve(installation, 20, "consulta-1");
        var id = first.ReservationId!.Value;
        Assert.Equal(QuotaOperation.Dispatched, store.MarkDispatched(installation, id));
        var replay = store.TryReserve(installation, 20, "consulta-1");
        Assert.Equal(QuotaOperation.AlreadyDispatched, replay.Outcome);
        Assert.Equal(first.ReservationId, replay.ReservationId);
        Assert.Equal(QuotaOperation.AlreadyDispatched, store.MarkDispatched(installation, id));
        Assert.Equal(20, store.Snapshot(installation)!.ReservedUnits);
    }

    [Fact]
    public void ReintentoCompletadoNoConsumeNiReservaDeNuevo()
    {
        var store = Store();
        var id = store.TryReserve(installation, 20, "consulta-1").ReservationId!.Value;
        store.MarkDispatched(installation, id);
        store.Complete(installation, id, 8);
        var before = store.Snapshot(installation);
        Assert.Equal(QuotaOperation.AlreadyCompleted,
            store.TryReserve(installation, 20, "consulta-1").Outcome);
        Assert.Equal(before, store.Snapshot(installation));
    }

    [Fact]
    public void CancelacionNoResucitaConLaMismaClave()
    {
        var store = Store();
        var id = store.TryReserve(installation, 20, "consulta-1").ReservationId!.Value;
        store.CancelBeforeDispatch(installation, id);
        Assert.Equal(QuotaOperation.Conflict, store.TryReserve(installation, 20, "consulta-1").Outcome);
        Assert.Equal(0, store.Snapshot(installation)!.ReservedUnits);
        Assert.Equal(QuotaOperation.Reserved, store.TryReserve(installation, 20, "consulta-2").Outcome);
    }

    [Fact]
    public void AdmisionRechazadaNoConsumeClave()
    {
        var store = Store();
        var busy = store.TryReserve(installation, 20, "ocupada").ReservationId!.Value;
        Assert.Equal(QuotaOperation.FaqFallback, store.TryReserve(installation, 10, "consulta-1").Outcome);
        store.CancelBeforeDispatch(installation, busy);
        Assert.Equal(QuotaOperation.Reserved, store.TryReserve(installation, 10, "consulta-1").Outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("con espacios")]
    [InlineData("salto\nlinea")]
    [InlineData("ámbito")]
    public void ClaveInvalidaNoCreaTrabajo(string? key)
    {
        var store = Store();
        Assert.Equal(QuotaOperation.Invalid, store.TryReserve(installation, 10, key!).Outcome);
        Assert.Equal(0, store.Snapshot(installation)!.RunningRequests);
    }
}
