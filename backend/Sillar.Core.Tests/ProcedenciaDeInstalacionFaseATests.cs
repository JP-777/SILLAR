using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sillar.Core.Authorization;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Shared.Replication;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// Procedencia de la instalación (Fase A, candidato): se emite con todos los
/// hechos o no se emite.
/// </summary>
/// <remarks>
/// Lógica pura salvo el productor, que usa un puerto cerrado: la conexión se
/// rechaza antes de llegar a ningún servidor. Sin PostgreSQL.
/// </remarks>
public sealed class ProcedenciaDeInstalacionFaseATests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 24, 9, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid Clave = Guid.Parse("01920000-0000-7000-8000-000000000001");
    private const string Version = "1.4.0";

    private static InstallationProvenanceRow Fila(Guid? clave = null, bool completa = true)
        => new(clave ?? Clave, completa, "1.3.0");

    private static InstallationProvenanceResult Evaluar(
        IReadOnlyList<InstallationProvenanceRow> filas,
        string? configurado = "sur",
        string estampado = "sur")
        => InstallationProvenanceEvaluator.Evaluate(filas, configurado, estampado, Version, Ahora);

    [Fact]
    public void Con_todos_los_hechos_se_emite_la_procedencia_completa()
    {
        var resultado = Evaluar([Fila()]);

        Assert.True(resultado.IsObserved);
        Assert.Null(resultado.Failure);
        Assert.Equal(Ahora, resultado.ObservedAt);
        Assert.Equal(
            new InstallationProvenance(Clave, "sur", "1.3.0", Version, InstallationProvenanceEvaluator.Source),
            resultado.Provenance);
    }

    [Fact]
    public void Sin_instalacion_no_hay_procedencia()
        => SinProcedencia(ProvenanceFailure.InstallationMissing, Evaluar([]));

    [Fact]
    public void Con_clave_vacia_no_hay_procedencia()
        => SinProcedencia(ProvenanceFailure.InstallationIdentityInvalid, Evaluar([Fila(Guid.Empty)]));

    [Fact]
    public void Con_dos_instalaciones_no_se_elige_ninguna()
        => SinProcedencia(ProvenanceFailure.InstallationIdentityInvalid, Evaluar([Fila(), Fila(Guid.Parse("01920000-0000-7000-8000-000000000002"))]));

    [Fact]
    public void Con_la_instalacion_a_medio_configurar_no_hay_procedencia()
        => SinProcedencia(ProvenanceFailure.InstallationIncomplete, Evaluar([Fila(completa: false)]));

    [Fact]
    public void El_nodo_por_defecto_no_cuenta_como_procedencia()
        // NodeIdentity estampa «principal» sin configuración. Eso no es un hecho.
        => SinProcedencia(ProvenanceFailure.NodeNotConfigured, Evaluar([Fila()], configurado: null, estampado: NodeIdentity.DefaultCode));

    [Fact]
    public void Si_el_nodo_configurado_no_es_el_que_se_estampa_no_hay_procedencia()
        => SinProcedencia(ProvenanceFailure.NodeInconsistent, Evaluar([Fila()], configurado: "sur", estampado: "norte"));

    private static void SinProcedencia(ProvenanceFailure esperado, InstallationProvenanceResult resultado)
    {
        Assert.False(resultado.IsObserved);
        Assert.Null(resultado.Provenance);
        Assert.Equal(esperado, resultado.Failure);
        Assert.Equal(Ahora, resultado.ObservedAt);
    }

    // ── Productor ──────────────────────────────────────────────────────────

    private const string CadenaSinServidor = "Host=127.0.0.1;Port=1;Database=ninguna;Username=nadie;Password=nada;Timeout=2";

    private static (InstallationProvenanceReader Lector, CoreDbContext Contexto) Productor()
    {
        var contexto = Contexto(CadenaSinServidor);
        var lector = new InstallationProvenanceReader(
            contexto,
            Configuracion("sur"),
            new NodeIdentity("sur"),
            new RelojFijo(Ahora),
            NullLogger<InstallationProvenanceReader>.Instance);
        return (lector, contexto);
    }

    [Fact]
    public async Task Si_la_base_no_responde_no_hay_procedencia()
    {
        var (lector, contexto) = Productor();
        await using var _ = contexto;

        var resultado = await lector.ReadAsync(TestContext.Current.CancellationToken);

        SinProcedencia(ProvenanceFailure.AuthorityUnavailable, resultado);
    }

    [Fact]
    public async Task Con_el_token_del_llamador_cancelado_se_propaga_la_cancelacion()
    {
        var (lector, contexto) = Productor();
        await using var _ = contexto;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => lector.ReadAsync(new CancellationToken(canceled: true)));
    }

    [Fact]
    public void La_lectura_es_una_sola_sentencia_acotada_a_dos_filas()
    {
        var (lector, contexto) = Productor();
        using var _ = contexto;

        var sql = lector.RowsQuery().ToQueryString();

        Assert.Contains("core.installation", sql);
        Assert.Contains("LIMIT", sql);
    }

    [Fact]
    public void CORE_no_registra_el_productor_de_procedencia_por_su_cuenta()
    {
        var servicios = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        servicios.AddCoreEssentials(Configuracion(), CadenaSinServidor).AddCoreAuthentication();

        Assert.DoesNotContain(servicios, d => d.ServiceType == typeof(IInstallationProvenance));
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora.ToUniversalTime();
    }
}
