using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Core.Services;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// Zona horaria IANA candidata y acción de auditoría <c>export</c> (Fase A).
/// </summary>
/// <remarks>
/// Lógica pura. La zona usa la base de zonas del runtime; el modelo se compara
/// con la instantánea de migraciones sin abrir conexión.
/// </remarks>
public sealed class ZonaIanaYExportFaseATests
{
    // ── Zona IANA ──────────────────────────────────────────────────────────

    [Fact]
    public void Un_identificador_que_el_runtime_resuelve_queda_pendiente_de_canonizacion_con_los_dos_valores_por_separado()
    {
        var resultado = IanaZoneCandidate.Evaluate("America/Lima");

        Assert.Equal(IanaZoneOutcome.RuntimeResolvedCandidate, resultado.Outcome);
        Assert.Equal("America/Lima", resultado.OriginalValue);
        Assert.Equal("America/Lima", resultado.RuntimeId);
    }

    [Fact]
    public void Un_alias_se_resuelve_pero_no_se_da_por_canonico()
    {
        // Pacific/Samoa es un enlace de tzdata a Pacific/Pago_Pago.
        var resultado = IanaZoneCandidate.Evaluate("Pacific/Samoa");

        Assert.Equal(IanaZoneOutcome.RuntimeResolvedCandidate, resultado.Outcome);
        Assert.Equal("Pacific/Samoa", resultado.OriginalValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PENDIENTE_DEFINIR")]
    public void Sin_valor_o_con_el_marcador_de_la_semilla_la_zona_esta_ausente(string? valor)
    {
        var resultado = IanaZoneCandidate.Evaluate(valor);

        Assert.Equal(IanaZoneOutcome.Missing, resultado.Outcome);
        Assert.Equal(valor, resultado.OriginalValue);
        Assert.Null(resultado.RuntimeId);
    }

    [Theory]
    [InlineData("SA Pacific Standard Time")] // identificador de Windows
    [InlineData(" America/Lima")]
    [InlineData("America/Lima ")]
    [InlineData("   ")]                      // solo espacios: es un valor, no una ausencia
    [InlineData("Lima")]
    [InlineData("UTC-5")]
    [InlineData("America//Lima")]
    [InlineData("America/../Lima")]
    [InlineData("../../etc/passwd")]
    [InlineData(@"America\Lima")]
    [InlineData("America/Ciudad_Que_No_Existe")]
    [InlineData("pendiente_definir")]
    public void Un_valor_que_no_es_un_identificador_resoluble_es_invalido_y_conserva_lo_escrito(string valor)
    {
        var resultado = IanaZoneCandidate.Evaluate(valor);

        Assert.Equal(IanaZoneOutcome.Invalid, resultado.Outcome);
        Assert.Equal(valor, resultado.OriginalValue);
        Assert.Null(resultado.RuntimeId);
    }

    [Theory]
    [InlineData("America/Lima")]
    [InlineData("america/lima")]
    [InlineData("Pacific/Samoa")]
    [InlineData("Etc/UTC")]
    public void Ningun_valor_se_da_por_canonico_verificado_sin_una_regla_ratificada(string valor)
        // "america/lima" en Linux es inválido; en Windows la resolución pasa por
        // ICU y podría resolverse. En ningún caso llega a canónico verificado.
        => Assert.NotEqual(IanaZoneOutcome.IanaCanonicalVerified, IanaZoneCandidate.Evaluate(valor).Outcome);

    [Fact]
    public void La_lectura_pide_la_clave_candidata_y_ninguna_otra()
    {
        var lector = new LectorFijo(new() { [IanaZoneCandidate.SettingKey] = "America/Lima" });

        var resultado = IanaZoneCandidate.Read(lector);

        Assert.Equal(IanaZoneOutcome.RuntimeResolvedCandidate, resultado.Outcome);
        Assert.Equal(["installation.timezone_iana"], lector.Pedidas);
    }

    [Fact]
    public void El_marcador_de_ausencia_es_el_mismo_que_deja_la_semilla()
        => Assert.Equal(SiteSettingService.PendingMarker, IanaZoneCandidate.PendingMarker);

    private sealed class LectorFijo(Dictionary<string, string?> valores) : ISettingsReader
    {
        public List<string> Pedidas { get; } = [];

        public string? Get(string key)
        {
            Pedidas.Add(key);
            return valores.GetValueOrDefault(key);
        }

        public T? Get<T>(string key) => throw new NotSupportedException();

        public IReadOnlyDictionary<string, string> GetPublic() => throw new NotSupportedException();
    }

    // ── Auditoría: export ─────────────────────────────────────────────────

    [Fact]
    public void La_exportacion_es_una_accion_de_auditoria_admitida()
    {
        Assert.Equal("export", AuditAction.Export);
        Assert.Contains(AuditAction.Export, AuditAction.All);
        Assert.Equal(AuditAction.All.Length, AuditAction.All.Distinct().Count());
    }

    [Fact]
    public void El_modelo_de_CORE_no_tiene_cambios_sin_migracion()
    {
        // Sin CoreAuditExport, añadir la acción cambia el CHECK del modelo y
        // MigrateAsync se niega a arrancar. La comparación no abre conexión.
        using var contexto = Contexto("Host=127.0.0.1;Port=1;Database=ninguna;Username=nadie;Password=nada");

        Assert.False(contexto.Database.HasPendingModelChanges());
    }
}
