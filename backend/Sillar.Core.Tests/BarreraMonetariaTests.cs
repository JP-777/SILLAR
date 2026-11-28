using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sillar.Core.Auditing;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Core.Services;
using Sillar.Core.Settings;
using Sillar.Shared.Events;

namespace Sillar.Core.Tests;

/// <summary>
/// E5.2 — barrera transaccional de la moneda: identidad del bloqueo, orden de
/// adquisición, sonda bajo el bloqueo, cierre ante la duda, cancelación y vuelta atrás.
/// </summary>
/// <remarks>
/// Sin PostgreSQL: el orden se observa con <see cref="BaseGuionada"/>. La exclusión
/// mutua real entre dos conexiones queda pendiente de validación física.
/// </remarks>
public sealed class BarreraMonetariaTests
{
    // ── Identidad del bloqueo ──────────────────────────────────────────────

    [Fact]
    public void La_identidad_es_fija_y_de_dos_claves_SILL_1()
    {
        Assert.Equal(0x53494C4C, MonetaryAuthorityLock.Namespace);
        Assert.Equal("SILL", Encoding.ASCII.GetString(BitConverter.GetBytes(MonetaryAuthorityLock.Namespace).Reverse().ToArray()));
        Assert.Equal(1, MonetaryAuthorityLock.CurrencyAuthority);
    }

    [Fact]
    public void Exclusivo_y_compartido_son_el_mismo_bloqueo_de_transaccion_en_dos_modos()
    {
        var exclusivo = MonetaryAuthorityLock.AcquireExclusive();
        var compartido = MonetaryAuthorityLock.AcquireShared();

        Assert.Equal("SELECT pg_advisory_xact_lock({0}, {1})", exclusivo.Format);
        Assert.Equal("SELECT pg_advisory_xact_lock_shared({0}, {1})", compartido.Format);
        Assert.Equal(new object[] { 0x53494C4C, 1 }, exclusivo.GetArguments());
        Assert.Equal(exclusivo.GetArguments(), compartido.GetArguments());
        Assert.All(new[] { exclusivo.GetArgument(0), exclusivo.GetArgument(1) }, a => Assert.IsType<int>(a));
    }

    [Fact]
    public void Los_demas_bloqueos_consultivos_del_producto_son_de_una_clave_bigint_y_no_pueden_chocar()
    {
        // PostgreSQL no mezcla el espacio de una clave bigint con el de dos claves int4.
        Assert.Equal(typeof(long), typeof(ModuleActivationService).GetField(nameof(ModuleActivationService.ActivationLockKey))!.FieldType);
        Assert.IsType<long>((object)InstallationTimeZoneService.ChangeLock);
        Assert.NotEqual(ModuleActivationService.ActivationLockKey, InstallationTimeZoneService.ChangeLock);

        var backend = Path.Combine(RaizDelRepositorio(), "backend");
        var usos = Directory.EnumerateFiles(backend, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains(".Tests" + Path.DirectorySeparatorChar))
            .Where(f => File.ReadAllText(f).Contains("pg_advisory"))
            .Select(Path.GetFileName)
            .Order()
            .ToList();

        Assert.Equal(
            // El SQL del bloqueo de moneda solo vive en el contrato: CORE lo toma de ahí.
            ["InstallationTimeZoneService.cs", "ModuleActivationService.cs", "ModuleLicensing.cs", "MonetaryAuthorityLock.cs",
             "ServiceOrderApplicationService.cs", "TrackingApplicationService.cs"],
            usos);

        foreach (var archivo in new[] { "ServiceOrderApplicationService.cs", "TrackingApplicationService.cs" })
        {
            var ruta = Directory.EnumerateFiles(backend, archivo, SearchOption.AllDirectories).Single(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
            Assert.Contains("hashtextextended", File.ReadAllText(ruta));
        }
    }

    // ── Orden de adquisición ───────────────────────────────────────────────

    [Fact]
    public async Task El_orden_es_transaccion_espera_bloqueo_exclusivo_sonda_y_despues_la_fila()
    {
        var (m, base_) = Montar(MonetaryRecordsState.None);
        await using var _ = m.Contexto;

        var r = await Establecer(m);

        // Ninguna fila preparada en la base guionada: termina en NotPrepared, deshaciendo.
        Assert.Equal(CurrencyChangeOutcome.NotPrepared, r.Outcome);
        Assert.Equal("BEGIN", base_.Pasos[0]);
        Assert.Equal(InstallationCurrencyService.LockTimeoutSql, base_.Pasos[1]);
        Assert.StartsWith("SELECT pg_advisory_xact_lock(@p0, @p1)", base_.Pasos[2]);
        Assert.Equal("SONDA", base_.Pasos[3]);
        Assert.Contains("FOR UPDATE", base_.Pasos[4]);
        Assert.Contains("site_settings", base_.Pasos[5]);
        Assert.Equal("ROLLBACK", base_.Pasos[^1]);
        Assert.DoesNotContain("COMMIT", base_.Pasos);
    }

    [Fact]
    public async Task El_bloqueo_se_pide_una_sola_vez_y_en_modo_exclusivo()
    {
        var (m, base_) = Montar(MonetaryRecordsState.None);
        await using var _ = m.Contexto;

        await Establecer(m);

        Assert.Single(base_.Pasos, p => p.Contains("pg_advisory"));
        Assert.DoesNotContain(base_.Pasos, p => p.Contains("pg_advisory_xact_lock_shared"));
        Assert.DoesNotContain(base_.Pasos, p => p.Contains("pg_advisory_lock"));
    }

    // ── Cierre ante la duda, bajo el bloqueo ───────────────────────────────

    [Fact]
    public async Task Con_pedidos_se_rechaza_bajo_el_bloqueo_tras_mirar_la_fila_y_sin_escribir()
    {
        // Desde la O-2 ratificada, con pedidos hay que mirar la fila: si ya es esa
        // moneda y está acreditada, la respuesta es Unchanged. Aquí no hay fila, así
        // que habría que escribir, y con pedidos no se escribe.
        var (m, base_) = Montar(MonetaryRecordsState.Present);
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.RecordsPresent, (await Establecer(m)).Outcome);
        Assert.Equal(["BEGIN", InstallationCurrencyService.LockTimeoutSql], base_.Pasos.Take(2));
        Assert.Equal("SONDA", base_.Pasos[3]);
        Assert.Contains("FOR UPDATE", base_.Pasos[4]);
        Assert.Contains("FROM core.site_settings", base_.Pasos[5]);
        Assert.Equal("ROLLBACK", base_.Pasos[^1]);
        Assert.DoesNotContain(base_.Pasos, p => p.Contains("UPDATE core.") || p.Contains("INSERT"));
        Assert.DoesNotContain("COMMIT", base_.Pasos);
    }

    [Fact]
    public async Task Si_ventas_no_sabe_si_hay_pedidos_se_rechaza_bajo_el_bloqueo()
    {
        var (m, base_) = Montar(MonetaryRecordsState.Unknown);
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.RecordsUnverifiable, (await Establecer(m)).Outcome);
        Assert.Equal(["SONDA", "ROLLBACK"], base_.Pasos.Skip(3));
    }

    [Fact]
    public async Task Si_la_sonda_falla_se_rechaza_bajo_el_bloqueo()
    {
        var (m, base_) = Montar(MonetaryRecordsState.None, sondaFalla: true);
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.RecordsUnverifiable, (await Establecer(m)).Outcome);
        Assert.Equal(["SONDA", "ROLLBACK"], base_.Pasos.Skip(3));
    }

    [Fact]
    public async Task Sin_sonda_registrada_se_rechaza_sin_abrir_transaccion_ni_pedir_el_bloqueo()
    {
        var (m, base_) = Montar(null);
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.RecordsUnverifiable, (await Establecer(m)).Outcome);
        Assert.Empty(base_.Pasos);
    }

    // ── Bloqueo ocupado ────────────────────────────────────────────────────

    [Fact]
    public async Task Si_una_venta_retiene_el_bloqueo_mas_de_lo_permitido_se_rechaza_sin_consultar_la_sonda()
    {
        var (m, base_) = Montar(MonetaryRecordsState.None);
        await using var _ = m.Contexto;
        base_.FallaAlEjecutar = sql => sql.Contains("pg_advisory")
            ? new PostgresException("canceling statement due to lock timeout", "ERROR", "ERROR", PostgresErrorCodes.LockNotAvailable)
            : null;

        var r = await Establecer(m);

        Assert.Equal(CurrencyChangeOutcome.MonetaryLockBusy, r.Outcome);
        Assert.DoesNotContain("SONDA", base_.Pasos);
        Assert.Equal("ROLLBACK", base_.Pasos[^1]);
    }

    // ── Cancelación y vuelta atrás ─────────────────────────────────────────

    [Fact]
    public async Task Si_el_llamador_cancela_esperando_el_bloqueo_se_propaga_y_se_deshace()
    {
        var (m, base_) = Montar(MonetaryRecordsState.None);
        await using var _ = m.Contexto;
        using var origen = new CancellationTokenSource();
        base_.FallaAlEjecutar = sql =>
        {
            if (!sql.Contains("pg_advisory"))
            {
                return null;
            }

            origen.Cancel();
            return new OperationCanceledException(origen.Token);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Establecer(m, origen.Token));
        Assert.DoesNotContain("SONDA", base_.Pasos);
        Assert.Equal("ROLLBACK", base_.Pasos[^1]);
    }

    [Fact]
    public async Task Si_el_llamador_cancela_durante_la_sonda_se_propaga_y_se_deshace()
    {
        using var origen = new CancellationTokenSource();
        var (m, base_) = Montar(MonetaryRecordsState.None, cancelarEnLaSonda: origen);
        await using var _ = m.Contexto;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Establecer(m, origen.Token));
        Assert.DoesNotContain(base_.Pasos, p => p.Contains("FOR UPDATE"));
        Assert.Equal("ROLLBACK", base_.Pasos[^1]);
    }

    [Fact]
    public async Task Si_falla_el_bloqueo_de_la_fila_no_se_guarda_nada_y_no_queda_nada_pendiente()
    {
        var (m, base_) = Montar(MonetaryRecordsState.None);
        await using var _ = m.Contexto;
        base_.FallaAlEjecutar = sql => sql.Contains("FOR UPDATE") ? new InvalidOperationException("conexión cortada") : null;

        var r = await Establecer(m);

        Assert.Equal(CurrencyChangeOutcome.NotSaved, r.Outcome);
        Assert.Equal("ROLLBACK", base_.Pasos[^1]);
        Assert.DoesNotContain("COMMIT", base_.Pasos);
        Assert.Empty(m.Contexto.ChangeTracker.Entries<AuditLog>());
        Assert.Empty(m.Contexto.ChangeTracker.Entries<SiteSetting>());
    }

    // ── Lo que no cambia de E5.1 ──────────────────────────────────────────

    [Fact]
    public async Task Antes_de_la_barrera_siguen_las_negativas_de_rol_valor_y_auditoria_sin_tocar_la_base()
    {
        var (m, base_) = Montar(MonetaryRecordsState.None);
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.Forbidden, (await Establecer(m, superAdmin: false)).Outcome);
        Assert.Equal(CurrencyChangeOutcome.Invalid, (await Establecer(m, valor: "USD")).Outcome);
        Assert.Empty(base_.Pasos);

        await using var otro = new BaseGuionada().Contexto();
        var (ajeno, baseAjena) = Montar(MonetaryRecordsState.None, contextoDelEscritor: otro);
        await using var __ = ajeno.Contexto;
        Assert.Equal(CurrencyChangeOutcome.AuditNotAtomic, (await Establecer(ajeno)).Outcome);
        Assert.Empty(baseAjena.Pasos);
    }

    [Fact]
    public void La_ruta_de_escritura_sigue_eligiendo_igual_que_en_E5_1()
    {
        var registro = InstallationCurrencyAuditText.Compose("PENDIENTE_DEFINIR", "PEN");

        Assert.Equal(CurrencyWriteDecision.Unchanged, InstallationCurrencyWriteRule.Decide("PEN", "PEN", registro));
        Assert.Equal(CurrencyWriteDecision.Reattest, InstallationCurrencyWriteRule.Decide("PEN", "PEN", null));
        Assert.Equal(CurrencyWriteDecision.Change, InstallationCurrencyWriteRule.Decide("PENDIENTE_DEFINIR", "PEN", null));
        Assert.True(InstallationCurrencyEvaluator.Evaluate(true, "PEN", registro, DateTimeOffset.UnixEpoch).IsAccredited);
        Assert.Equal(InstallationCurrencyStatus.NotAttested, InstallationCurrencyEvaluator.Evaluate(true, "PEN", null, DateTimeOffset.UnixEpoch).Status);
    }

    // ── Montaje ────────────────────────────────────────────────────────────

    private sealed record Montaje(InstallationCurrencyService Servicio, CoreDbContext Contexto);

    private static (Montaje, BaseGuionada) Montar(
        MonetaryRecordsState? estadoDeLaSonda,
        bool sondaFalla = false,
        CancellationTokenSource? cancelarEnLaSonda = null,
        CoreDbContext? contextoDelEscritor = null)
    {
        var base_ = new BaseGuionada();
        var contexto = base_.Contexto();
        var servicios = new ServiceCollection();
        if (estadoDeLaSonda is { } estado)
        {
            servicios.AddSingleton<IMonetaryRecordsProbe>(new SondaQueAnota(base_, estado, sondaFalla, cancelarEnLaSonda));
        }

        var proveedor = servicios.BuildServiceProvider();
        var servicio = new InstallationCurrencyService(
            contexto,
            new AuditWriter(contextoDelEscritor ?? contexto, new HttpContextAccessor(), TimeProvider.System),
            new SettingsCache(proveedor.GetRequiredService<IServiceScopeFactory>()),
            new BusVacio(),
            proveedor,
            TimeProvider.System,
            NullLogger<InstallationCurrencyService>.Instance);
        return (new Montaje(servicio, contexto), base_);
    }

    private static Task<CurrencyChangeResult> Establecer(Montaje m, CancellationToken? ct = null, bool superAdmin = true, string valor = "PEN")
        => m.Servicio.SetAsync(valor, superAdmin, 1, "principal@ejemplo.test", ct ?? TestContext.Current.CancellationToken);

    /// <summary>Anota cuándo se la consulta. Nunca pide el bloqueo: es la regla del contrato.</summary>
    private sealed class SondaQueAnota(BaseGuionada base_, MonetaryRecordsState estado, bool falla, CancellationTokenSource? cancelar)
        : IMonetaryRecordsProbe
    {
        public Task<MonetaryRecordsState> ProbeAsync(CancellationToken cancellationToken)
        {
            base_.Pasos.Add("SONDA");
            if (cancelar is not null)
            {
                cancelar.Cancel();
                throw new OperationCanceledException(cancelar.Token);
            }

            return falla ? throw new InvalidOperationException("ventas caído") : Task.FromResult(estado);
        }
    }

    private sealed class BusVacio : IEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken) where TEvent : notnull
            => Task.CompletedTask;
    }

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
}
