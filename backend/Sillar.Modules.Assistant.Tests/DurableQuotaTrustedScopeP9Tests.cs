using Npgsql;
using Sillar.Modules.Assistant.Endpoints;
using Sillar.Modules.Assistant.Services;
using Xunit;

namespace Sillar.Modules.Assistant.Tests;

/// <summary>
/// P9: la autoridad de instalacion puede fallar antes de abrir DB.
/// Ninguno de estos casos conecta a PostgreSQL: el puerto 1 no ofrece DB de pruebas.
/// Solo acredita la denegacion previa a DB; no CORE real ni revocacion en transaccion.
/// </summary>
public sealed class DurableQuotaTrustedScopeP9Tests
{
    private static readonly Guid Installation = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid Lease = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    private sealed class InstallationScope(Guid value, bool allow = true, bool throws = false)
        : ITrustedAssistantInstallationScope
    {
        public int Calls { get; private set; }

        public bool TryGetEnabledInstallation(out Guid installationId)
        {
            Calls++;
            installationId = value;
            if (throws) throw new InvalidOperationException("Autoridad CORE/Host no disponible");
            return allow;
        }
    }

    private static NpgsqlDataSource NoDatabase() => NpgsqlDataSource.Create(
        "Host=127.0.0.1;Port=1;Database=sillar_p9_sin_bd;Username=nadie;Password=nadie;Timeout=1;Pooling=false");

    [Fact]
    public async Task ReservaDeniegaSiLaAutoridadLanzaExcepcion()
    {
        using var db = NoDatabase();
        var scope = new InstallationScope(Installation, throws: true);
        var ledger = new DurableAssistantQuotaLedger(db, scope);
        var result = await ledger.TryReserveAsync(10, "reserva-p9-01");
        Assert.Equal(DurableQuotaOutcome.Denied, result.Outcome);
        Assert.Equal(1, scope.Calls);
    }

    [Fact]
    public async Task DespachoDeniegaSiLaAutoridadLanzaExcepcion()
    {
        using var db = NoDatabase();
        var ledger = new DurableAssistantQuotaLedger(db, new InstallationScope(Installation, throws: true));
        Assert.Equal(DurableQuotaOutcome.Denied, (await ledger.MarkDispatchedAsync(Lease)).Outcome);
    }

    [Fact]
    public async Task InciertoDeniegaSiLaAutoridadLanzaExcepcion()
    {
        using var db = NoDatabase();
        var ledger = new DurableAssistantQuotaLedger(db, new InstallationScope(Installation, throws: true));
        Assert.Equal(DurableQuotaOutcome.Denied, (await ledger.MarkUncertainAsync(Lease)).Outcome);
    }

    [Fact]
    public async Task LiquidacionDeniegaSiLaAutoridadLanzaExcepcion()
    {
        using var db = NoDatabase();
        var ledger = new DurableAssistantQuotaLedger(db, new InstallationScope(Installation, throws: true));
        Assert.Equal(DurableQuotaOutcome.Denied, (await ledger.CompleteAsync(Lease, 4)).Outcome);
    }

    [Fact]
    public async Task CancelacionDeniegaSiLaAutoridadLanzaExcepcion()
    {
        using var db = NoDatabase();
        var ledger = new DurableAssistantQuotaLedger(db, new InstallationScope(Installation, throws: true));
        Assert.Equal(DurableQuotaOutcome.Denied, (await ledger.CancelBeforeDispatchAsync(Lease)).Outcome);
    }

    [Fact]
    public async Task ListadoNoExponeReservasSiLaAutoridadLanzaExcepcion()
    {
        using var db = NoDatabase();
        var ledger = new DurableAssistantQuotaLedger(db, new InstallationScope(Installation, throws: true));
        Assert.Empty(await ledger.ListOutstandingAsync());
    }

    [Fact]
    public async Task ReservaDeniegaScopePositivoConGuidVacio()
    {
        using var db = NoDatabase();
        var ledger = new DurableAssistantQuotaLedger(db, new InstallationScope(Guid.Empty));
        Assert.Equal(DurableQuotaOutcome.Denied, (await ledger.TryReserveAsync(10, "reserva-p9-02")).Outcome);
    }

    [Fact]
    public async Task ReservaDeniegaScopeNegativoAunqueEntregueGuid()
    {
        using var db = NoDatabase();
        var ledger = new DurableAssistantQuotaLedger(db, new InstallationScope(Installation, allow: false));
        Assert.Equal(DurableQuotaOutcome.Denied, (await ledger.TryReserveAsync(10, "reserva-p9-03")).Outcome);
    }

    [Fact]
    public async Task DespachoDeniegaScopeNegativoAunqueEntregueGuid()
    {
        using var db = NoDatabase();
        var ledger = new DurableAssistantQuotaLedger(db, new InstallationScope(Installation, allow: false));
        Assert.Equal(DurableQuotaOutcome.Denied, (await ledger.MarkDispatchedAsync(Lease)).Outcome);
    }

    [Fact]
    public async Task ListadoVacioAnteGuidVacio()
    {
        using var db = NoDatabase();
        var ledger = new DurableAssistantQuotaLedger(db, new InstallationScope(Guid.Empty));
        Assert.Empty(await ledger.ListOutstandingAsync());
    }

    [Fact]
    public async Task ListadoVacioAnteScopeNegativoAunqueEntregueGuid()
    {
        using var db = NoDatabase();
        var ledger = new DurableAssistantQuotaLedger(db, new InstallationScope(Installation, allow: false));
        Assert.Empty(await ledger.ListOutstandingAsync());
    }
}
