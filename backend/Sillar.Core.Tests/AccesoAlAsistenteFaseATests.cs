using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Core.Authorization;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain.Values;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// Acceso al Asistente en Fase A (candidato): con hechos favorables queda
/// pendiente, con cualquier duda se deniega, y nunca se concede.
/// </summary>
/// <remarks>
/// Lógica pura salvo la sección del productor, que construye contextos cuya
/// conexión falla antes de llegar a ningún servidor —un interceptor que lanza al
/// abrir, o un puerto cerrado—. Ninguna prueba de este archivo usa PostgreSQL.
/// </remarks>
public sealed class AccesoAlAsistenteFaseATests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 23, 10, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid Clave = Guid.Parse("01920000-0000-7000-8000-000000000001");

    private static AssistantFactsRow Hechos(
        Guid? clave = null,
        bool completa = true,
        string licencia = LicenseType.Subscription,
        DateTimeOffset? hasta = null,
        bool sinFecha = false,
        bool declarado = true,
        bool conActivacion = true,
        bool activo = true,
        DateTimeOffset? vence = null)
        => new(
            clave ?? Clave,
            completa,
            licencia,
            sinFecha ? null : hasta ?? Ahora.AddDays(30),
            declarado,
            conActivacion,
            activo,
            vence);

    private static AssistantAccessDecision Evaluar(params AssistantFactsRow[] filas)
        => AssistantAccessEvaluator.Evaluate(filas, Ahora);

    // ── Hechos favorables: pendiente, nunca concedido ─────────────────────

    public static TheoryData<string, DateTimeOffset?, DateTimeOffset?> Favorables => new()
    {
        { LicenseType.Subscription, Ahora.AddDays(30), null },
        { LicenseType.Trial, Ahora.AddDays(7), Ahora.AddDays(3) },
        { LicenseType.Perpetual, null, null },
        { LicenseType.Perpetual, null, Ahora.AddYears(1) },
    };

    [Theory]
    [MemberData(nameof(Favorables))]
    public void Con_hechos_favorables_queda_pendiente_y_nunca_se_concede(string licencia, DateTimeOffset? hasta, DateTimeOffset? vence)
    {
        var decision = Evaluar(Hechos(licencia: licencia, hasta: hasta, sinFecha: hasta is null, vence: vence));

        Assert.Equal(AssistantAccessOutcome.PendingDecision, decision.Outcome);
        Assert.Equal(AssistantAccessReason.EntitlementPolicyUnratified, decision.Reason);
        Assert.False(decision.IsAllowed);
        Assert.Equal(Clave, decision.InstallationKey);
        Assert.Equal(AssistantAccessEvaluator.PendingSource, decision.Source);
        Assert.Equal(Ahora, decision.ObservedAt);
        Assert.NotNull(decision.Facts);
    }

    [Fact]
    public void La_vigencia_pendiente_es_la_primera_fecha_de_fin_entre_licencia_y_activacion()
    {
        var decision = Evaluar(Hechos(hasta: Ahora.AddDays(30), vence: Ahora.AddDays(10)));

        Assert.Equal(Ahora.AddDays(10), decision.ValidUntil);
    }

    [Fact]
    public void Una_licencia_perpetua_con_activacion_sin_fin_no_tiene_vigencia_que_informar()
        => Assert.Null(Evaluar(Hechos(licencia: LicenseType.Perpetual, sinFecha: true)).ValidUntil);

    [Fact]
    public void Los_hechos_leidos_viajan_con_la_decision()
    {
        var vence = Ahora.AddDays(5);

        var hechos = Evaluar(Hechos(licencia: LicenseType.Trial, hasta: Ahora.AddDays(9), vence: vence)).Facts!;

        Assert.Equal(new AssistantAccessFacts(LicenseType.Trial, Ahora.AddDays(9), true, true, true, vence), hechos);
    }

    // ── Identidad ──────────────────────────────────────────────────────────

    [Fact]
    public void Sin_instalacion_se_deniega_sin_clave_ni_hechos()
    {
        var decision = Evaluar();

        AssertDenegado(AssistantAccessReason.InstallationMissing, decision);
        Assert.Null(decision.InstallationKey);
        Assert.Null(decision.Facts);
    }

    [Fact]
    public void Una_clave_de_instalacion_vacia_se_deniega_y_no_se_devuelve()
    {
        // Aunque todo lo demás sea favorable: sin identidad no hay de quién hablar.
        var decision = Evaluar(Hechos(clave: Guid.Empty));

        AssertDenegado(AssistantAccessReason.InstallationIdentityInvalid, decision);
        Assert.Null(decision.InstallationKey);
    }

    [Fact]
    public void Con_dos_instalaciones_no_se_elige_ninguna()
    {
        var decision = Evaluar(Hechos(), Hechos(clave: Guid.Parse("01920000-0000-7000-8000-000000000002")));

        AssertDenegado(AssistantAccessReason.InstallationIdentityInvalid, decision);
        Assert.Null(decision.InstallationKey);
    }

    [Fact]
    public void Con_la_instalacion_a_medio_configurar_se_deniega()
        => AssertDenegado(AssistantAccessReason.InstallationIncomplete, Evaluar(Hechos(completa: false)));

    // ── Licencia ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("TRIAL")]
    [InlineData("Perpetual")]
    [InlineData("enterprise")]
    public void Un_tipo_de_licencia_desconocido_se_deniega_aunque_se_parezca_a_uno_valido(string tipo)
        => AssertDenegado(AssistantAccessReason.LicenseUnrecognized, Evaluar(Hechos(licencia: tipo)));

    [Theory]
    [InlineData(LicenseType.Trial)]
    [InlineData(LicenseType.Subscription)]
    public void Una_licencia_temporal_sin_fecha_de_fin_no_se_lee_como_ilimitada(string tipo)
        => AssertDenegado(AssistantAccessReason.LicenseExpiryMissing, Evaluar(Hechos(licencia: tipo, sinFecha: true)));

    [Fact]
    public void Una_licencia_perpetua_con_fecha_de_fin_es_incoherente_y_se_deniega()
        => AssertDenegado(AssistantAccessReason.LicenseInconsistent, Evaluar(Hechos(licencia: LicenseType.Perpetual, hasta: Ahora.AddDays(1))));

    [Fact]
    public void Una_licencia_vencida_se_deniega()
        => AssertDenegado(AssistantAccessReason.LicenseExpired, Evaluar(Hechos(hasta: Ahora.AddTicks(-1))));

    [Fact]
    public void Una_licencia_que_vence_en_este_mismo_instante_ya_esta_vencida()
        => AssertDenegado(AssistantAccessReason.LicenseExpired, Evaluar(Hechos(hasta: Ahora)));

    [Fact]
    public void La_licencia_se_comprueba_antes_que_el_modulo()
        => AssertDenegado(AssistantAccessReason.LicenseExpired, Evaluar(Hechos(hasta: Ahora.AddDays(-1), declarado: false)));

    // ── Módulo ─────────────────────────────────────────────────────────────

    [Fact]
    public void Sin_el_modulo_declarado_se_deniega()
        => AssertDenegado(AssistantAccessReason.ModuleMissing, Evaluar(Hechos(declarado: false, conActivacion: false, activo: false)));

    [Fact]
    public void Un_modulo_sin_fila_de_activacion_cuenta_como_inactivo()
        => AssertDenegado(AssistantAccessReason.ModuleInactive, Evaluar(Hechos(conActivacion: false, activo: false)));

    [Fact]
    public void Con_el_modulo_desactivado_se_deniega()
        => AssertDenegado(AssistantAccessReason.ModuleInactive, Evaluar(Hechos(activo: false)));

    [Fact]
    public void Una_activacion_que_vence_en_este_mismo_instante_ya_esta_vencida()
        => AssertDenegado(AssistantAccessReason.ModuleExpired, Evaluar(Hechos(vence: Ahora)));

    private static void AssertDenegado(AssistantAccessReason esperado, AssistantAccessDecision decision)
    {
        Assert.Equal(AssistantAccessOutcome.Denied, decision.Outcome);
        Assert.Equal(esperado, decision.Reason);
        Assert.False(decision.IsAllowed);
        Assert.Null(decision.ValidUntil);
        Assert.Equal(AssistantAccessEvaluator.Source, decision.Source);
        Assert.Equal(Ahora, decision.ObservedAt);
    }

    // ── Productor: sin autoridad no hay prueba ────────────────────────────

    private const string CadenaSinServidor = "Host=127.0.0.1;Port=1;Database=ninguna;Username=nadie;Password=nada;Timeout=2";

    private static (CandidateAssistantAccessAuthority Autoridad, CoreDbContext Contexto, RelojContado Reloj) Productor(IInterceptor? interceptor = null)
    {
        var opciones = new DbContextOptionsBuilder<CoreDbContext>(CoreDataServiceExtensions.BuildOptions(CadenaSinServidor));
        if (interceptor is not null)
        {
            opciones.AddInterceptors(interceptor);
        }

        var contexto = new CoreDbContext(opciones.Options, Nodo, TimeProvider.System);
        var reloj = new RelojContado(Ahora);
        return (new CandidateAssistantAccessAuthority(contexto, reloj, NullLogger<CandidateAssistantAccessAuthority>.Instance), contexto, reloj);
    }

    [Fact]
    public async Task Si_la_base_no_responde_se_deniega_por_autoridad_no_disponible()
    {
        var (autoridad, contexto, reloj) = Productor();
        await using var _ = contexto;

        var decision = await autoridad.EvaluateCurrentInstallationAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AssistantAccessOutcome.Denied, decision.Outcome);
        Assert.Equal(AssistantAccessReason.AuthorityUnavailable, decision.Reason);
        Assert.Null(decision.InstallationKey);
        Assert.Null(decision.Facts);
        Assert.Equal(Ahora, decision.ObservedAt);
        Assert.Equal(1, reloj.Lecturas);
    }

    public static TheoryData<string> FallosInternos => new() { "cancelacion-interna", "tiempo-agotado", "proveedor" };

    [Theory]
    [MemberData(nameof(FallosInternos))]
    public async Task Un_fallo_interno_con_el_token_del_llamador_vivo_se_deniega_y_no_se_propaga(string fallo)
    {
        Exception excepcion = fallo switch
        {
            "cancelacion-interna" => new OperationCanceledException("cancelación interna"),
            "tiempo-agotado" => new TimeoutException("tiempo agotado"),
            _ => new InvalidOperationException("fallo del proveedor"),
        };
        var (autoridad, contexto, reloj) = Productor(new FallaAlAbrir(excepcion));
        await using var _ = contexto;

        var decision = await autoridad.EvaluateCurrentInstallationAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AssistantAccessOutcome.Denied, decision.Outcome);
        Assert.Equal(AssistantAccessReason.AuthorityUnavailable, decision.Reason);
        Assert.Equal(1, reloj.Lecturas);
    }

    [Fact]
    public async Task Con_el_token_del_llamador_ya_cancelado_se_propaga_la_cancelacion()
    {
        var (autoridad, contexto, reloj) = Productor();
        await using var _ = contexto;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => autoridad.EvaluateCurrentInstallationAsync(new CancellationToken(canceled: true)));
        Assert.Equal(0, reloj.Lecturas);
    }

    [Fact]
    public async Task Si_el_llamador_cancela_durante_la_lectura_se_propaga_aunque_el_proveedor_lance_otra_cosa()
    {
        using var origen = new CancellationTokenSource();
        var (autoridad, contexto, _) = Productor(new FallaAlAbrir(new InvalidOperationException("conexión cortada"), origen));
        await using var __ = contexto;

        var excepcion = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => autoridad.EvaluateCurrentInstallationAsync(origen.Token));
        Assert.Equal(origen.Token, excepcion.CancellationToken);
    }

    [Fact]
    public void La_lectura_de_hechos_es_una_sola_sentencia_sobre_instalacion_y_modulos()
    {
        // Traducir la consulta no abre conexión. Si EF no pudiera traducirla
        // entera a SQL, lanzaría aquí en vez de partirla en varias lecturas.
        var (autoridad, contexto, _) = Productor();
        using var _ = contexto;

        var sql = autoridad.FactsQuery().ToQueryString();

        Assert.Contains("core.installation", sql);
        Assert.Contains("core.modules", sql);
        Assert.Contains("core.module_activations", sql);
        Assert.Contains("LIMIT", sql);
        Assert.DoesNotContain(";", sql.TrimEnd().TrimEnd(';'));
    }

    // ── Registro ───────────────────────────────────────────────────────────

    [Fact]
    public void CORE_no_registra_el_productor_candidato_por_su_cuenta()
    {
        var servicios = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        servicios.AddCoreEssentials(Configuracion(), CadenaSinServidor).AddCoreAuthentication();

        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IAssistantAccessAuthority));
    }

    // ── Dobles ─────────────────────────────────────────────────────────────

    private sealed class RelojContado(DateTimeOffset ahora) : TimeProvider
    {
        public int Lecturas { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            Lecturas++;
            return ahora.ToUniversalTime();
        }
    }

    /// <summary>Lanza antes de que la conexión se abra: no sale ningún paquete.</summary>
    private sealed class FallaAlAbrir(Exception excepcion, CancellationTokenSource? cancelarAntes = null) : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            cancelarAntes?.Cancel();
            throw excepcion;
        }
    }
}
