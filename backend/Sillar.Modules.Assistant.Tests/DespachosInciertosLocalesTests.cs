using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public sealed class DespachosInciertosLocalesTests
{
    private readonly Guid installation = Guid.NewGuid();
    private LocalAssistantQuotaStore Store() => new([
        new(installation, AssistantMode.Local, true, 20, 1)
    ]);

    [Fact]
    public void TimeoutRetienePresupuestoYConcurrenciaSinReenvioNiLiquidacion()
    {
        var store = Store();
        var id = store.TryReserve(installation, 20, "consulta-1").ReservationId!.Value;
        store.MarkDispatched(installation, id);
        var before = store.Snapshot(installation);
        Assert.Equal(QuotaOperation.Uncertain, store.MarkUncertain(installation, id));
        Assert.Equal(QuotaOperation.Uncertain, store.MarkDispatched(installation, id));
        Assert.Equal(QuotaOperation.Conflict, store.CancelBeforeDispatch(installation, id));
        Assert.Equal(QuotaOperation.Conflict, store.Complete(installation, id, 0));
        Assert.Equal(QuotaOperation.Conflict, store.Complete(installation, id, 20));
        Assert.Equal(QuotaOperation.FaqFallback, store.TryReserve(installation, 1, "consulta-2").Outcome);
        var replay = store.TryReserve(installation, 20, "consulta-1");
        Assert.Equal(QuotaOperation.Uncertain, replay.Outcome);
        Assert.Equal(id, replay.ReservationId);
        Assert.Equal(AssistantMode.Local, replay.Mode);
        Assert.Equal(before, store.Snapshot(installation));
    }

    [Fact]
    public async Task NotificacionesConcurrentesDeTimeoutNoDuplicanNiLiberanReserva()
    {
        var store = Store();
        var id = store.TryReserve(installation, 20, "consulta-1").ReservationId!.Value;
        store.MarkDispatched(installation, id);
        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Task.Run(() =>
            store.MarkUncertain(installation, id))));
        Assert.All(results, result => Assert.Equal(QuotaOperation.Uncertain, result));
        Assert.Equal(new LocalQuotaSnapshot(20, 0, 20, 1, AssistantMode.Local), store.Snapshot(installation));
    }

    [Fact]
    public void ReservaSinDespachoNoSeMarcaInciertaYPuedeCancelarse()
    {
        var store = Store();
        var id = store.TryReserve(installation, 20, "consulta-1").ReservationId!.Value;
        Assert.Equal(QuotaOperation.Conflict, store.MarkUncertain(installation, id));
        Assert.Equal(QuotaOperation.Cancelled, store.CancelBeforeDispatch(installation, id));
        Assert.Equal(QuotaOperation.Conflict, store.MarkUncertain(installation, id));
        Assert.Equal(0, store.Snapshot(installation)!.ReservedUnits);
    }

    [Fact]
    public void TimeoutTardioNoRevierteConsumoYaConfirmado()
    {
        var store = Store();
        var id = store.TryReserve(installation, 20, "consulta-1").ReservationId!.Value;
        store.MarkDispatched(installation, id);
        store.Complete(installation, id, 7);
        Assert.Equal(QuotaOperation.Conflict, store.MarkUncertain(installation, id));
        Assert.Equal(QuotaOperation.AlreadyCompleted, store.TryReserve(installation, 20, "consulta-1").Outcome);
        Assert.Equal(new LocalQuotaSnapshot(20, 7, 0, 0, AssistantMode.Local), store.Snapshot(installation));
    }

    [Fact]
    public void OtraInstalacionNoPuedeMarcarDespachoIncierto()
    {
        var store = Store();
        var id = store.TryReserve(installation, 20, "consulta-1").ReservationId!.Value;
        store.MarkDispatched(installation, id);
        Assert.Equal(QuotaOperation.NotFound, store.MarkUncertain(Guid.NewGuid(), id));
        Assert.Equal(QuotaOperation.NotFound, store.MarkUncertain(installation, Guid.Empty));
        Assert.Equal(QuotaOperation.Completed, store.Complete(installation, id, 7));
    }
}
