using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sillar.Modules.Payments;
using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;
using Sillar.Modules.Reports;
using Sillar.Modules.Sales;
using Sillar.Modules.Sales.Commercial;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Pedidos;
using static Sillar.Host.CommerceBridge.Tests.ComposicionVerticalTests;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>
/// R5.1: sabotajes de DI. Cada uno altera una fuente de registros, el destino previo o la
/// colección ya compuesta, y la barrera tiene que rechazarlo con una causa reconocible.
/// </summary>
public sealed class SabotajesDiTests
{
    private static readonly List<string> Registro = [];

    /// <summary>Un escritor de M03 que el inventario no conoce.</summary>
    private sealed class QuintoEscritorM03;

    /// <summary>Un puerto de efectos con capacidad positiva: nunca se invoca, solo se registra.</summary>
    private sealed class PuertoPositivo : IProviderCommercialEffectPort
    {
        public ValueTask<ProviderCommercialEffectResult> TryApplyAsync(ProviderCommercialEffectCandidate candidate, CancellationToken cancellationToken)
            => ValueTask.FromResult(ProviderCommercialEffectResult.Applied("efecto-prohibido", false));
    }

    private sealed class LectorFalso : IOrderAggregateReader
    {
        public Task<OrderAggregateSnapshot> ReadCreatedBetweenAsync(DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
            => throw new InvalidOperationException("nunca");
    }

    private sealed class EvaluacionFalsa : ICommercialReadOnlyEvaluationCandidate
    {
        public Task<CommercialReadOnlyEvaluationResult> EvaluateAsync(OrderCorrelationRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("nunca");
    }

    private sealed class PuertoM11Falso : IM03CommercialReadOnlyEvaluationPort
    {
        public ValueTask<M03CommercialReadOnlyEvaluationResult> EvaluateAsync(M03OrderCorrelationRequestSnapshot request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("nunca");
    }

    private sealed class CorrelacionFicticia : IProviderAuthorizedOrderCorrelationSource
    {
        public ValueTask<ProviderAuthorizedOrderCorrelation?> LoadAsync(Guid inboxEventId, PaymentIdempotencyKeys identity, CancellationToken cancellationToken)
            => throw new InvalidOperationException("nunca");
    }

    private static void RegistrarM03Real(IServiceCollection s, IConfiguration c)
    {
        new SalesModule().RegisterServices(s, c);
        s.AddCandidateSalesReadOnlyEvaluation();
        s.AddCandidateSalesMonetaryReaders();
    }

    private static IServiceCollection ComponerCon(Action<IServiceCollection, IConfiguration>? m03 = null, Action<IServiceCollection, IConfiguration>? m10 = null)
        => new ServiceCollection().AddNegativeCommerceIntegration(Config(), new Entorno(Environments.Development), m03, m10);

    private static void Rechazado(string sabotaje, Action accion, string causa)
    {
        var e = Assert.Throws<InvalidOperationException>(accion);
        Assert.Contains(causa, e.Message, StringComparison.Ordinal);
        lock (Registro)
        {
            Registro.Add($"{sabotaje} | RECHAZADO | {e.Message}");
            Escribir("sabotajes-di.txt", Registro.Order(StringComparer.Ordinal));
        }
    }

    private static void RechazadoAlComponer(string sabotaje, Func<IServiceCollection> componer, string causa)
    {
        IServiceCollection? destino = null;
        Rechazado(sabotaje, () => destino = componer(), causa);
        Assert.Null(destino);
    }

    // 1 · Un quinto escritor de M03 registrado por la fuente.
    [Fact]
    public void S01_Un_quinto_escritor_de_M03_en_la_fuente_rechaza_la_composicion()
        => RechazadoAlComponer("S01 quinto escritor M03 en SalesModule",
            () => ComponerCon((s, c) => { RegistrarM03Real(s, c); s.AddScoped<QuintoEscritorM03>(); }),
            "rechaza el inventario de M03");

    // 2 · Un escritor con otra interfaz: el aplicador comercial completo de M03.
    [Fact]
    public void S02_El_aplicador_comercial_de_M03_en_la_fuente_rechaza_la_composicion()
        => RechazadoAlComponer("S02 AddCandidateSalesCommerce en la fuente M03",
            () => ComponerCon((s, c) => { RegistrarM03Real(s, c); s.AddCandidateSalesCommerce(); }),
            "IExternalSettlementCandidate");

    [Fact]
    public void S02b_El_aplicador_comercial_añadido_despues_de_componer_impide_construir()
    {
        var s = Componer();
        s.AddCandidateSalesCommerce();
        Rechazado("S02b AddCandidateSalesCommerce tras componer", () => s.BuildNegativeServiceProvider(), "servicio prohibido");
    }

    // 2c · Un registro inesperado con una interfaz admitida, en la fuente y después.
    [Fact]
    public void S02c_Un_segundo_lector_de_M03_en_la_fuente_rechaza_la_composicion()
        => RechazadoAlComponer("S02c segundo IOrderAggregateReader en la fuente M03",
            () => ComponerCon((s, c) => { RegistrarM03Real(s, c); s.AddScoped<IOrderAggregateReader, LectorFalso>(); }),
            "Inesperados");

    [Fact]
    public void S02d_Sustituir_un_lector_admitido_despues_de_componer_impide_construir()
    {
        var s = Componer();
        s.RemoveAll<IOrderAggregateReader>();
        s.AddScoped<IOrderAggregateReader, LectorFalso>();
        Rechazado("S02d IOrderAggregateReader sustituido tras componer", () => s.BuildNegativeServiceProvider(), "IOrderAggregateReader");
    }

    [Fact]
    public void S02e_Una_fuente_M03_incompleta_rechaza_la_composicion()
        => RechazadoAlComponer("S02e fuente M03 sin lectores monetarios",
            () => ComponerCon((s, c) => { new SalesModule().RegisterServices(s, c); s.AddCandidateSalesReadOnlyEvaluation(); }),
            "Ausentes");

    [Fact]
    public void S02f_Un_registro_inesperado_en_M10_rechaza_la_composicion()
        => RechazadoAlComponer("S02f registro extra en ReportsModule",
            () => ComponerCon(null, (s, c) => { new ReportsModule().RegisterServices(s, c); s.AddScoped<QuintoEscritorM03>(); }),
            "rechaza el inventario de M10");

    [Fact]
    public void S02g_El_lector_de_valor_I02_añadido_despues_de_componer_impide_construir()
    {
        var s = Componer();
        s.AddCandidateSalesMonetaryReaders();
        Rechazado("S02g IOrderValueAggregateReader tras componer", () => s.BuildNegativeServiceProvider(), "servicio prohibido");
    }

    [Fact]
    public void S02h_Un_destino_con_registros_previos_de_M03_rechaza_la_composicion()
    {
        var s = new ServiceCollection();
        new SalesModule().RegisterServices(s, Config());
        var antes = s.Count;
        Rechazado("S02h SalesModule ya registrado en el destino",
            () => s.AddNegativeCommerceIntegration(Config(), new Entorno(Environments.Development)), "sin registros previos");
        Assert.Equal(antes, s.Count);
    }

    // 3 · Un puerto de efectos incompatible preinscrito.
    [Fact]
    public void S03_Un_puerto_de_efectos_preinscrito_rechaza_la_composicion()
    {
        var s = new ServiceCollection();
        s.AddScoped<IProviderCommercialEffectPort, PuertoPositivo>();
        Rechazado("S03 IProviderCommercialEffectPort positivo preinscrito",
            () => s.AddNegativeCommerceIntegration(Config(), new Entorno(Environments.Development)), "sin registros previos");
        Assert.Single(s);
    }

    // 4 · Duplicar o sustituir el adaptador negativo.
    [Fact]
    public void S04_Duplicar_el_adaptador_negativo_impide_construir()
    {
        var s = Componer();
        s.AddScoped<IProviderCommercialEffectPort, M03ReadOnlyNegativeEffectPortAdapter>();
        Rechazado("S04 adaptador negativo duplicado", () => s.BuildNegativeServiceProvider(), "IProviderCommercialEffectPort");
    }

    [Fact]
    public void S04b_Sustituir_el_adaptador_negativo_por_un_puerto_positivo_impide_construir()
    {
        var s = Componer();
        s.RemoveAll<IProviderCommercialEffectPort>();
        s.AddScoped<IProviderCommercialEffectPort, PuertoPositivo>();
        Rechazado("S04b puerto positivo en lugar del negativo", () => s.BuildNegativeServiceProvider(), "IProviderCommercialEffectPort");
    }

    [Fact]
    public void S04c_Sustituir_el_adaptador_por_una_factoria_impide_construir()
    {
        var s = Componer();
        s.RemoveAll<IProviderCommercialEffectPort>();
        s.AddScoped<IProviderCommercialEffectPort>(_ => new PuertoPositivo());
        Rechazado("S04c factoría en lugar del negativo", () => s.BuildNegativeServiceProvider(), "IProviderCommercialEffectPort");
    }

    [Fact]
    public void S04d_El_adaptador_como_singleton_impide_construir()
    {
        var s = Componer();
        s.RemoveAll<IProviderCommercialEffectPort>();
        s.AddSingleton<IProviderCommercialEffectPort, M03ReadOnlyNegativeEffectPortAdapter>();
        Rechazado("S04d adaptador negativo con otro ciclo de vida", () => s.BuildNegativeServiceProvider(), "IProviderCommercialEffectPort");
    }

    [Fact]
    public void S04e_Un_puerto_de_efectos_dentro_de_la_fuente_M11_rechaza_la_composicion()
    {
        var s = new ServiceCollection();
        Rechazado("S04e PaymentsModule que registrara un puerto de efectos",
            () => s.AddPaymentsNegativeComposition(Config(), (x, c) =>
            {
                new PaymentsModule().RegisterServices(x, c);
                x.AddScoped<IProviderCommercialEffectPort, PuertoPositivo>();
            }),
            "rechaza el inventario de PaymentsModule");
        Assert.Empty(s);
    }

    // Controles equivalentes sobre los otros puertos que sostienen la garantía.
    [Fact]
    public void S05_Sustituir_la_politica_Hold_por_una_de_reintento_impide_construir()
    {
        var s = Componer();
        s.RemoveAll<IProviderCommercialRetryPolicy>();
        s.AddSingleton<IProviderCommercialRetryPolicy>(new ConfiguredCommercialRetryPolicy(3));
        Rechazado("S05 política de reintento en lugar de Hold", () => s.BuildNegativeServiceProvider(), "IProviderCommercialRetryPolicy");
    }

    [Fact]
    public void S06_Una_correlacion_autorizada_ficticia_impide_construir()
    {
        var s = Componer();
        s.RemoveAll<IProviderAuthorizedOrderCorrelationSource>();
        s.AddScoped<IProviderAuthorizedOrderCorrelationSource, CorrelacionFicticia>();
        Rechazado("S06 correlación autorizada ficticia", () => s.BuildNegativeServiceProvider(), "IProviderAuthorizedOrderCorrelationSource");
    }

    [Fact]
    public void S07_Sustituir_el_consumidor_M11_de_la_evaluacion_impide_construir()
    {
        var s = Componer();
        s.RemoveAll<IM03CommercialReadOnlyEvaluationPort>();
        s.AddScoped<IM03CommercialReadOnlyEvaluationPort, PuertoM11Falso>();
        Rechazado("S07 consumidor M11 sustituido", () => s.BuildNegativeServiceProvider(), "IM03CommercialReadOnlyEvaluationPort");
    }

    [Fact]
    public void S08_Una_evaluacion_M03_falsa_por_tipo_impide_construir()
    {
        var s = Componer();
        s.RemoveAll<ICommercialReadOnlyEvaluationCandidate>();
        s.AddScoped<ICommercialReadOnlyEvaluationCandidate, EvaluacionFalsa>();
        Rechazado("S08 evaluación M03 sustituida por tipo", () => s.BuildNegativeServiceProvider(), "ICommercialReadOnlyEvaluationCandidate");
    }

    [Fact]
    public void S08b_Una_evaluacion_M03_falsa_por_factoria_la_detecta_la_resolucion()
    {
        var s = Componer();
        s.RemoveAll<ICommercialReadOnlyEvaluationCandidate>();
        s.AddScoped<ICommercialReadOnlyEvaluationCandidate>(_ => new EvaluacionFalsa());
        Rechazado("S08b evaluación M03 sustituida por factoría", () => s.BuildNegativeServiceProvider(), "adaptador sin efectos de M03");
    }

    [Fact]
    public void S09_Readmitir_la_recepcion_o_el_escritor_de_hechos_de_M11_impide_construir()
    {
        var s = Componer();
        new PaymentsModule().RegisterServices(s, Config());
        Rechazado("S09 PaymentsModule completo añadido tras componer", () => s.BuildNegativeServiceProvider(), "no está intacta");
    }

    [Fact]
    public void S10_Sin_sabotaje_la_composicion_construye_y_resuelve()
    {
        var s = Componer();
        using var sp = s.BuildNegativeServiceProvider();
        using var scope = sp.CreateScope();
        Assert.IsType<M03ReadOnlyNegativeEffectPortAdapter>(Assert.Single(scope.ServiceProvider.GetServices<IProviderCommercialEffectPort>()));
        lock (Registro)
        {
            Registro.Add($"S10 control sin sabotaje | CONSTRUYE | descriptores={s.Count}");
            Escribir("sabotajes-di.txt", Registro.Order(StringComparer.Ordinal));
        }
    }
}
