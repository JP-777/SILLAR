using Sillar.Core.Auditing;
using Sillar.Core.Contracts;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// M12.09 — auditoría editorial durable: sin estrategia ratificada no se registra
/// nada y nada se presenta como atómico.
/// </summary>
/// <remarks>Lógica pura. Sin base de datos.</remarks>
public sealed class AuditoriaEditorialM12Tests
{
    private static readonly EditorialActor Actor = new(7, "principal", AdminRole.Editor, Guid.Parse("01920000-0000-7000-8000-0000000000c1"));

    private static EditorialAuditRecord Evento(Guid? id = null)
        => new(
            id ?? Guid.CreateVersion7(),
            Guid.Parse("01920000-0000-7000-8000-000000000001"),
            Actor,
            "01920000-0000-7000-8000-0000000000b1",
            EditorialOperation.Publish,
            new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.FromHours(-5)));

    private static readonly UnratifiedEditorialAudit Productor = new();

    [Fact]
    public async Task Un_evento_valido_se_rechaza_porque_no_hay_estrategia_ratificada()
    {
        var r = await Productor.RecordAsync(Evento(), TestContext.Current.CancellationToken);

        Assert.Equal(EditorialAuditOutcome.Rejected, r.Outcome);
        Assert.Equal(EditorialAuditFailure.StrategyUnratified, r.Failure);
        Assert.False(r.IsAtomicWithEditorialCommit);
    }

    [Fact]
    public void El_productor_no_declara_estrategia_ni_atomicidad_ni_ratificacion()
    {
        Assert.Equal(new EditorialAuditCapability(EditorialAuditStrategy.None, false, false), Productor.Capability);
        Assert.False(EditorialAuditRequirements.PermitsEditorialCommit(Productor.Capability));
    }

    public static TheoryData<EditorialAuditRecord> Invalidos => new()
    {
        Evento(Guid.Empty),
        Evento(Guid.Parse("3f2504e0-4f89-41d3-9a0c-0305e82c3301")), // v4, no v7
        Evento() with { InstallationKey = Guid.Empty },
        Evento() with { Actor = Actor with { AdminUserId = 0 } },
        Evento() with { Actor = Actor with { SessionId = Guid.Empty } },
        Evento() with { Actor = Actor with { HomeNode = "" } },
        Evento() with { ArticleId = "" },
        Evento() with { ArticleId = "con espacio" },
        Evento() with { Operation = (EditorialOperation)99 },
    };

    [Theory]
    [MemberData(nameof(Invalidos))]
    public async Task Un_evento_invalido_se_rechaza_como_invalido(EditorialAuditRecord evento)
        => Assert.Equal(EditorialAuditFailure.RecordInvalid,
            (await Productor.RecordAsync(evento, TestContext.Current.CancellationToken)).Failure);

    [Theory]
    [InlineData(EditorialAuditStrategy.None, true, true)]
    [InlineData(EditorialAuditStrategy.SharedTransaction, true, false)]
    [InlineData(EditorialAuditStrategy.DurableOutbox, true, false)]
    [InlineData(EditorialAuditStrategy.SharedTransaction, false, true)]
    [InlineData(EditorialAuditStrategy.DurableOutbox, false, true)]
    public void Sin_estrategia_ratificada_y_atomica_M12_no_puede_confirmar(EditorialAuditStrategy estrategia, bool atomica, bool ratificada)
        => Assert.False(EditorialAuditRequirements.PermitsEditorialCommit(new(estrategia, atomica, ratificada)));

    [Fact]
    public async Task Con_el_token_del_llamador_cancelado_se_propaga_la_cancelacion()
        => await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Productor.RecordAsync(Evento(), new CancellationToken(canceled: true)));

    [Fact]
    public void CORE_no_registra_el_productor_editorial_por_su_cuenta()
    {
        var servicios = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        servicios.AddCoreEssentials(Configuracion(), "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x").AddCoreAuthentication();

        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IEditorialAudit));
    }
}
