using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Core.Contracts;
using Sillar.Core.Services;
using Sillar.Core.Settings;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// M10.08 — estados de verificación de la zona IANA. Ningún camino llega a
/// <see cref="TimeZoneVerificationStatus.Verified"/>.
/// </summary>
/// <remarks>
/// Lógica pura salvo el productor, cuyo contexto apunta a un puerto cerrado: la
/// conexión se rechaza antes de llegar a ningún servidor. Sin PostgreSQL.
/// </remarks>
public sealed class ZonaVerificacionM10Tests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 26, 12, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid Instalacion = Guid.Parse("01920000-0000-7000-8000-000000000001");
    private static readonly Guid Atestacion = Guid.Parse("01920000-0000-7000-8000-0000000000a1");

    private static readonly IanaZoneEvaluation Bogota = IanaZoneCandidate.Evaluate("America/Bogota");
    private static readonly TzdbObservation Fuente = new(true, "tzdata.zi", "2026e", true);

    private static TimeZoneAttestation Atestada(
        string zona = "America/Bogota",
        Guid? instalacion = null,
        string fuente = "tzdata.zi",
        string version = "2026e",
        DateTimeOffset? revocada = null)
        => new(Atestacion, instalacion ?? Instalacion, zona, fuente, version, 1, "principal", Ahora.AddDays(-3), revocada);

    private static TimeZoneFacts Hechos(TimeZoneAttestation? atestacion = null, TzdbObservation? fuente = null, Lookup? busqueda = null)
        => new(
            fuente ?? Fuente,
            busqueda ?? (atestacion is null ? Lookup.NotFound : Lookup.Found),
            atestacion,
            Lookup.Found,
            Instalacion);

    private static InstallationTimeZoneResolution Evaluar(TimeZoneFacts? hechos, IanaZoneEvaluation? valor = null)
        => InstallationTimeZoneEvaluator.Evaluate(valor ?? Bogota, IInstallationTimeZoneResolver.InitialZoneId, hechos, Ahora);

    private static void Estado(TimeZoneVerificationStatus estado, TimeZoneVerificationReason razon, InstallationTimeZoneResolution r)
    {
        Assert.Equal(estado, r.Status);
        Assert.Equal(razon, r.Reason);
        Assert.NotEqual(TimeZoneVerificationStatus.Verified, r.Status);
        Assert.Equal(Ahora, r.ObservedAt);
    }

    // ── Zona inicial e inválida ────────────────────────────────────────────

    [Fact]
    public void La_zona_inicial_America_Lima_sigue_sin_verificar_aunque_haya_atestacion()
    {
        var r = Evaluar(Hechos(Atestada("America/Lima")), IanaZoneCandidate.Evaluate(null));

        Assert.Equal("America/Lima", r.ZoneId);
        Assert.Equal(InstallationTimeZoneOrigin.ApprovedInitial, r.Origin);
        Estado(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.InitialZoneNotAttestable, r);
        Assert.Null(r.AttestationId);
    }

    [Fact]
    public void Un_valor_configurado_invalido_no_tiene_zona_ni_cae_a_la_inicial()
    {
        var r = Evaluar(Hechos(Atestada()), IanaZoneCandidate.Evaluate("SA Pacific Standard Time"));

        Assert.False(r.IsResolved);
        Estado(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.ConfiguredValueInvalid, r);
    }

    [Fact]
    public void Si_el_runtime_no_conoce_la_zona_inicial_no_se_inventa_otra()
    {
        var r = InstallationTimeZoneEvaluator.Evaluate(IanaZoneCandidate.Evaluate(null), "America/Zona_Que_No_Existe", null, Ahora);

        Assert.False(r.IsResolved);
        Estado(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.InitialZoneUnresolvable, r);
    }

    // ── Fuente ─────────────────────────────────────────────────────────────

    [Fact]
    public void Sin_fuente_TZDB_la_zona_configurada_es_no_verificable()
    {
        var r = Evaluar(Hechos(Atestada(), TzdbObservation.Unavailable));

        Assert.Equal("America/Bogota", r.ZoneId);
        Assert.Equal(InstallationTimeZoneOrigin.ConfiguredSetting, r.Origin);
        Estado(TimeZoneVerificationStatus.SourceNotVerifiable, TimeZoneVerificationReason.TzdbSourceUnavailable, r);
    }

    [Fact]
    public void Un_alias_en_la_fuente_no_se_verifica()
        => Estado(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.NotCanonicalInSource,
            Evaluar(Hechos(Atestada(), Fuente with { IsCanonical = false })));

    // ── Atestación ─────────────────────────────────────────────────────────

    [Fact]
    public void Sin_atestacion_queda_sin_verificar()
        => Estado(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.NotAttested, Evaluar(Hechos()));

    [Fact]
    public void Si_la_atestacion_no_se_puede_leer_queda_sin_verificar()
        => Estado(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.AttestationUnavailable,
            Evaluar(Hechos(busqueda: Lookup.Unavailable)));

    [Fact]
    public void Una_atestacion_revocada_da_revocada()
    {
        var r = Evaluar(Hechos(Atestada(revocada: Ahora.AddHours(-1))));

        Estado(TimeZoneVerificationStatus.Revoked, TimeZoneVerificationReason.AttestationRevoked, r);
        Assert.Equal(Atestacion, r.AttestationId);
    }

    [Fact]
    public void Una_atestacion_de_otro_valor_de_zona_esta_obsoleta()
        => Estado(TimeZoneVerificationStatus.Stale, TimeZoneVerificationReason.StaleValueChanged,
            Evaluar(Hechos(Atestada("America/Lima"))));

    [Fact]
    public void Una_atestacion_de_otra_version_de_tzdata_esta_obsoleta()
        => Estado(TimeZoneVerificationStatus.Stale, TimeZoneVerificationReason.StaleSourceChanged,
            Evaluar(Hechos(Atestada(version: "2025b"))));

    [Fact]
    public void Una_atestacion_de_otra_fuente_esta_obsoleta()
        => Estado(TimeZoneVerificationStatus.Stale, TimeZoneVerificationReason.StaleSourceChanged,
            Evaluar(Hechos(Atestada(fuente: "otra-tzdb"))));

    [Fact]
    public void Una_atestacion_de_otra_instalacion_esta_obsoleta()
        => Estado(TimeZoneVerificationStatus.Stale, TimeZoneVerificationReason.StaleInstallationChanged,
            Evaluar(Hechos(Atestada(instalacion: Guid.Parse("01920000-0000-7000-8000-000000000002")))));

    [Fact]
    public void Sin_identidad_unica_de_instalacion_queda_sin_verificar()
        => Estado(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.InstallationUnavailable,
            Evaluar(Hechos(Atestada()) with { InstallationLookup = Lookup.NotFound, InstallationKey = null }));

    [Fact]
    public void Con_todos_los_hechos_a_favor_no_hay_verificado_sin_autoridad_ratificada()
    {
        var r = Evaluar(Hechos(Atestada()));

        Estado(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.AttestationAuthorityUnratified, r);
        Assert.Equal(Atestacion, r.AttestationId);
        Assert.True(r.IsResolved);
    }

    // ── Productor ──────────────────────────────────────────────────────────

    private const string CadenaSinServidor = "Host=127.0.0.1;Port=1;Database=ninguna;Username=nadie;Password=nada;Timeout=2";

    private static InstallationTimeZoneResolver Productor(string? valor, ITimeZoneAttestationStore? atestaciones = null, ITzdbSource? fuente = null)
        => new(
            new LectorFijo(valor),
            fuente ?? new UnavailableTzdbSource(),
            atestaciones ?? new NoTimeZoneAttestationStore(),
            Contexto(CadenaSinServidor),
            new RelojFijo(Ahora),
            NullLogger<InstallationTimeZoneResolver>.Instance);

    [Fact]
    public async Task Con_los_adaptadores_actuales_una_zona_configurada_es_no_verificable()
    {
        var r = await Productor("America/Bogota").ResolveAsync(TestContext.Current.CancellationToken);

        Estado(TimeZoneVerificationStatus.SourceNotVerifiable, TimeZoneVerificationReason.TzdbSourceUnavailable, r);
    }

    [Fact]
    public async Task Con_los_adaptadores_actuales_sin_valor_la_zona_es_la_inicial_sin_verificar()
    {
        var r = await Productor(null).ResolveAsync(TestContext.Current.CancellationToken);

        Assert.Equal("America/Lima", r.ZoneId);
        Estado(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.InitialZoneNotAttestable, r);
    }

    [Fact]
    public async Task Si_la_instalacion_no_se_puede_leer_la_zona_queda_sin_verificar()
    {
        var r = await Productor("America/Bogota", new AtestacionesFijas(Atestada()), new FuenteFija(Fuente))
            .ResolveAsync(TestContext.Current.CancellationToken);

        Estado(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.InstallationUnavailable, r);
    }

    [Fact]
    public async Task Si_el_almacen_de_atestaciones_falla_la_zona_queda_sin_verificar()
    {
        var r = await Productor("America/Bogota", new AtestacionesQueFallan(), new FuenteFija(Fuente))
            .ResolveAsync(TestContext.Current.CancellationToken);

        Estado(TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.AttestationUnavailable, r);
    }

    [Fact]
    public async Task Con_el_token_del_llamador_cancelado_se_propaga_la_cancelacion()
        => await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Productor("America/Bogota").ResolveAsync(new CancellationToken(canceled: true)));

    [Fact]
    public void El_marcador_de_ausencia_es_el_mismo_que_deja_la_semilla()
        => Assert.Equal(SiteSettingService.PendingMarker, IanaZoneCandidate.PendingMarker);

    [Fact]
    public void CORE_no_registra_el_productor_de_zona_por_su_cuenta()
    {
        var servicios = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        servicios.AddCoreEssentials(Configuracion(), CadenaSinServidor).AddCoreAuthentication();

        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IInstallationTimeZoneResolver));
    }

    // ── Dobles ─────────────────────────────────────────────────────────────

    private sealed class LectorFijo(string? valor) : ISettingsReader
    {
        public string? Get(string key) => key == IanaZoneCandidate.SettingKey ? valor : null;
        public T? Get<T>(string key) => throw new NotSupportedException();
        public IReadOnlyDictionary<string, string> GetPublic() => throw new NotSupportedException();
    }

    private sealed class FuenteFija(TzdbObservation observacion) : ITzdbSource
    {
        public TzdbObservation Observe(string zoneId) => observacion;
    }

    private sealed class AtestacionesFijas(TimeZoneAttestation atestacion) : ITimeZoneAttestationStore
    {
        public Task<TimeZoneAttestation?> ReadCurrentAsync(CancellationToken cancellationToken)
            => Task.FromResult<TimeZoneAttestation?>(atestacion);
    }

    private sealed class AtestacionesQueFallan : ITimeZoneAttestationStore
    {
        public Task<TimeZoneAttestation?> ReadCurrentAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("almacén caído");
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora.ToUniversalTime();
    }
}
