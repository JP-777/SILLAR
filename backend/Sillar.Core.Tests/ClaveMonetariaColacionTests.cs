using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Core.Domain;
using Sillar.Core.Dtos;
using Sillar.Core.Services;
using Sillar.Core.Settings;
using Sillar.Shared.Events;

namespace Sillar.Core.Tests;

/// <summary>
/// E9 · O-1 — la ruta genérica de configuración no alcanza la clave monetaria con
/// ningún alias que la colación <c>core.es_ci</c> dé por igual.
/// </summary>
/// <remarks>
/// <para>
/// <c>core.es_ci</c> es ICU <c>es-PE-u-ks-level2</c>, no determinista: para ella son
/// iguales las mayúsculas, los anchos completos, las variantes de compatibilidad y
/// los caracteres ignorables. La base guionada hace de esa colación: ante cualquier
/// lectura de <c>core.site_settings</c> devuelve la fila canónica de la moneda, que
/// es lo que haría PostgreSQL con un alias. Así se prueba la barrera que importa
/// —la identidad de la fila devuelta—, no la colación misma.
/// </para>
/// <para>
/// Que ICU dé por iguales exactamente estos alias queda pendiente de prueba física
/// (<c>ENTREGA-07</c> §13).
/// </para>
/// </remarks>
public sealed class ClaveMonetariaColacionTests
{
    private const string Clave = "installation.currency_iso4217";

    public static TheoryData<string, string> Alias => new()
    {
        { "mayúsculas", "INSTALLATION.CURRENCY_ISO4217" },
        { "mayúscula inicial", "Installation.currency_iso4217" },
        { "guion suave U+00AD", "installation.curr­ency_iso4217" },
        { "espacio de anchura cero U+200B", "installation.currency​_iso4217" },
        { "unión de anchura cero U+200D", "‍installation.currency_iso4217" },
        { "no unión U+200C", "installation‌.currency_iso4217" },
        { "unión de palabras U+2060", "installation.currency_iso⁠4217" },
        { "marca de orden U+FEFF", "﻿installation.currency_iso4217" },
        { "selector de variante U+FE0F", "installation.currency_iso4217️" },
        { "i de ancho completo U+FF49", "ｉnstallation.currency_iso4217" },
        { "punto de ancho completo U+FF0E", "installation．currency_iso4217" },
        { "dígitos de ancho completo", "installation.currency_iso４２１７" },
        { "s larga U+017F", "inſtallation.currency_iſo4217" },
        { "guion bajo de ancho completo U+FF3F", "installation.currency＿iso4217" },
    };

    [Theory]
    [MemberData(nameof(Alias))]
    public async Task Ningun_alias_de_la_clave_monetaria_llega_a_escribir_por_la_ruta_generica(string caso, string alias)
    {
        var (servicio, base_, contexto, auditoria, bus) = Montar();
        await using var _ = contexto;

        var r = await servicio.UpdateAsync(alias, new UpdateSettingRequest("USD", true), true, 1, "principal@ejemplo.test",
            TestContext.Current.CancellationToken);

        Assert.True(r.Outcome == SettingOutcome.Forbidden, caso);
        Assert.Contains("super_admin", r.Error);

        // Sin escritura, sin auditoría, sin aviso, sin transacción y sin bloqueo comercial.
        Assert.DoesNotContain(base_.Pasos, p => p.Contains("UPDATE") || p.Contains("INSERT") || p.Contains("DELETE"));
        Assert.DoesNotContain(base_.Pasos, p => p.Contains("pg_advisory"));
        Assert.DoesNotContain("BEGIN", base_.Pasos);
        Assert.DoesNotContain("COMMIT", base_.Pasos);
        Assert.Equal(0, auditoria.Escrituras);
        Assert.Equal(0, bus.Avisos);
        Assert.Empty(contexto.ChangeTracker.Entries<AuditLog>());
        Assert.All(contexto.ChangeTracker.Entries<SiteSetting>(), e =>
        {
            Assert.Equal(EntityState.Unchanged, e.State);
            Assert.Equal("PEN", e.Entity.SettingValue);
            Assert.False(e.Entity.IsPublic);
        });
    }

    [Fact]
    public async Task Un_alias_que_supera_el_filtro_ordinal_se_detiene_en_la_fila_que_devuelve_la_base()
    {
        // El guion suave no lo quita OrdinalIgnoreCase: la petición llega a la base,
        // y es la identidad de la fila la que corta.
        var (servicio, base_, contexto, _, _) = Montar();
        await using var __ = contexto;

        var r = await servicio.UpdateAsync("installation.curr­ency_iso4217", new UpdateSettingRequest("USD", null), true, 1,
            "principal@ejemplo.test", TestContext.Current.CancellationToken);

        Assert.Equal(SettingOutcome.Forbidden, r.Outcome);
        Assert.Single(base_.Pasos);
        Assert.Contains("FROM core.site_settings", base_.Pasos[0]);
    }

    [Fact]
    public async Task La_clave_exacta_se_corta_antes_de_tocar_la_base()
    {
        var (servicio, base_, contexto, _, _) = Montar();
        await using var _ = contexto;

        var r = await servicio.UpdateAsync(Clave, new UpdateSettingRequest("USD", null), true, 1, "principal@ejemplo.test",
            TestContext.Current.CancellationToken);

        Assert.Equal(SettingOutcome.Forbidden, r.Outcome);
        Assert.Empty(base_.Pasos);
    }

    [Fact]
    public async Task Una_fila_que_no_es_la_moneda_sigue_su_camino_normal()
    {
        // La barrera mira la identidad de la fila, no la petición: business_name pasa
        // y llega a la validación de su valor.
        var (servicio, _, contexto, auditoria, _) = Montar(filaDevuelta: "business_name");
        await using var __ = contexto;

        var r = await servicio.UpdateAsync("business_name", new UpdateSettingRequest(null, null), true, 1, "principal@ejemplo.test",
            TestContext.Current.CancellationToken);

        Assert.Equal(SettingOutcome.Invalid, r.Outcome);
        Assert.Equal(0, auditoria.Escrituras);
    }

    // ── Montaje ────────────────────────────────────────────────────────────

    private static (SiteSettingService, BaseGuionada, Sillar.Core.Data.CoreDbContext, AuditoriaQueCuenta, BusQueCuenta) Montar(
        string filaDevuelta = Clave)
    {
        var base_ = new BaseGuionada
        {
            FilasAlLeer = sql => sql.Contains("FROM core.site_settings") ? Fila(filaDevuelta) : null,
        };
        var contexto = base_.Contexto();
        var auditoria = new AuditoriaQueCuenta();
        var bus = new BusQueCuenta();
        var proveedor = new ServiceCollection().BuildServiceProvider();
        var servicio = new SiteSettingService(
            contexto,
            new SettingsCache(proveedor.GetRequiredService<IServiceScopeFactory>()),
            auditoria,
            bus,
            TimeProvider.System);
        return (servicio, base_, contexto, auditoria, bus);
    }

    /// <summary>La fila canónica que devolvería PostgreSQL para la clave.</summary>
    private static DataTable Fila(string clave)
        => BaseGuionada.FilaDeAjuste(clave, clave == Clave ? "PEN" : "Mi negocio");

    private sealed class AuditoriaQueCuenta : IAuditWriter
    {
        public int Escrituras { get; private set; }

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
        {
            Escrituras++;
            return Task.CompletedTask;
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
