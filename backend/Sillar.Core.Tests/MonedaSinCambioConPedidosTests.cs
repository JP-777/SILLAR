using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sillar.Core.Auditing;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Core.Settings;
using Sillar.Shared.Events;

namespace Sillar.Core.Tests;

/// <summary>
/// E9 · O-2 ratificada — pedir la moneda que ya está acreditada responde
/// <c>Unchanged</c> aunque haya pedidos, bajo la barrera y sin escribir nada. Todo lo
/// que escribiría sigue rechazado con pedidos.
/// </summary>
/// <remarks>
/// Sin PostgreSQL: <see cref="BaseGuionada"/> devuelve la fila de la moneda y el último
/// comprobante, y anota el orden. La exclusión mutua real queda para la prueba física
/// (<c>ENTREGA-07</c> §14).
/// </remarks>
public sealed class MonedaSinCambioConPedidosTests
{
    private const string Clave = "installation.currency_iso4217";

    private static readonly string ComprobanteDePen = InstallationCurrencyAuditText.Compose("PENDIENTE_DEFINIR", "PEN");

    // ── Unchanged con pedidos ──────────────────────────────────────────────

    [Fact]
    public async Task Con_pedidos_pedir_la_moneda_acreditada_responde_sin_cambios()
    {
        var m = Montar(MonetaryRecordsState.Present, "PEN", ComprobanteDePen);
        await using var _ = m.Contexto;

        var r = await Establecer(m);

        Assert.Equal(CurrencyChangeOutcome.Unchanged, r.Outcome);
        Assert.Equal("PEN", r.Currency?.Code);
        Assert.Null(r.Message);
        NadaEscrito(m);
    }

    [Fact]
    public async Task Sin_cambios_se_decide_bajo_el_bloqueo_con_la_fila_releida_y_su_comprobante()
    {
        var m = Montar(MonetaryRecordsState.Present, "PEN", ComprobanteDePen);
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.Unchanged, (await Establecer(m)).Outcome);

        var pasos = m.Base.Pasos;
        Assert.Equal("BEGIN", pasos[0]);
        Assert.Equal(InstallationCurrencyService.LockTimeoutSql, pasos[1]);
        Assert.StartsWith("SELECT pg_advisory_xact_lock(@p0, @p1)", pasos[2]);
        Assert.Equal("SONDA", pasos[3]);
        Assert.Contains("FOR UPDATE", pasos[4]);

        // La fila y el comprobante se leen después del bloqueo y del FOR UPDATE.
        var lecturasDeFila = Indices(pasos, p => p.Contains("FROM core.site_settings") && !p.Contains("FOR UPDATE"));
        var lecturaDelComprobante = pasos.FindIndex(p => p.Contains("FROM core.audit_log"));
        Assert.Equal(2, lecturasDeFila.Count); // la consulta y su relectura
        Assert.All(lecturasDeFila, i => Assert.True(i > 4));
        Assert.True(lecturaDelComprobante > lecturasDeFila[^1]);
        Assert.Equal("ROLLBACK", pasos[^1]);
    }

    [Fact]
    public async Task Sin_cambios_no_acredita_de_nuevo_ni_avisa_ni_toca_la_fila()
    {
        var m = Montar(MonetaryRecordsState.Present, "PEN", ComprobanteDePen);
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.Unchanged, (await Establecer(m)).Outcome);

        Assert.Empty(m.Contexto.ChangeTracker.Entries<AuditLog>());
        Assert.Equal(0, m.Bus.Avisos);
        var fila = Assert.Single(m.Contexto.ChangeTracker.Entries<SiteSetting>());
        Assert.Equal(EntityState.Unchanged, fila.State);
        Assert.Equal("PEN", fila.Entity.SettingValue);
    }

    [Fact]
    public async Task Sin_pedidos_la_moneda_acreditada_tambien_responde_sin_cambios()
    {
        var m = Montar(MonetaryRecordsState.None, "PEN", ComprobanteDePen);
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.Unchanged, (await Establecer(m)).Outcome);
        NadaEscrito(m);
    }

    // ── 409 con pedidos: todo lo que escribiría ───────────────────────────

    public static TheoryData<string, string, string?> QueEscribirian => new()
    {
        { "el valor guardado no tiene comprobante", "PEN", null },
        { "el comprobante dice otra moneda", "PEN", InstallationCurrencyAuditText.Compose("PEN", "USD") },
        { "el comprobante no tiene la forma exacta", "PEN", "Moneda de la instalación: anterior=; nueva=PEN; lista=sillar-iso4217-v1." },
        { "el comprobante inventa el valor anterior", "PEN", "Moneda de la instalación: anterior=pen; nueva=PEN; lista=sillar-iso4217-v1." },
        { "el comprobante lleva texto de más", "PEN", ComprobanteDePen + " " },
        { "la moneda está pendiente", "PENDIENTE_DEFINIR", null },
        { "hay otra moneda acreditada", "USD", InstallationCurrencyAuditText.Compose("PENDIENTE_DEFINIR", "USD") },
        { "el valor guardado difiere en mayúsculas", "pen", ComprobanteDePen },
        { "el valor guardado lleva un espacio", "PEN ", ComprobanteDePen },
    };

    [Theory]
    [MemberData(nameof(QueEscribirian))]
    public async Task Con_pedidos_lo_que_cambiaria_o_acreditaria_la_moneda_se_rechaza(string caso, string guardado, string? comprobante)
    {
        var m = Montar(MonetaryRecordsState.Present, guardado, comprobante);
        await using var _ = m.Contexto;

        var r = await Establecer(m);

        Assert.True(r.Outcome == CurrencyChangeOutcome.RecordsPresent, $"{caso}: {r.Outcome}");
        Assert.Contains("pedidos", r.Message);
        NadaEscrito(m);
        Assert.All(m.Contexto.ChangeTracker.Entries<SiteSetting>(), e =>
        {
            Assert.Equal(EntityState.Unchanged, e.State);
            Assert.Equal(guardado, e.Entity.SettingValue);
        });
    }

    // ── Sin respuesta firme de Ventas no hay Unchanged ─────────────────────

    [Fact]
    public async Task Si_ventas_no_sabe_si_hay_pedidos_no_se_responde_sin_cambios_ni_se_mira_la_fila()
    {
        var m = Montar(MonetaryRecordsState.Unknown, "PEN", ComprobanteDePen);
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.RecordsUnverifiable, (await Establecer(m)).Outcome);
        Assert.Equal(["SONDA", "ROLLBACK"], m.Base.Pasos.Skip(3));
    }

    [Fact]
    public async Task Sin_sonda_no_se_responde_sin_cambios_ni_se_toca_la_base()
    {
        var m = Montar(null, "PEN", ComprobanteDePen);
        await using var _ = m.Contexto;

        Assert.Equal(CurrencyChangeOutcome.RecordsUnverifiable, (await Establecer(m)).Outcome);
        Assert.Empty(m.Base.Pasos);
    }

    // ── Idempotencia ───────────────────────────────────────────────────────

    [Fact]
    public async Task Pedir_la_misma_moneda_varias_veces_con_pedidos_responde_siempre_igual_y_nunca_escribe()
    {
        var m = Montar(MonetaryRecordsState.Present, "PEN", ComprobanteDePen);
        await using var _ = m.Contexto;

        var resultados = new List<CurrencyChangeOutcome>();
        for (var vez = 0; vez < 3; vez++)
        {
            resultados.Add((await Establecer(m)).Outcome);
        }

        Assert.All(resultados, r => Assert.Equal(CurrencyChangeOutcome.Unchanged, r));
        Assert.Equal(3, m.Base.Pasos.Count(p => p == "BEGIN"));
        Assert.Equal(3, m.Base.Pasos.Count(p => p == "ROLLBACK"));
        NadaEscrito(m);
    }

    // ── Concurrencia: lo que decide es lo que hay bajo el bloqueo ──────────

    [Fact]
    public async Task Si_una_venta_retiene_el_bloqueo_no_se_responde_sin_cambios_por_adelantado()
    {
        var m = Montar(MonetaryRecordsState.Present, "PEN", ComprobanteDePen);
        await using var _ = m.Contexto;
        m.Base.FallaAlEjecutar = sql => sql.Contains("pg_advisory")
            ? new PostgresException("canceling statement due to lock timeout", "ERROR", "ERROR", PostgresErrorCodes.LockNotAvailable)
            : null;

        Assert.Equal(CurrencyChangeOutcome.MonetaryLockBusy, (await Establecer(m)).Outcome);
        Assert.DoesNotContain("SONDA", m.Base.Pasos);
        Assert.DoesNotContain(m.Base.Pasos, p => p.Contains("site_settings") || p.Contains("audit_log"));
        NadaEscrito(m);
    }

    [Fact]
    public async Task Una_vista_vieja_en_el_contexto_no_da_un_sin_cambios_falso()
    {
        // El contexto cree que ya es PEN; bajo el bloqueo la base dice pendiente.
        var m = Montar(MonetaryRecordsState.Present, "PENDIENTE_DEFINIR", ComprobanteDePen);
        await using var _ = m.Contexto;
        m.Contexto.SiteSettings.Attach(new SiteSetting { SiteSettingId = 15, SettingKey = Clave, SettingValue = "PEN", ValueType = "text" });

        Assert.Equal(CurrencyChangeOutcome.RecordsPresent, (await Establecer(m)).Outcome);
        Assert.Equal("PENDIENTE_DEFINIR", Assert.Single(m.Contexto.ChangeTracker.Entries<SiteSetting>()).Entity.SettingValue);
        NadaEscrito(m);
    }

    [Fact]
    public async Task Si_otro_super_admin_acaba_de_acreditarla_se_responde_sin_cambios()
    {
        // Dos CORE a la vez: el segundo esperó el bloqueo y lee lo que el primero confirmó.
        var m = Montar(MonetaryRecordsState.Present, "PEN", ComprobanteDePen);
        await using var _ = m.Contexto;
        m.Contexto.SiteSettings.Attach(new SiteSetting { SiteSettingId = 15, SettingKey = Clave, SettingValue = "PENDIENTE_DEFINIR", ValueType = "text" });

        Assert.Equal(CurrencyChangeOutcome.Unchanged, (await Establecer(m)).Outcome);
        Assert.Equal("PEN", Assert.Single(m.Contexto.ChangeTracker.Entries<SiteSetting>()).Entity.SettingValue);
        NadaEscrito(m);
    }

    [Fact]
    public async Task Si_no_se_puede_leer_el_comprobante_no_se_responde_sin_cambios()
    {
        var m = Montar(MonetaryRecordsState.Present, "PEN", ComprobanteDePen);
        await using var _ = m.Contexto;
        m.Base.FallaAlEjecutar = sql => sql.Contains("FROM core.audit_log") ? new InvalidOperationException("conexión cortada") : null;

        Assert.Equal(CurrencyChangeOutcome.NotSaved, (await Establecer(m)).Outcome);
        Assert.Equal("ROLLBACK", m.Base.Pasos[^1]);
        NadaEscrito(m);
    }

    // ── Montaje ────────────────────────────────────────────────────────────

    private sealed record Montaje(InstallationCurrencyService Servicio, CoreDbContext Contexto, BaseGuionada Base, BusQueCuenta Bus);

    private static Montaje Montar(MonetaryRecordsState? estadoDeLaSonda, string? guardado, string? comprobante)
    {
        var base_ = new BaseGuionada();
        base_.FilasAlLeer = sql =>
            sql.Contains("FROM core.site_settings") && guardado is not null ? BaseGuionada.FilaDeAjuste(Clave, guardado)
            : sql.Contains("FROM core.audit_log") ? BaseGuionada.Texto(comprobante)
            : null;
        var contexto = base_.Contexto();
        var servicios = new ServiceCollection();
        if (estadoDeLaSonda is { } estado)
        {
            servicios.AddSingleton<IMonetaryRecordsProbe>(new SondaQueAnota(base_, estado));
        }

        var proveedor = servicios.BuildServiceProvider();
        var bus = new BusQueCuenta();
        var servicio = new InstallationCurrencyService(
            contexto,
            new AuditWriter(contexto, new HttpContextAccessor(), TimeProvider.System),
            new SettingsCache(proveedor.GetRequiredService<IServiceScopeFactory>()),
            bus,
            proveedor,
            TimeProvider.System,
            NullLogger<InstallationCurrencyService>.Instance);
        return new Montaje(servicio, contexto, base_, bus);
    }

    private static Task<CurrencyChangeResult> Establecer(Montaje m)
        => m.Servicio.SetAsync("PEN", true, 1, "principal@ejemplo.test", TestContext.Current.CancellationToken);

    /// <summary>Ni sentencia de escritura, ni commit, ni auditoría preparada, ni aviso.</summary>
    private static void NadaEscrito(Montaje m)
    {
        Assert.DoesNotContain(m.Base.Pasos, p => p.Contains("UPDATE core.") || p.Contains("INSERT") || p.Contains("DELETE"));
        Assert.DoesNotContain("COMMIT", m.Base.Pasos);
        Assert.Empty(m.Contexto.ChangeTracker.Entries<AuditLog>());
        Assert.Equal(0, m.Bus.Avisos);
    }

    private static List<int> Indices(List<string> pasos, Func<string, bool> condicion)
        => pasos.Select((p, i) => (p, i)).Where(x => condicion(x.p)).Select(x => x.i).ToList();

    private sealed class SondaQueAnota(BaseGuionada base_, MonetaryRecordsState estado) : IMonetaryRecordsProbe
    {
        public Task<MonetaryRecordsState> ProbeAsync(CancellationToken cancellationToken)
        {
            base_.Pasos.Add("SONDA");
            return Task.FromResult(estado);
        }
    }

    private sealed class BusQueCuenta : IEventPublisher
    {
        public int Avisos { get; private set; }

        public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken) where TEvent : notnull
        {
            Avisos++;
            return Task.CompletedTask;
        }
    }
}
