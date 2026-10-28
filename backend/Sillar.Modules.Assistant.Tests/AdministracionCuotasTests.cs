using Sillar.Modules.Assistant.Domain;
using Sillar.Modules.Assistant.Services;

namespace Sillar.Modules.Assistant.Tests;

public class AdministracionCuotasTests
{
    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();
    private static readonly DateTimeOffset Ahora = new(2026, 10, 19, 12, 0, 0, TimeSpan.FromHours(-5));

    private LocalAssistantQuotaStore Store() => new([
        new LocalQuotaConfiguration(_a, AssistantMode.Local, true, 100, 2),
        new LocalQuotaConfiguration(_b, AssistantMode.Faq, false, 80, 0)
    ]);

    private TrustedQuotaAdministrator Admin(Guid? inst = null, string role = EditorialRoles.SuperAdmin,
        bool scope = true, string key = "operador-7") => new(inst ?? _a, role, key, scope);

    private LocalQuotaConfiguration Change(AssistantMode mode = AssistantMode.ExternalApi,
        long limit = 200, bool engine = true, int concurrency = 3) =>
        new(_a, mode, engine, limit, concurrency);

    [Fact]
    public void SuperAdminCambiaPoliticaConVersionYAuditoriaLocal()
    {
        var s = Store();
        Assert.Equal(QuotaPolicyUpdateOutcome.Updated, s.UpdatePolicy(Admin(), Change(), 0, Ahora));
        var policy = s.ReadPolicy(Admin())!;
        Assert.Equal(1, policy.Version);
        Assert.Equal(AssistantMode.ExternalApi, policy.Configuration.Mode);
        var log = Assert.Single(s.ReadPolicyChanges(Admin()));
        Assert.Equal("operador-7", log.ActorAuditKey);
        Assert.Equal(0, log.PreviousVersion);
        Assert.Equal(1, log.NewVersion);
        Assert.Equal(Ahora.ToUniversalTime(), log.ChangedAt);
    }

    [Theory]
    [InlineData(EditorialRoles.Editor)]
    [InlineData(EditorialRoles.Admin)]
    [InlineData("visitante")]
    public void NingunOtroRolCambiaOLeeLaPolitica(string role)
    {
        var s = Store();
        Assert.Equal(QuotaPolicyUpdateOutcome.Forbidden,
            s.UpdatePolicy(Admin(role: role), Change(), 0, Ahora));
        Assert.Null(s.ReadPolicy(Admin(role: role)));
        Assert.Empty(s.ReadPolicyChanges(Admin(role: role)));
        Assert.Equal(0, s.ReadPolicy(Admin())!.Version);
    }

    [Fact]
    public void AutoridadSinAmbitoNoPuedeModificar()
    {
        var s = Store();
        Assert.Equal(QuotaPolicyUpdateOutcome.Forbidden,
            s.UpdatePolicy(Admin(scope: false), Change(), 0, Ahora));
        Assert.Null(s.ReadPolicy(Admin(scope: false)));
        Assert.Equal(0, s.ReadPolicy(Admin())!.Version);
    }

    [Fact]
    public void NoPermiteModificarInstalacionAjena()
    {
        var s = Store();
        Assert.Equal(QuotaPolicyUpdateOutcome.Invalid,
            s.UpdatePolicy(Admin(_b), Change(), 0, Ahora));
        Assert.Equal(0, s.ReadPolicy(Admin(_b))!.Version);
    }

    [Fact]
    public void UnSoloCambioGanaCuandoCompitenConLaMismaVersion()
    {
        var s = Store();
        var results = Enumerable.Range(0, 32).AsParallel()
            .Select(i => s.UpdatePolicy(Admin(), Change(limit: 101 + i), 0, Ahora))
            .ToArray();
        Assert.Single(results, r => r == QuotaPolicyUpdateOutcome.Updated);
        Assert.Equal(31, results.Count(r => r == QuotaPolicyUpdateOutcome.Conflict));
        Assert.Equal(1, s.ReadPolicy(Admin())!.Version);
    }

    [Fact]
    public void VersionAntiguaNoSobrescribePolitica()
    {
        var s = Store();
        Assert.Equal(QuotaPolicyUpdateOutcome.Updated,
            s.UpdatePolicy(Admin(), Change(), 0, Ahora));
        Assert.Equal(QuotaPolicyUpdateOutcome.Conflict,
            s.UpdatePolicy(Admin(), Change(limit: 300), 0, Ahora));
        Assert.Equal(200, s.ReadPolicy(Admin())!.Configuration.LimitUnits);
    }

    [Fact]
    public void RepeticionExactaEsInocuaYNoDuplicaAuditoria()
    {
        var s = Store();
        Assert.Equal(QuotaPolicyUpdateOutcome.Updated,
            s.UpdatePolicy(Admin(), Change(), 0, Ahora));
        Assert.Equal(QuotaPolicyUpdateOutcome.Unchanged,
            s.UpdatePolicy(Admin(), Change(), 1, Ahora));
        Assert.Single(s.ReadPolicyChanges(Admin()));
        Assert.Equal(1, s.ReadPolicy(Admin())!.Version);
    }

    [Fact]
    public void NoCambiaModoConReservaPendiente()
    {
        var s = Store();
        var token = s.TryReserve(_a, 50).ReservationId!.Value;
        Assert.Equal(QuotaPolicyUpdateOutcome.InFlight,
            s.UpdatePolicy(Admin(), Change(), 0, Ahora));
        Assert.Equal(QuotaOperation.Cancelled, s.CancelBeforeDispatch(_a, token));
        Assert.Equal(QuotaPolicyUpdateOutcome.Updated,
            s.UpdatePolicy(Admin(), Change(), 0, Ahora));
    }

    [Fact]
    public void NoCambiaModoSiElMotorYaRecibioUnaPeticionIncierta()
    {
        var s = Store();
        var token = s.TryReserve(_a, 50).ReservationId!.Value;
        s.MarkDispatched(_a, token);
        Assert.Equal(QuotaPolicyUpdateOutcome.InFlight,
            s.UpdatePolicy(Admin(), Change(), 0, Ahora));
        Assert.Equal(50, s.Snapshot(_a)!.ReservedUnits);
        Assert.Equal(AssistantMode.Local, s.Snapshot(_a)!.Mode);
    }

    [Fact]
    public void MantieneConsumoConfirmadoTrasCambio()
    {
        var s = Store();
        var token = s.TryReserve(_a, 50).ReservationId!.Value;
        s.MarkDispatched(_a, token);
        s.Complete(_a, token, 30);
        Assert.Equal(QuotaPolicyUpdateOutcome.Updated,
            s.UpdatePolicy(Admin(), Change(AssistantMode.Local), 0, Ahora));
        Assert.Equal(30, s.Snapshot(_a)!.ConsumedUnits);
        Assert.Equal(200, s.Snapshot(_a)!.LimitUnits);
    }

    [Fact]
    public void NoBajaLimitePorDebajoDeConsumoConfirmado()
    {
        var s = Store();
        var token = s.TryReserve(_a, 50).ReservationId!.Value;
        s.MarkDispatched(_a, token);
        s.Complete(_a, token, 30);
        Assert.Equal(QuotaPolicyUpdateOutcome.Conflict,
            s.UpdatePolicy(Admin(), Change(limit: 29), 0, Ahora));
        Assert.Equal(100, s.Snapshot(_a)!.LimitUnits);
    }

    [Fact]
    public void NoMezclaUnidadesDeMotoresConConsumoPrevio()
    {
        var s = Store();
        var token = s.TryReserve(_a, 50).ReservationId!.Value;
        s.MarkDispatched(_a, token);
        s.Complete(_a, token, 30);
        Assert.Equal(QuotaPolicyUpdateOutcome.Conflict,
            s.UpdatePolicy(Admin(), Change(), 0, Ahora));
        Assert.Equal(AssistantMode.Local, s.ReadPolicy(Admin())!.Configuration.Mode);
        Assert.Equal(30, s.Snapshot(_a)!.ConsumedUnits);
    }

    [Fact]
    public void CambiarAApiNoInvocaUnProveedorNiCambiaPorFallback()
    {
        var s = Store();
        s.UpdatePolicy(Admin(), Change(), 0, Ahora);
        var reserved = s.TryReserve(_a, 20);
        Assert.Equal(QuotaOperation.Reserved, reserved.Outcome);
        Assert.Equal(AssistantMode.ExternalApi, reserved.Mode);
        Assert.Equal(0, s.Snapshot(_b)!.ReservedUnits);
        Assert.Equal(AssistantMode.Faq, s.Snapshot(_b)!.Mode);
    }

    [Fact]
    public void ConfiguracionFaqNoPuedeMantenerMotorEncendido()
    {
        var s = Store();
        Assert.Equal(QuotaPolicyUpdateOutcome.Invalid,
            s.UpdatePolicy(Admin(), Change(AssistantMode.Faq), 0, Ahora));
        Assert.Equal(0, s.ReadPolicy(Admin())!.Version);
    }

    [Fact]
    public void RegistroDeCambiosNoSePuedeMutarPorElLlamador()
    {
        var s = Store();
        s.UpdatePolicy(Admin(), Change(), 0, Ahora);
        var old = s.ReadPolicyChanges(Admin());
        Assert.Single(old);
        s.UpdatePolicy(Admin(), Change(limit: 300), 1, Ahora);
        Assert.Single(old);
        Assert.Equal(2, s.ReadPolicyChanges(Admin()).Count);
    }

    [Fact]
    public void LecturaDeAuditoriaEsLimitadaYRechazaValoresInvalidos()
    {
        var s = Store();
        s.UpdatePolicy(Admin(), Change(), 0, Ahora);
        s.UpdatePolicy(Admin(), Change(limit: 300), 1, Ahora);
        Assert.Single(s.ReadPolicyChanges(Admin(), 1));
        Assert.Empty(s.ReadPolicyChanges(Admin(), 0));
        Assert.Empty(s.ReadPolicyChanges(Admin(), 101));
        Assert.Empty(s.ReadPolicyChanges(Admin(_b)));
    }

    [Fact]
    public void ActorDeAuditoriaInvalidoNoPuedeCambiarConfiguracion()
    {
        var s = Store();
        Assert.Equal(QuotaPolicyUpdateOutcome.Forbidden,
            s.UpdatePolicy(Admin(key: "  "), Change(), 0, Ahora));
        Assert.Equal(QuotaPolicyUpdateOutcome.Forbidden,
            s.UpdatePolicy(Admin(key: "operador\\ncontrol".Replace("\\n", "\n")), Change(), 0, Ahora));
        Assert.Equal(0, s.ReadPolicy(Admin())!.Version);
    }

    [Fact]
    public void ModalidadFaqExplicitaNuncaReserva()
    {
        var s = Store();
        Assert.Equal(QuotaOperation.FaqOnly, s.TryReserve(_b, 10).Outcome);
        Assert.Equal(0, s.Snapshot(_b)!.ConsumedUnits);
    }

    [Fact]
    public void InstalacionInexistenteNoDevuelveDatosNiPuedeSerConfigurada()
    {
        var s = Store();
        var missing = Admin(Guid.NewGuid());
        Assert.Null(s.ReadPolicy(missing));
        Assert.Equal(QuotaPolicyUpdateOutcome.NotFound,
            s.UpdatePolicy(missing, new LocalQuotaConfiguration(missing.InstallationId,
                AssistantMode.Local, true, 50, 1), 0, Ahora));
    }
}
