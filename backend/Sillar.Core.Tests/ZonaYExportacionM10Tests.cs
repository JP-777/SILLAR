using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Core.Auditing;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Core.Settings;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// Zona horaria de la instalación y auditoría obligatoria de exportaciones
/// (Fase B, candidatos para M10).
/// </summary>
/// <remarks>
/// Lógica pura salvo los casos de escritura real, que usan un puerto cerrado: la
/// conexión se rechaza antes de llegar a ningún servidor. Sin PostgreSQL.
/// </remarks>
public sealed class ZonaYExportacionM10Tests
{
    // ── Zona horaria ───────────────────────────────────────────────────────
    // Conciliadas con el contrato definitivo de CORE (1d7c968): la resolución
    // pasa por InstallationTimeZoneEvaluator, con Status y Reason en lugar de
    // Verification y Failure. Sin hechos de fuente ni de atestación, como hoy.

    private static readonly DateTimeOffset Ahora = new(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(-5));

    private static InstallationTimeZoneResolution Resolver(string? valor, string inicial = IInstallationTimeZoneResolver.InitialZoneId)
        => InstallationTimeZoneEvaluator.Evaluate(IanaZoneCandidate.Evaluate(valor), inicial, facts: null, Ahora);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PENDIENTE_DEFINIR")]
    public void Sin_valor_configurado_la_zona_es_la_inicial_aprobada_y_sin_verificar(string? valor)
    {
        var zona = Resolver(valor);

        Assert.True(zona.IsResolved);
        Assert.Equal("America/Lima", zona.ZoneId);
        Assert.Equal("America/Lima", zona.Zone!.Id);
        Assert.Equal(InstallationTimeZoneOrigin.ApprovedInitial, zona.Origin);
        Assert.Equal(TimeZoneVerificationStatus.Unverified, zona.Status);
        Assert.Equal(TimeZoneVerificationReason.InitialZoneNotAttestable, zona.Reason);
        Assert.Equal(IanaZoneOutcome.Missing, zona.Setting.Outcome);
    }

    [Fact]
    public void Con_un_valor_configurado_que_el_runtime_resuelve_se_usa_ese_y_tampoco_queda_verificado()
    {
        var zona = Resolver("America/Bogota");

        Assert.Equal("America/Bogota", zona.ZoneId);
        Assert.Equal(InstallationTimeZoneOrigin.ConfiguredSetting, zona.Origin);
        // Sin fuente TZDB, que es lo que hay hoy: no verificable, nunca verificada.
        Assert.Equal(TimeZoneVerificationStatus.SourceNotVerifiable, zona.Status);
        Assert.NotEqual(TimeZoneVerificationStatus.Verified, zona.Status);
    }

    [Theory]
    [InlineData("SA Pacific Standard Time")]
    [InlineData("Lima")]
    [InlineData("America/Ciudad_Que_No_Existe")]
    public void Un_valor_invalido_no_cae_a_la_zona_inicial(string valor)
    {
        var zona = Resolver(valor);

        Assert.False(zona.IsResolved);
        Assert.Null(zona.ZoneId);
        Assert.Null(zona.Origin);
        Assert.Equal(TimeZoneVerificationReason.ConfiguredValueInvalid, zona.Reason);
        Assert.Equal(valor, zona.Setting.OriginalValue);
    }

    [Fact]
    public void Si_el_runtime_no_conoce_la_zona_inicial_no_se_inventa_otra()
    {
        var zona = Resolver(null, "America/Zona_Que_No_Existe");

        Assert.False(zona.IsResolved);
        Assert.Equal(TimeZoneVerificationReason.InitialZoneUnresolvable, zona.Reason);
    }

    // «Una_evaluacion_canonica_tampoco_convierte_la_zona_en_verificada» queda
    // obsoleta: IanaZoneOutcome.IanaCanonicalVerified ya no existe en el contrato.
    // Sustituta: ZonaVerificacionM10Tests.Con_todos_los_hechos_a_favor_no_hay_verificado_sin_autoridad_ratificada,
    // que exige lo mismo con todos los hechos de fuente y atestación a favor.

    // ── Exportación: metadatos ─────────────────────────────────────────────

    private static readonly ExportAuditRequest Valida = new("reporting", "sales_by_day", "csv", 120);

    [Fact]
    public async Task Una_exportacion_valida_queda_registrada_con_los_metadatos_minimos_y_el_autor_de_la_sesion()
    {
        var escritor = new EscritorFalso();
        var (auditoria, contexto) = Auditoria(escritor);
        using var _ = contexto;

        var resultado = await auditoria.RecordAsync(Valida, TestContext.Current.CancellationToken);

        Assert.True(resultado.IsRecorded);
        Assert.Equal(7, resultado.ExportId!.Value.Version);
        var entrada = Assert.Single(escritor.Entradas);
        Assert.Equal(
            new AuditEntry(AuditAction.Export)
            {
                AdminUserId = 7,
                AdminUserEmail = "operador@ejemplo.test",
                ModuleCode = "reporting",
                EntityType = "sales_by_day",
                EntityId = resultado.ExportId.ToString(),
                Summary = "Exportación sales_by_day en csv, 120 filas.",
            },
            entrada);
    }

    [Fact]
    public async Task Sin_numero_de_filas_el_resumen_no_inventa_uno()
    {
        var escritor = new EscritorFalso();
        var (auditoria, contexto) = Auditoria(escritor);
        using var _ = contexto;

        await auditoria.RecordAsync(Valida with { RowCount = null }, TestContext.Current.CancellationToken);

        Assert.Equal("Exportación sales_by_day en csv.", escritor.Entradas.Single().Summary);
    }

    [Theory]
    [InlineData(null, "operador@ejemplo.test")]
    [InlineData(7, null)]
    [InlineData(7, "")]
    public async Task Sin_administrador_identificado_no_se_registra_ni_se_escribe(int? id, string? correo)
    {
        var escritor = new EscritorFalso();
        var (auditoria, contexto) = Auditoria(escritor, new AdminFalso(id, correo));
        using var _ = contexto;

        var resultado = await auditoria.RecordAsync(Valida, TestContext.Current.CancellationToken);

        Assert.False(resultado.IsRecorded);
        Assert.Equal(ExportAuditFailure.ActorMissing, resultado.Failure);
        Assert.Null(resultado.ExportId);
        Assert.Empty(escritor.Entradas);
    }

    public static TheoryData<ExportAuditRequest> Invalidas => new()
    {
        Valida with { ModuleCode = "" },
        Valida with { ModuleCode = "Reporting" },
        Valida with { ModuleCode = new string('a', 41) },
        Valida with { ExportKind = "" },
        Valida with { ExportKind = "ventas de Juan Pérez" },
        Valida with { ExportKind = "sales-by-day" },
        Valida with { ExportKind = "1sales" },
        Valida with { ExportKind = new string('a', 61) },
        Valida with { Format = "" },
        Valida with { Format = "CSV" },
        Valida with { Format = "x.csv" },
        Valida with { Format = new string('a', 11) },
        Valida with { RowCount = -1 },
    };

    [Theory]
    [MemberData(nameof(Invalidas))]
    public async Task Un_metadato_fuera_de_formato_no_se_registra_ni_se_escribe(ExportAuditRequest peticion)
    {
        var escritor = new EscritorFalso();
        var (auditoria, contexto) = Auditoria(escritor);
        using var _ = contexto;

        var resultado = await auditoria.RecordAsync(peticion, TestContext.Current.CancellationToken);

        Assert.Equal(ExportAuditFailure.MetadataInvalid, resultado.Failure);
        Assert.Empty(escritor.Entradas);
    }

    [Fact]
    public void Los_limites_de_formato_son_los_de_las_columnas_de_auditoria()
    {
        Assert.True(ExportAudit.IsValid(new(new string('a', 40), new string('b', 60), new string('c', 10), 0)));
    }

    // ── Exportación: escritura que falla ───────────────────────────────────

    [Fact]
    public async Task Si_la_auditoria_no_se_puede_escribir_no_se_registra_y_no_queda_nada_pendiente()
    {
        // Escritor real de CORE contra un puerto cerrado: SaveChanges falla.
        var contexto = Contexto(CadenaSinServidor);
        await using var _ = contexto;
        var escritor = new AuditWriter(contexto, new HttpContextAccessor(), TimeProvider.System);
        var auditoria = new ExportAudit(escritor, new AdminFalso(7, "operador@ejemplo.test"), contexto, NullLogger<ExportAudit>.Instance);

        var resultado = await auditoria.RecordAsync(Valida, TestContext.Current.CancellationToken);

        Assert.False(resultado.IsRecorded);
        Assert.Equal(ExportAuditFailure.AuditUnavailable, resultado.Failure);
        Assert.Null(resultado.ExportId);
        Assert.Empty(contexto.ChangeTracker.Entries<AuditLog>());
    }

    [Fact]
    public async Task Con_el_token_del_llamador_cancelado_se_propaga_la_cancelacion_y_no_se_escribe()
    {
        var escritor = new EscritorFalso();
        var (auditoria, contexto) = Auditoria(escritor);
        using var _ = contexto;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => auditoria.RecordAsync(Valida, new CancellationToken(canceled: true)));
        Assert.Empty(escritor.Entradas);
    }

    [Fact]
    public async Task Si_el_llamador_cancela_durante_la_escritura_se_propaga_aunque_el_escritor_lance_otra_cosa()
    {
        using var origen = new CancellationTokenSource();
        var escritor = new EscritorFalso(() =>
        {
            origen.Cancel();
            throw new InvalidOperationException("conexión cortada");
        });
        var (auditoria, contexto) = Auditoria(escritor);
        using var _ = contexto;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => auditoria.RecordAsync(Valida, origen.Token));
    }

    // ── Registro ───────────────────────────────────────────────────────────

    [Fact]
    public void CORE_no_registra_los_productores_candidatos_por_su_cuenta()
    {
        var servicios = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        servicios.AddCoreEssentials(Configuracion(), CadenaSinServidor).AddCoreAuthentication();

        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IExportAudit));
        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IInstallationTimeZoneResolver));
    }

    // ── Dobles ─────────────────────────────────────────────────────────────

    private const string CadenaSinServidor = "Host=127.0.0.1;Port=1;Database=ninguna;Username=nadie;Password=nada;Timeout=2";

    private static (ExportAudit Auditoria, CoreDbContext Contexto) Auditoria(IAuditWriter escritor, ICurrentAdmin? admin = null)
    {
        var contexto = Contexto(CadenaSinServidor);
        return (new ExportAudit(escritor, admin ?? new AdminFalso(7, "operador@ejemplo.test"), contexto, NullLogger<ExportAudit>.Instance), contexto);
    }

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

    private sealed class AdminFalso(int? id, string? correo) : ICurrentAdmin
    {
        public int? AdminUserId => id;
        public string? Email => correo;
        public string? Role => id is null ? null : AdminRole.Admin;
        public string? DisplayName => id is null ? null : "Operador";
        public string? HomeNode => id is null ? null : "principal";
        public bool IsInRole(string role) => false;
    }
}
