using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public class ReservasDeCuotasLocalesTests
{
    private readonly Guid _instalacion = Guid.NewGuid();
    private readonly Guid _otra = Guid.NewGuid();

    private LocalAssistantQuotaStore Store(AssistantMode mode = AssistantMode.Local,
        long limit = 100, int parallel = 2, bool engine = true) =>
        new([new LocalQuotaConfiguration(_instalacion, mode, engine, limit, parallel)]);

    [Fact]
    public void FaqNoReservaRecursosNiActivaIA()
    {
        var store = Store(AssistantMode.Faq);
        var r = store.TryReserve(_instalacion, 50);
        Assert.Equal(QuotaOperation.FaqOnly, r.Outcome);
        Assert.Null(r.ReservationId);
        Assert.Equal(0, store.Snapshot(_instalacion)!.RunningRequests);
    }

    [Fact]
    public void ModoLocalHaceReservaAtomica()
    {
        var store = Store();
        var r = store.TryReserve(_instalacion, 40);
        Assert.Equal(QuotaOperation.Reserved, r.Outcome);
        Assert.Equal(AssistantMode.Local, r.Mode);
        Assert.NotNull(r.ReservationId);
        Assert.Equal(40, store.Snapshot(_instalacion)!.ReservedUnits);
    }

    [Fact]
    public void ModoApiNoCambiaAutomaticamenteALocal()
    {
        var store = Store(AssistantMode.ExternalApi);
        var r = store.TryReserve(_instalacion, 40);
        Assert.Equal(QuotaOperation.Reserved, r.Outcome);
        Assert.Equal(AssistantMode.ExternalApi, r.Mode);
    }

    [Fact]
    public void MotorNoConfiguradoSoloPermiteFaq()
    {
        var store = Store(engine: false);
        Assert.Equal(QuotaOperation.FaqFallback, store.TryReserve(_instalacion, 5).Outcome);
        Assert.Equal(0, store.Snapshot(_instalacion)!.ReservedUnits);
    }

    [Fact]
    public void NoSuperaCuotaNiCambiaModo()
    {
        var store = Store(limit: 10);
        Assert.Equal(QuotaOperation.Reserved, store.TryReserve(_instalacion, 7).Outcome);
        Assert.Equal(QuotaOperation.FaqFallback, store.TryReserve(_instalacion, 4).Outcome);
        Assert.Equal(7, store.Snapshot(_instalacion)!.ReservedUnits);
    }

    [Fact]
    public void CompletarEsIdempotenteConElMismoConsumo()
    {
        var store = Store();
        var id = store.TryReserve(_instalacion, 40).ReservationId!.Value;
        Assert.Equal(QuotaOperation.Dispatched, store.MarkDispatched(_instalacion, id));
        Assert.Equal(QuotaOperation.Completed, store.Complete(_instalacion, id, 12));
        Assert.Equal(QuotaOperation.AlreadyCompleted, store.Complete(_instalacion, id, 12));
        var state = store.Snapshot(_instalacion)!;
        Assert.Equal(12, state.ConsumedUnits);
        Assert.Equal(0, state.ReservedUnits);
        Assert.Equal(0, state.RunningRequests);
    }

    [Fact]
    public void ReplayConConsumoDistintoNoCobraDeNuevo()
    {
        var store = Store();
        var id = store.TryReserve(_instalacion, 20).ReservationId!.Value;
        store.MarkDispatched(_instalacion, id);
        store.Complete(_instalacion, id, 10);
        Assert.Equal(QuotaOperation.Conflict, store.Complete(_instalacion, id, 11));
        Assert.Equal(10, store.Snapshot(_instalacion)!.ConsumedUnits);
    }

    [Fact]
    public void NoCompletaSinMarcarInvocacion()
    {
        var store = Store();
        var id = store.TryReserve(_instalacion, 20).ReservationId!.Value;
        Assert.Equal(QuotaOperation.Conflict, store.Complete(_instalacion, id, 10));
        Assert.Equal(20, store.Snapshot(_instalacion)!.ReservedUnits);
    }

    [Fact]
    public void CancelacionPreviaLiberaCuota()
    {
        var store = Store(limit: 20);
        var id = store.TryReserve(_instalacion, 20).ReservationId!.Value;
        Assert.Equal(QuotaOperation.Cancelled, store.CancelBeforeDispatch(_instalacion, id));
        Assert.Equal(QuotaOperation.AlreadyCancelled, store.CancelBeforeDispatch(_instalacion, id));
        Assert.Equal(QuotaOperation.Reserved, store.TryReserve(_instalacion, 20).Outcome);
    }

    [Fact]
    public void NoLiberaReservaDespuesDeEnviar()
    {
        var store = Store(limit: 20);
        var id = store.TryReserve(_instalacion, 20).ReservationId!.Value;
        store.MarkDispatched(_instalacion, id);
        Assert.Equal(QuotaOperation.Conflict, store.CancelBeforeDispatch(_instalacion, id));
        Assert.Equal(QuotaOperation.FaqFallback, store.TryReserve(_instalacion, 1).Outcome);
        Assert.Equal(20, store.Snapshot(_instalacion)!.ReservedUnits);
    }

    [Fact]
    public void DuplicarDespachoNoAutorizaOtraInvocacion()
    {
        var store = Store();
        var id = store.TryReserve(_instalacion, 10).ReservationId!.Value;
        Assert.Equal(QuotaOperation.Dispatched, store.MarkDispatched(_instalacion, id));
        Assert.Equal(QuotaOperation.AlreadyDispatched, store.MarkDispatched(_instalacion, id));
    }

    [Fact]
    public void BloqueaConsumoMayorAlReservado()
    {
        var store = Store();
        var id = store.TryReserve(_instalacion, 10).ReservationId!.Value;
        store.MarkDispatched(_instalacion, id);
        Assert.Equal(QuotaOperation.Conflict, store.Complete(_instalacion, id, 11));
        Assert.Equal(0, store.Snapshot(_instalacion)!.ConsumedUnits);
    }

    [Fact]
    public async Task AltaConcurrenciaNoSuperaLimites()
    {
        var store = Store(limit: 50, parallel: 3);
        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
            store.TryReserve(_instalacion, 10))));
        Assert.Equal(3, results.Count(x => x.Outcome == QuotaOperation.Reserved));
        Assert.Equal(30, store.Snapshot(_instalacion)!.ReservedUnits);
        Assert.Equal(3, store.Snapshot(_instalacion)!.RunningRequests);
    }

    [Fact]
    public void ImpideOperacionEntreInstalaciones()
    {
        var store = Store();
        var id = store.TryReserve(_instalacion, 10).ReservationId!.Value;
        Assert.Equal(QuotaOperation.NotFound, store.MarkDispatched(_otra, id));
        Assert.Equal(QuotaOperation.NotFound, store.Complete(_otra, id, 1));
        Assert.Equal(QuotaOperation.NotFound, store.CancelBeforeDispatch(_otra, id));
        Assert.Equal(10, store.Snapshot(_instalacion)!.ReservedUnits);
    }

    [Fact]
    public void ConfiguracionDuplicadaOInvalidaSeRechaza()
    {
        Assert.Throws<ArgumentException>(() => new LocalAssistantQuotaStore([
            new LocalQuotaConfiguration(_instalacion, AssistantMode.Local, true, 10, 1),
            new LocalQuotaConfiguration(_instalacion, AssistantMode.Local, true, 10, 1)
        ]));
        Assert.Throws<ArgumentException>(() => new LocalAssistantQuotaStore([
            new LocalQuotaConfiguration(_instalacion, AssistantMode.Local, true, -1, 1)
        ]));
    }

    [Fact]
    public void IdentidadNoExistenteNoConsumePresupuesto()
    {
        var store = Store();
        Assert.Equal(QuotaOperation.NotFound, store.TryReserve(_otra, 10).Outcome);
        Assert.Equal(QuotaOperation.Invalid, store.TryReserve(_instalacion, 0).Outcome);
        Assert.Equal(0, store.Snapshot(_instalacion)!.ConsumedUnits);
    }
}
