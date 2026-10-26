using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Core.Auditing;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// M10.13 — auditoría de exportación antes del archivo, con el tipo de M10
/// (<c>reporting</c> / <c>order_indicators</c> / <c>csv</c>).
/// </summary>
/// <remarks>
/// Con el vocabulario real ninguna exportación se registra: <c>export</c> no está
/// en <see cref="AuditAction.All"/>. El camino positivo se prueba con un
/// vocabulario de prueba y un escritor falso; en el producto sigue cerrado.
/// Sin PostgreSQL: los contextos apuntan a un puerto cerrado.
/// </remarks>
public sealed class ExportacionAuditadaM10Tests
{
    private static readonly ExportAuditRequest DeM10 = new("reporting", "order_indicators", "csv", 42);
    private static readonly string[] ConExport = [.. AuditAction.All, ExportAudit.ExportActionCode];
    private const string CadenaSinServidor = "Host=127.0.0.1;Port=1;Database=ninguna;Username=nadie;Password=nada;Timeout=2";

    private static (ExportAudit Auditoria, CoreDbContext Contexto) Auditoria(
        IAuditWriter escritor, ICurrentAdmin? admin = null, IReadOnlyCollection<string>? vocabulario = null)
    {
        var contexto = Contexto(CadenaSinServidor);
        var auditoria = vocabulario is null
            ? new ExportAudit(escritor, admin ?? Operadora(), contexto, NullLogger<ExportAudit>.Instance)
            : new ExportAudit(escritor, admin ?? Operadora(), contexto, NullLogger<ExportAudit>.Instance, vocabulario);
        return (auditoria, contexto);
    }

    private static AdminFalso Operadora() => new(7, "operadora@ejemplo.test", "principal");

    // ── Con el vocabulario real: cerrado ──────────────────────────────────

    [Fact]
    public void El_vocabulario_de_auditoria_de_esta_base_no_admite_export()
        => Assert.DoesNotContain(ExportAudit.ExportActionCode, AuditAction.All);

    [Fact]
    public async Task Con_el_vocabulario_real_la_exportacion_de_M10_se_rechaza_sin_escribir_ni_entregar()
    {
        var escritor = new EscritorFalso();
        var (auditoria, contexto) = Auditoria(escritor);
        using var _ = contexto;
        var entregas = 0;

        var r = await auditoria.DeliverAuditedAsync(DeM10, (_, _) => Task.FromResult(++entregas), TestContext.Current.CancellationToken);

        Assert.Equal(ExportAuditFailure.ExportActionNotAdmitted, r.Audit.Failure);
        Assert.False(r.Delivered);
        Assert.Equal(0, entregas);
        Assert.Empty(escritor.Entradas);
    }

    // ── Camino positivo, solo con vocabulario de prueba ───────────────────

    [Fact]
    public async Task Con_export_admitido_se_registra_con_el_actor_real_y_despues_se_entrega_con_su_identificador()
    {
        var orden = new List<string>();
        var escritor = new EscritorFalso(() => orden.Add("auditoria"));
        var (auditoria, contexto) = Auditoria(escritor, vocabulario: ConExport);
        using var _ = contexto;

        var r = await auditoria.DeliverAuditedAsync(DeM10, (id, _) => { orden.Add("archivo"); return Task.FromResult(id); },
            TestContext.Current.CancellationToken);

        Assert.True(r.Audit.IsRecorded);
        Assert.True(r.Delivered);
        Assert.Equal(r.Audit.ExportId, r.Value);
        Assert.Equal(["auditoria", "archivo"], orden);
        Assert.Equal(new ExportActor(7, "operadora@ejemplo.test", "principal"), r.Audit.Actor);
        Assert.Equal(
            new AuditEntry("export")
            {
                AdminUserId = 7,
                AdminUserEmail = "operadora@ejemplo.test",
                ModuleCode = "reporting",
                EntityType = "order_indicators",
                EntityId = r.Audit.ExportId.ToString(),
                Summary = "Exportación order_indicators en csv, 42 filas.",
            },
            Assert.Single(escritor.Entradas));
    }

    // ── Actor ausente ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, "operadora@ejemplo.test", "principal")]
    [InlineData(7, null, "principal")]
    [InlineData(7, "", "principal")]
    [InlineData(7, "operadora@ejemplo.test", null)]
    [InlineData(7, "operadora@ejemplo.test", "")]
    public async Task Sin_actor_completo_no_se_registra_ni_se_entrega(int? id, string? correo, string? nodo)
    {
        var escritor = new EscritorFalso();
        var (auditoria, contexto) = Auditoria(escritor, new AdminFalso(id, correo, nodo), ConExport);
        using var _ = contexto;
        var entregas = 0;

        var r = await auditoria.DeliverAuditedAsync(DeM10, (_, _) => Task.FromResult(++entregas), TestContext.Current.CancellationToken);

        Assert.Equal(ExportAuditFailure.ActorMissing, r.Audit.Failure);
        Assert.Equal(0, entregas);
        Assert.Empty(escritor.Entradas);
    }

    // ── Metadatos ──────────────────────────────────────────────────────────

    public static TheoryData<ExportAuditRequest> Ilegales => new()
    {
        DeM10 with { ModuleCode = "Reporting" },
        DeM10 with { ExportKind = "Order_Indicators" },
        DeM10 with { ExportKind = "order-indicators" },
        DeM10 with { ExportKind = "pedidos de Juan Pérez" },
        DeM10 with { Format = "CSV" },
        DeM10 with { Format = "x.csv" },
        DeM10 with { RowCount = -1 },
    };

    [Theory]
    [MemberData(nameof(Ilegales))]
    public async Task Un_metadato_ilegal_no_se_registra_ni_se_entrega(ExportAuditRequest peticion)
    {
        var escritor = new EscritorFalso();
        var (auditoria, contexto) = Auditoria(escritor, vocabulario: ConExport);
        using var _ = contexto;
        var entregas = 0;

        var r = await auditoria.DeliverAuditedAsync(peticion, (_, _) => Task.FromResult(++entregas), TestContext.Current.CancellationToken);

        Assert.Equal(ExportAuditFailure.MetadataInvalid, r.Audit.Failure);
        Assert.Equal(0, entregas);
        Assert.Empty(escritor.Entradas);
    }

    // ── Auditoría fallida: ni un byte ──────────────────────────────────────

    [Fact]
    public async Task Si_el_escritor_falla_no_se_entrega_nada()
    {
        var escritor = new EscritorFalso(() => throw new InvalidOperationException("base caída"));
        var (auditoria, contexto) = Auditoria(escritor, vocabulario: ConExport);
        using var _ = contexto;
        var entregas = 0;

        var r = await auditoria.DeliverAuditedAsync(DeM10, (_, _) => Task.FromResult(++entregas), TestContext.Current.CancellationToken);

        Assert.Equal(ExportAuditFailure.AuditUnavailable, r.Audit.Failure);
        Assert.Null(r.Audit.ExportId);
        Assert.False(r.Delivered);
        Assert.Equal(0, entregas);
    }

    [Fact]
    public async Task Con_el_escritor_real_contra_una_base_caida_no_se_entrega_y_no_queda_fila_pendiente()
    {
        var contexto = Contexto(CadenaSinServidor);
        await using var _ = contexto;
        var escritor = new AuditWriter(contexto, new HttpContextAccessor(), TimeProvider.System);
        var auditoria = new ExportAudit(escritor, Operadora(), contexto, NullLogger<ExportAudit>.Instance, ConExport);
        var entregas = 0;

        var r = await auditoria.DeliverAuditedAsync(DeM10, (_, _) => Task.FromResult(++entregas), TestContext.Current.CancellationToken);

        Assert.Equal(ExportAuditFailure.AuditUnavailable, r.Audit.Failure);
        Assert.Equal(0, entregas);
        Assert.Empty(contexto.ChangeTracker.Entries<AuditLog>());
    }

    [Fact]
    public async Task Con_el_token_del_llamador_cancelado_se_propaga_y_no_se_escribe()
    {
        var escritor = new EscritorFalso();
        var (auditoria, contexto) = Auditoria(escritor, vocabulario: ConExport);
        using var _ = contexto;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => auditoria.RecordAsync(DeM10, new CancellationToken(canceled: true)));
        Assert.Empty(escritor.Entradas);
    }

    [Fact]
    public void CORE_no_registra_el_productor_de_exportacion_por_su_cuenta()
    {
        var servicios = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        servicios.AddCoreEssentials(Configuracion(), CadenaSinServidor).AddCoreAuthentication();

        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IExportAudit));
    }

    // ── Dobles ─────────────────────────────────────────────────────────────

    private sealed class EscritorFalso(Action? alEscribir = null) : IAuditWriter
    {
        public List<AuditEntry> Entradas { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
        {
            alEscribir?.Invoke();
            Entradas.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class AdminFalso(int? id, string? correo, string? nodo) : ICurrentAdmin
    {
        public int? AdminUserId => id;
        public string? Email => correo;
        public string? Role => id is null ? null : AdminRole.Admin;
        public string? DisplayName => id is null ? null : "Operadora";
        public string? HomeNode => nodo;
        public bool IsInRole(string role) => false;
    }
}
