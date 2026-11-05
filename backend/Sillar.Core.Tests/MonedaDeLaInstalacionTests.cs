using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Core.Auditing;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Core.Dtos;
using Sillar.Core.Migrations;
using Sillar.Core.Services;
using Sillar.Core.Settings;
using Sillar.Shared.Events;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// Moneda de la instalación: autoridad monetaria privada, que nace sin moneda,
/// solo la establece super_admin y se guarda con su auditoría o no se guarda.
/// </summary>
/// <remarks>
/// <para>
/// Pruebas unitarias, sin PostgreSQL. Los contextos apuntan a un puerto cerrado:
/// cualquier intento de tocar la base falla al instante, así que un resultado
/// distinto de <see cref="CurrencyChangeOutcome.NotSaved"/> prueba que la base no
/// se tocó.
/// </para>
/// <para>
/// <b>Lo que solo se puede acreditar con PostgreSQL</b> —establecer PEN de verdad,
/// el bloqueo <c>FOR UPDATE</c>, que un fallo de la auditoría deshaga el cambio y
/// que la migración sea idempotente— queda pendiente de validación física.
/// </para>
/// </remarks>
public sealed class MonedaDeLaInstalacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 4, 12, 0, 0, TimeSpan.FromHours(-5));
    private const string CadenaSinServidor = "Host=127.0.0.1;Port=1;Database=ninguna;Username=nadie;Password=nada;Timeout=2";

    // ── Lista cerrada y versionada ─────────────────────────────────────────

    [Fact]
    public void La_lista_de_monedas_soportadas_es_exactamente_PEN_con_dos_decimales_en_su_version_1()
    {
        Assert.Equal("sillar-iso4217-v1", SupportedCurrencies.Version);
        Assert.Equal([new SupportedCurrency("PEN", 2)], SupportedCurrencies.All);
    }

    [Fact]
    public void Los_datos_de_PEN_coinciden_con_los_de_CLDR_del_runtime()
    {
        // Contraste, no fuente: la publicación oficial no fue alcanzable.
        Assert.Equal("PEN", new RegionInfo("PE").ISOCurrencySymbol);
        Assert.Equal(2, CultureInfo.GetCultureInfo("es-PE").NumberFormat.CurrencyDecimalDigits);
    }

    [Fact]
    public void Los_decimales_soportados_caben_en_las_columnas_de_importe()
        // numeric(12,2) en catálogo, ventas, cotizaciones y servicios.
        => Assert.All(SupportedCurrencies.All, moneda => Assert.Equal(2, moneda.MinorUnits));

    // ── Validación ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, CurrencyValueStatus.Missing)]
    [InlineData("", CurrencyValueStatus.Empty)]
    [InlineData("   ", CurrencyValueStatus.Empty)]
    [InlineData("PENDIENTE_DEFINIR", CurrencyValueStatus.Pending)]
    [InlineData("pen", CurrencyValueStatus.Malformed)]
    [InlineData("Pen", CurrencyValueStatus.Malformed)]
    [InlineData(" PEN", CurrencyValueStatus.Malformed)]
    [InlineData("PEN ", CurrencyValueStatus.Malformed)]
    [InlineData("PE", CurrencyValueStatus.Malformed)]
    [InlineData("PENN", CurrencyValueStatus.Malformed)]
    [InlineData("P3N", CurrencyValueStatus.Malformed)]
    [InlineData("ÑEN", CurrencyValueStatus.Malformed)]
    [InlineData("S/", CurrencyValueStatus.Malformed)]
    [InlineData("604", CurrencyValueStatus.Malformed)]
    [InlineData("USD", CurrencyValueStatus.Unsupported)]
    [InlineData("EUR", CurrencyValueStatus.Unsupported)]
    [InlineData("XXX", CurrencyValueStatus.Unsupported)]
    [InlineData("PEN", CurrencyValueStatus.Supported)]
    public void Cada_valor_se_clasifica_sin_recortar_ni_cambiar_mayusculas(string? valor, CurrencyValueStatus esperado)
        => Assert.Equal(esperado, InstallationCurrency.Check(valor).Status);

    // ── Texto de auditoría, que es el comprobante ──────────────────────────

    [Fact]
    public void El_registro_lleva_el_valor_anterior_el_nuevo_y_la_version_de_la_lista()
        => Assert.Equal(
            "Moneda de la instalación: anterior=PENDIENTE_DEFINIR; nueva=PEN; lista=sillar-iso4217-v1.",
            InstallationCurrencyAuditText.Compose("PENDIENTE_DEFINIR", "PEN"));

    [Fact]
    public void Un_valor_anterior_no_reconocible_no_se_copia_al_registro()
        => Assert.Contains("anterior=valor-no-admitido;", InstallationCurrencyAuditText.Compose("; nueva=PEN", "PEN"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Configuración 'installation.currency_iso4217' actualizada.")]
    [InlineData("Moneda de la instalación: anterior=PEN; nueva=pen; lista=sillar-iso4217-v1.")]
    [InlineData("Moneda de la instalación: anterior=PEN; nueva=PEN; lista=sillar-iso4217-v1. extra")]
    public void Un_registro_sin_la_forma_exacta_no_acredita_ninguna_moneda(string? texto)
        => Assert.Null(InstallationCurrencyAuditText.NewValue(texto));

    // ── Lectura de la autoridad ────────────────────────────────────────────

    private static readonly string RegistroPen = InstallationCurrencyAuditText.Compose("PENDIENTE_DEFINIR", "PEN");

    [Fact]
    public void PEN_guardado_con_su_registro_esta_acreditado()
    {
        var lectura = InstallationCurrencyEvaluator.Evaluate(true, "PEN", RegistroPen, Ahora);

        Assert.True(lectura.IsAccredited);
        Assert.Equal(new SupportedCurrency("PEN", 2), lectura.Currency);
        Assert.Equal(SupportedCurrencies.Version, lectura.ListVersion);
        Assert.Equal(Ahora, lectura.ObservedAt);
    }

    [Fact]
    public void Una_instalacion_preparada_nace_sin_moneda()
        => NoAcreditada(InstallationCurrencyStatus.NotEstablished,
            InstallationCurrencyEvaluator.Evaluate(true, InstallationCurrency.PendingMarker, null, Ahora));

    [Fact]
    public void Sin_la_clave_la_instalacion_no_esta_preparada()
        => NoAcreditada(InstallationCurrencyStatus.NotPrepared, InstallationCurrencyEvaluator.Evaluate(false, null, null, Ahora));

    [Theory]
    [InlineData("USD")]
    [InlineData("pen")]
    [InlineData("S/")]
    public void Un_valor_guardado_fuera_de_la_lista_no_se_acredita(string valor)
        => NoAcreditada(InstallationCurrencyStatus.Unsupported, InstallationCurrencyEvaluator.Evaluate(true, valor, RegistroPen, Ahora));

    [Fact]
    public void PEN_escrito_por_fuera_sin_registro_no_se_acredita()
        => NoAcreditada(InstallationCurrencyStatus.NotAttested, InstallationCurrencyEvaluator.Evaluate(true, "PEN", null, Ahora));

    [Fact]
    public void PEN_con_un_registro_que_dice_otra_moneda_no_se_acredita()
        => NoAcreditada(InstallationCurrencyStatus.NotAttested,
            InstallationCurrencyEvaluator.Evaluate(true, "PEN",
                "Moneda de la instalación: anterior=PEN; nueva=USD; lista=sillar-iso4217-v1.", Ahora));

    [Fact]
    public void Un_registro_generico_de_configuracion_no_acredita()
        => NoAcreditada(InstallationCurrencyStatus.NotAttested,
            InstallationCurrencyEvaluator.Evaluate(true, "PEN", "Configuración 'installation.currency_iso4217' actualizada.", Ahora));

    private static void NoAcreditada(InstallationCurrencyStatus esperado, InstallationCurrencyReading lectura)
    {
        Assert.Equal(esperado, lectura.Status);
        Assert.False(lectura.IsAccredited);
        Assert.Null(lectura.Currency);
    }

    [Fact]
    public void La_lectura_es_una_sola_sentencia_sobre_la_clave_privada_y_nunca_mira_currency_code()
    {
        using var contexto = Contexto(CadenaSinServidor);

        var sql = new InstallationCurrencyAuthority(contexto, TimeProvider.System, NullLogger<InstallationCurrencyAuthority>.Instance)
            .FactsQuery().ToQueryString();

        Assert.Contains("core.site_settings", sql);
        Assert.Contains("core.audit_log", sql);
        Assert.Contains("installation.currency_iso4217", sql);
        Assert.DoesNotContain("currency_code", sql);
        Assert.DoesNotContain(";", sql);
    }

    [Fact]
    public void La_escritura_y_la_lectura_buscan_el_comprobante_con_el_mismo_filtro()
    {
        using var contexto = Contexto(CadenaSinServidor);
        var lectura = new InstallationCurrencyAuthority(contexto, TimeProvider.System, NullLogger<InstallationCurrencyAuthority>.Instance)
            .FactsQuery().ToQueryString();
        var escritura = InstallationCurrencyAuditQuery.LatestSummary(contexto).Take(1).ToQueryString();

        foreach (var parte in new[] { "entity_type = 'setting'", "entity_id = 'installation.currency_iso4217'", "action = 'update'", "audit_log_id DESC" })
        {
            Assert.Contains(parte, lectura);
            Assert.Contains(parte, escritura);
        }
    }

    [Fact]
    public async Task Si_la_base_no_responde_la_moneda_no_esta_disponible()
    {
        await using var contexto = Contexto(CadenaSinServidor);

        var lectura = await new InstallationCurrencyAuthority(contexto, new RelojFijo(Ahora), NullLogger<InstallationCurrencyAuthority>.Instance)
            .ReadAsync(TestContext.Current.CancellationToken);

        NoAcreditada(InstallationCurrencyStatus.Unavailable, lectura);
    }

    // ── Operación administrativa ───────────────────────────────────────────

    private sealed record Montaje(InstallationCurrencyService Servicio, CoreDbContext Contexto, SondaFalsa? Sonda);

    private static Montaje Servicio(
        SondaFalsa? sonda = null,
        CoreDbContext? contextoDelEscritor = null,
        IEventPublisher? bus = null,
        CoreDbContext? baseDeDatos = null)
    {
        var contexto = baseDeDatos ?? Contexto(CadenaSinServidor);
        var proveedor = new ServiceCollection();
        if (sonda is not null)
        {
            proveedor.AddSingleton<IMonetaryRecordsProbe>(sonda);
        }

        var services = proveedor.BuildServiceProvider();
        var escritor = new AuditWriter(contextoDelEscritor ?? contexto, new HttpContextAccessor(), new RelojFijo(Ahora));
        var servicio = new InstallationCurrencyService(
            contexto,
            escritor,
            new SettingsCache(services.GetRequiredService<IServiceScopeFactory>()),
            bus ?? new BusVacio(),
            services,
            new RelojFijo(Ahora),
            NullLogger<InstallationCurrencyService>.Instance);
        return new(servicio, contexto, sonda);
    }

    private static Task<CurrencyChangeResult> Establecer(Montaje m, string? valor, bool superAdmin = true, CancellationToken? ct = null)
        => m.Servicio.SetAsync(valor, superAdmin, 1, "principal@ejemplo.test", ct ?? TestContext.Current.CancellationToken);

    [Fact]
    public async Task Sin_super_admin_no_se_establece_ni_se_pregunta_por_pedidos()
    {
        var m = Servicio(new SondaFalsa(MonetaryRecordsState.None));
        await using var _ = m.Contexto;

        var r = await Establecer(m, "PEN", superAdmin: false);

        Assert.Equal(CurrencyChangeOutcome.Forbidden, r.Outcome);
        Assert.Equal(0, m.Sonda!.Llamadas);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PENDIENTE_DEFINIR")]
    [InlineData("pen")]
    [InlineData(" PEN")]
    [InlineData("USD")]
    public async Task Un_valor_ausente_pendiente_vacio_malformado_o_no_admitido_se_rechaza_sin_tocar_nada(string? valor)
    {
        var m = Servicio(new SondaFalsa(MonetaryRecordsState.None));
        await using var _ = m.Contexto;

        var r = await Establecer(m, valor);

        Assert.Equal(CurrencyChangeOutcome.Invalid, r.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(r.Message));
        Assert.Equal(0, m.Sonda!.Llamadas);
    }

    [Fact]
    public async Task Sin_quien_informe_de_los_pedidos_la_moneda_no_se_establece()
    {
        var m = Servicio();
        await using var _ = m.Contexto;

        var r = await Establecer(m, "PEN");

        // No es NotSaved: la base no se llegó a tocar.
        Assert.Equal(CurrencyChangeOutcome.RecordsUnverifiable, r.Outcome);
    }

    [Fact]
    public async Task Con_pedidos_registrados_la_moneda_no_se_establece()
    {
        // Desde E5.2 la sonda se consulta dentro de la transacción, bajo el bloqueo:
        // hace falta una base que deje llegar hasta ella. Mismo resultado esperado.
        var m = Servicio(new SondaFalsa(MonetaryRecordsState.Present), baseDeDatos: new BaseGuionada().Contexto());
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.RecordsPresent, (await Establecer(m, "PEN")).Outcome);
    }

    [Fact]
    public async Task Si_ventas_no_sabe_si_hay_pedidos_la_moneda_no_se_establece()
    {
        // Desde E5.2 la sonda se consulta dentro de la transacción, bajo el bloqueo:
        // hace falta una base que deje llegar hasta ella. Mismo resultado esperado.
        var m = Servicio(new SondaFalsa(MonetaryRecordsState.Unknown), baseDeDatos: new BaseGuionada().Contexto());
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.RecordsUnverifiable, (await Establecer(m, "PEN")).Outcome);
    }

    [Fact]
    public async Task Si_el_informe_de_pedidos_falla_la_moneda_no_se_establece()
    {
        // Desde E5.2 la sonda se consulta dentro de la transacción, bajo el bloqueo:
        // hace falta una base que deje llegar hasta ella. Mismo resultado esperado.
        var m = Servicio(new SondaFalsa(MonetaryRecordsState.None, falla: true), baseDeDatos: new BaseGuionada().Contexto());
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.RecordsUnverifiable, (await Establecer(m, "PEN")).Outcome);
    }

    [Fact]
    public async Task Si_el_cambio_y_su_auditoria_no_se_pueden_guardar_no_queda_nada_pendiente()
    {
        var m = Servicio(new SondaFalsa(MonetaryRecordsState.None));
        await using var _ = m.Contexto;

        var r = await Establecer(m, "PEN");

        Assert.Equal(CurrencyChangeOutcome.NotSaved, r.Outcome);
        Assert.Empty(m.Contexto.ChangeTracker.Entries<AuditLog>());
        Assert.Empty(m.Contexto.ChangeTracker.Entries<SiteSetting>());
    }

    [Fact]
    public async Task Si_la_auditoria_iria_en_otro_contexto_no_se_establece()
    {
        await using var otro = Contexto(CadenaSinServidor);
        var m = Servicio(new SondaFalsa(MonetaryRecordsState.None), contextoDelEscritor: otro);
        await using var _ = m.Contexto;

        var r = await Establecer(m, "PEN");

        Assert.Equal(CurrencyChangeOutcome.AuditNotAtomic, r.Outcome);
        Assert.Equal(0, m.Sonda!.Llamadas);
    }

    [Fact]
    public async Task Con_el_token_del_llamador_cancelado_se_propaga_la_cancelacion()
    {
        var m = Servicio(new SondaFalsa(MonetaryRecordsState.None));
        await using var _ = m.Contexto;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Establecer(m, "PEN", ct: new CancellationToken(canceled: true)));
    }

    // ── O-1 · El comprobante se reconoce venga de donde venga el valor anterior ──

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("USD")]
    [InlineData("pen")]
    [InlineData("S/")]
    [InlineData("; nueva=PEN")]
    [InlineData("valor-no-admitido")]
    [InlineData("PENDIENTE_DEFINIR")]
    [InlineData("PEN")]
    public void Un_cambio_legitimo_desde_cualquier_valor_anterior_queda_acreditado(string anterior)
    {
        var registro = InstallationCurrencyAuditText.Compose(anterior, "PEN");

        Assert.Equal("PEN", InstallationCurrencyAuditText.NewValue(registro));
        Assert.True(InstallationCurrencyEvaluator.Evaluate(true, "PEN", registro, Ahora).IsAccredited);
    }

    [Fact]
    public void Un_valor_anterior_muy_largo_tampoco_rompe_el_comprobante()
    {
        var registro = InstallationCurrencyAuditText.Compose(new string('X', 500), "PEN");

        Assert.True(registro.Length <= 300);
        Assert.Equal("PEN", InstallationCurrencyAuditText.NewValue(registro));
    }

    [Theory]
    [InlineData("Moneda de la instalación: anterior=usd; nueva=PEN; lista=sillar-iso4217-v1.")]
    [InlineData("Moneda de la instalación: anterior=Pen; nueva=PEN; lista=sillar-iso4217-v1.")]
    [InlineData("Moneda de la instalación: anterior=valor-no-admitidoX; nueva=PEN; lista=sillar-iso4217-v1.")]
    [InlineData("Moneda de la instalación: anterior=VALOR-NO-ADMITIDO; nueva=PEN; lista=sillar-iso4217-v1.")]
    [InlineData("Moneda de la instalación: anterior=PENDIENTE; nueva=PEN; lista=sillar-iso4217-v1.")]
    [InlineData("Moneda de la instalación: anterior=; nueva=PEN; lista=sillar-iso4217-v1.")]
    public void El_valor_anterior_solo_admite_lo_que_el_propio_registro_puede_escribir(string texto)
        => Assert.Null(InstallationCurrencyAuditText.NewValue(texto));

    // ── O-2 · Igual al guardado no es lo mismo que acreditado ─────────────

    private static readonly string RegistroDePen = InstallationCurrencyAuditText.Compose("PENDIENTE_DEFINIR", "PEN");

    [Fact]
    public void Si_ya_es_PEN_y_esta_acreditado_no_se_escribe_nada()
        => Assert.Equal(CurrencyWriteDecision.Unchanged, InstallationCurrencyWriteRule.Decide("PEN", "PEN", RegistroDePen));

    [Theory]
    [InlineData(null)]
    [InlineData("Configuración 'installation.currency_iso4217' actualizada.")]
    [InlineData("Moneda de la instalación: anterior=PEN; nueva=USD; lista=sillar-iso4217-v1.")]
    public void Si_ya_es_PEN_pero_sin_comprobante_se_registra_el_acto_y_no_se_responde_que_ya_estaba(string? ultimo)
        => Assert.Equal(CurrencyWriteDecision.Reattest, InstallationCurrencyWriteRule.Decide("PEN", "PEN", ultimo));

    [Theory]
    [InlineData("PENDIENTE_DEFINIR")]
    [InlineData("USD")]
    [InlineData("pen")]
    public void Si_el_guardado_es_otro_valor_se_cambia(string guardado)
        => Assert.Equal(CurrencyWriteDecision.Change, InstallationCurrencyWriteRule.Decide(guardado, "PEN", RegistroDePen));

    [Fact]
    public void El_registro_de_un_reestablecimiento_acredita_el_valor()
    {
        var registro = InstallationCurrencyAuditText.Compose("PEN", "PEN");

        Assert.True(InstallationCurrencyEvaluator.Evaluate(true, "PEN", registro, Ahora).IsAccredited);
        Assert.Equal(CurrencyWriteDecision.Unchanged, InstallationCurrencyWriteRule.Decide("PEN", "PEN", registro));
    }

    // ── O-3 · Después del commit, avisar no cambia el resultado ───────────

    [Fact]
    public async Task Si_un_publicador_falla_despues_del_commit_no_sale_ninguna_excepcion()
    {
        var m = Servicio(bus: new BusQueFalla(new InvalidOperationException("manejador roto")));
        await using var _ = m.Contexto;

        await m.Servicio.AnnounceAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Si_la_cancelacion_llega_despues_del_commit_no_sale_ninguna_excepcion()
    {
        var m = Servicio(bus: new BusQueFalla(new OperationCanceledException("tarde")));
        await using var _ = m.Contexto;

        await m.Servicio.AnnounceAsync(new CancellationToken(canceled: true));
    }

    // ── La ruta genérica no la toca ────────────────────────────────────────

    [Theory]
    [InlineData("installation.currency_iso4217")]
    [InlineData("INSTALLATION.CURRENCY_ISO4217")]
    public async Task La_ruta_generica_de_configuracion_rechaza_la_clave_de_moneda_sin_tocar_la_base(string clave)
    {
        await using var contexto = Contexto(CadenaSinServidor);
        var services = new ServiceCollection().BuildServiceProvider();
        var configuracion = new SiteSettingService(
            contexto,
            new SettingsCache(services.GetRequiredService<IServiceScopeFactory>()),
            new AuditWriter(contexto, new HttpContextAccessor(), TimeProvider.System),
            new BusVacio(),
            TimeProvider.System);

        var r = await configuracion.UpdateAsync(clave, new UpdateSettingRequest("PEN", false), true, 1, "principal@ejemplo.test",
            TestContext.Current.CancellationToken);

        Assert.Equal(SettingOutcome.Forbidden, r.Outcome);
        Assert.Contains("super_admin", r.Error);
    }

    // ── Preparación de instalaciones ───────────────────────────────────────

    private static string SqlDeSubida()
        => Assert.IsType<SqlOperation>(Assert.Single(new CoreInstallationCurrencyMigracion().Subida())).Sql;

    [Fact]
    public void La_preparacion_es_idempotente_privada_y_sin_moneda()
    {
        var sql = SqlDeSubida();

        Assert.Contains("ON CONFLICT (setting_key) DO NOTHING", sql);
        Assert.Contains("'installation.currency_iso4217', 'PENDIENTE_DEFINIR', 'text'", sql);
        Assert.Contains("SET is_public = false", sql);
        Assert.DoesNotContain("currency_code", sql);
        Assert.DoesNotContain("'PEN'", sql);
    }

    [Fact]
    public void Bajar_la_preparacion_no_borra_la_clave()
        => Assert.Empty(new CoreInstallationCurrencyMigracion().Bajada());

    [Fact]
    public void La_semilla_prepara_exactamente_la_misma_fila_que_la_migracion()
    {
        var semilla = File.ReadAllText(Path.Combine(RaizDelRepositorio(), "database", "modules", "core", "02_seed.sql"));

        Assert.Equal(Fila(SqlDeSubida()), Fila(semilla));
        Assert.Contains("WHERE setting_key = 'installation.currency_iso4217';", semilla);
    }

    private static string Fila(string sql)
        => Regex.Replace(
            Regex.Match(sql, @"\('installation\.currency_iso4217'[^)]*\)").Value,
            @"\s+", " ");

    [Fact]
    public void La_preparacion_no_cambia_el_modelo_de_CORE()
    {
        using var contexto = Contexto(CadenaSinServidor);

        Assert.False(contexto.Database.HasPendingModelChanges());
    }

    // ── Registro ───────────────────────────────────────────────────────────

    [Fact]
    public void CORE_registra_la_operacion_y_el_lector_pero_no_inventa_quien_informe_de_pedidos()
    {
        var servicios = new ServiceCollection();

        servicios.AddCoreEssentials(Configuracion(), CadenaSinServidor).AddCoreAuthentication();

        Assert.Contains(servicios, d => d.ServiceType == typeof(InstallationCurrencyService));
        Assert.Contains(servicios, d => d.ServiceType == typeof(IInstallationCurrencyAuthority));
        Assert.Contains(servicios, d => d.ServiceType == typeof(AuditWriter));
        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IMonetaryRecordsProbe));
    }

    [Fact]
    public void El_escritor_de_auditoria_publico_y_el_concreto_son_el_mismo_en_cada_peticion()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddCoreEssentials(Configuracion(), CadenaSinServidor);
        using var raiz = servicios.BuildServiceProvider();
        using var ambito = raiz.CreateScope();

        var concreto = ambito.ServiceProvider.GetRequiredService<AuditWriter>();

        Assert.Same(concreto, ambito.ServiceProvider.GetRequiredService<IAuditWriter>());
        Assert.Same(ambito.ServiceProvider.GetRequiredService<CoreDbContext>(), concreto.Context);
    }

    // ── Dobles ─────────────────────────────────────────────────────────────

    private static string RaizDelRepositorio()
    {
        for (var directorio = new DirectoryInfo(AppContext.BaseDirectory); directorio is not null; directorio = directorio.Parent)
        {
            if (Directory.Exists(Path.Combine(directorio.FullName, "database", "modules", "core")))
            {
                return directorio.FullName;
            }
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }

    private sealed class CoreInstallationCurrencyMigracion : CoreInstallationCurrency
    {
        public IReadOnlyList<MigrationOperation> Subida() => UpOperations;
        public IReadOnlyList<MigrationOperation> Bajada() => DownOperations;
    }

    private sealed class SondaFalsa(MonetaryRecordsState estado, bool falla = false) : IMonetaryRecordsProbe
    {
        public int Llamadas { get; private set; }

        public Task<MonetaryRecordsState> ProbeAsync(CancellationToken cancellationToken)
        {
            Llamadas++;
            return falla ? throw new InvalidOperationException("ventas caído") : Task.FromResult(estado);
        }
    }

    private sealed class BusQueFalla(Exception excepcion) : IEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken) where TEvent : notnull
            => throw excepcion;
    }

    private sealed class BusVacio : IEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken) where TEvent : notnull
            => Task.CompletedTask;
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora.ToUniversalTime();
    }
}
