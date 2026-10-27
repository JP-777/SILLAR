#if SALES_MONETARY_CONTRACTS
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Reports.Orders;
using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Reports.Tests;

/// <summary>
/// La adaptación a los contratos públicos de M03 (candidato <c>014fba0</c>). Solo
/// compila con <c>-p:SalesMonetaryContracts=true</c> sobre un árbol que los tenga.
/// </summary>
public sealed class SalesMonetaryAdapterTests
{
    private static readonly DateTimeOffset InicioUtc = new(2026, 9, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FinUtc = new(2026, 10, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Observado = new(2026, 10, 26, 15, 42, 10, TimeSpan.Zero);

    /// <summary>Lo que devuelve hoy el reductor de M03: personal medido, proveedor sin escritor.</summary>
    private static readonly OrderPaymentAggregateCapabilities CapacidadesDeM03 = new(
        SourceRecorded: true, CurrencyRecorded: true, LateRecorded: true, ProviderWriterPresent: false);

    private static OrderPaymentAggregateSnapshot PagosDeM03(long staff, long sinMoneda, params PaymentAmountByCurrency[] monedas)
        => new(InicioUtc, FinUtc, Observado,
            PaymentCountMeasure.Measured(staff),
            PaymentCountMeasure.NotMeasurable(NotMeasurableReason.ProviderWriterAbsent),
            PaymentCountMeasure.Measured(1),
            monedas,
            sinMoneda,
            sinMoneda == 0
                ? new PaymentAmountsCoverage(MeasureState.Measured, NotMeasurableReason.None)
                : new PaymentAmountsCoverage(MeasureState.NotMeasurable, NotMeasurableReason.CurrencyNotRecorded));

    [Fact]
    public void El_valor_de_M03_se_copia_campo_a_campo_con_activos_y_bajas_separados()
    {
        var snapshot = new OrderValueAggregateSnapshot(InicioUtc, FinUtc, Observado, 57,
            [new("PEN", 39, 12340.50m, 2, 500m), new("USD", 3, 310.00m, 0, 0m)],
            new OrdersWithoutCurrency(10, 3));

        var cohorte = SalesOrderValueAdapter.Map(snapshot);

        Assert.Equal(new OrderValueCohort(InicioUtc, FinUtc, Observado, 57, cohorte.ByCurrency, 10, 3), cohorte);
        Assert.Equal(
            [new OrderValueCurrencyGroup("PEN", 39, 12340.50m, 2, 500m), new OrderValueCurrencyGroup("USD", 3, 310.00m, 0, 0m)],
            cohorte.ByCurrency);
    }

    [Fact]
    public async Task Con_el_lector_publico_de_M03_registrado_I02_se_lee_de_el_y_no_del_puerto_interno()
    {
        var lector = new LectorDeValor(new(InicioUtc, FinUtc, Observado, 5, [new("PEN", 4, 40m, 1, 10m)], new(0, 0)));

        var (codigo, cuerpo) = await Consultar(Aplicacion(s => s.AddSingleton<IOrderValueAggregateReader>(lector)), "/api/admin/reporting/orders/value");

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("with_data", cuerpo.GetProperty("status").GetString());
        Assert.Equal("10", cuerpo.GetProperty("byCurrency")[0].GetProperty("inactiveAmount").GetString());
        Assert.Equal((InicioUtc, FinUtc), lector.Periodo);
    }

    [Fact]
    public async Task Con_el_lector_publico_de_pagos_y_sin_escritor_del_personal_acreditado_I05_I07_no_son_medibles()
    {
        var lector = new LectorDePagos(CapacidadesDeM03, PagosDeM03(staff: 0, sinMoneda: 0));

        var (_, cuerpo) = await Consultar(Aplicacion(s => s.AddSingleton<IOrderPaymentAggregateReader>(lector)), "/api/admin/reporting/payments");

        Assert.Equal("not_measurable", cuerpo.GetProperty("i05").GetProperty("status").GetString());
        Assert.Equal("staff_writer_not_accredited", cuerpo.GetProperty("notMeasurableReason").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i05").GetProperty("count").ValueKind);
        Assert.Equal(0, lector.Llamadas);
    }

    [Fact]
    public void Las_capacidades_de_M03_pasan_tal_cual_y_el_escritor_del_personal_queda_sin_acreditar()
    {
        Assert.Equal(new PaymentCapabilities(true, true, true, false, StaffWriterPresent: false),
            SalesOrderPaymentAdapter.Map(CapacidadesDeM03));
    }

    [Fact]
    public void Una_cohorte_de_M03_con_proveedor_sin_escritor_y_pagos_sin_moneda_da_cobertura_parcial_y_ningun_vacio()
    {
        var cohorte = SalesOrderPaymentAdapter.Map(PagosDeM03(staff: 3, sinMoneda: 1, new PaymentAmountByCurrency("PEN", 2, 70m)));

        Assert.Equal(new PaymentMeasure(false, null, NotMeasurableReasons.ProviderWriterAbsent), cohorte.Provider);
        Assert.False(cohorte.AmountsComplete);

        // Con el escritor del personal acreditado, que es lo único que M03 aún no declara.
        var vista = OrderPaymentReport.Project(
            SalesOrderPaymentAdapter.Map(CapacidadesDeM03) with { StaffWriterPresent = true }, cohorte, InicioUtc, FinUtc);

        Assert.Equal((IndicatorStatus.WithData, 3L, CurrencyCoverage.Partial), (vista.I05.Status, vista.I05.Count, vista.I05.Coverage));
        Assert.Equal([new SourceMeasureView("staff", MeasureStatus.Measured, 3, null),
            new SourceMeasureView("provider", MeasureStatus.NotMeasurable, null, NotMeasurableReasons.ProviderWriterAbsent)], vista.I05.BySource);
        Assert.Equal(CurrencyCoverage.Partial, vista.I06.Coverage);
        Assert.Equal(1, vista.I06.WithoutCurrencyCount);
        Assert.Equal((IndicatorStatus.WithData, 1L), (vista.I07.Status, vista.I07.Count));
    }

    public static TheoryData<string> MedidasIncoherentes => ["medida_con_motivo", "no_medible_sin_motivo", "motivo_desconocido"];

    [Theory]
    [MemberData(nameof(MedidasIncoherentes))]
    public void Un_estado_y_un_motivo_de_M03_que_no_concuerdan_no_se_adaptan(string caso)
    {
        PaymentCountMeasure medida = caso switch
        {
            "medida_con_motivo" => new(MeasureState.Measured, 1, NotMeasurableReason.ProviderWriterAbsent),
            "no_medible_sin_motivo" => new(MeasureState.NotMeasurable, null, NotMeasurableReason.None),
            _ => new(MeasureState.NotMeasurable, null, (NotMeasurableReason)99),
        };

        Assert.Throws<SourceIncoherentException>(() => SalesOrderPaymentAdapter.Map(medida));
    }

    [Fact]
    public void Una_cohorte_de_valor_sin_pedidos_sin_moneda_informados_no_se_adapta()
    {
        var incompleta = new OrderValueAggregateSnapshot(InicioUtc, FinUtc, Observado, 0, [], null!);

        Assert.Throws<SourceIncoherentException>(() => SalesOrderValueAdapter.Map(incompleta));
    }

    private static WebApplication Aplicacion(Action<IServiceCollection> lectores)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IOrderAggregateReader>(new VentasPresente());
        lectores(builder.Services);
        new ReportsModule().RegisterServices(builder.Services, new ConfigurationBuilder().Build());
        var app = builder.Build();
        new ReportsModule().MapEndpoints(app);
        return app;
    }

    private static async Task<(int Codigo, JsonElement Cuerpo)> Consultar(WebApplication app, string ruta)
    {
        var destino = ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>()
            .Single(r => r.RoutePattern.RawText == ruta);
        await using var scope = app.Services.CreateAsyncScope();
        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        contexto.Request.Method = "GET";
        contexto.Request.Path = ruta;
        contexto.Request.QueryString = QueryString.Create(new Dictionary<string, string?>
        {
            ["start"] = "2026-09-01T00:00:00-05:00",
            ["end"] = "2026-10-01T00:00:00-05:00",
        });
        using var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await destino.RequestDelegate!(contexto);

        return (contexto.Response.StatusCode, JsonDocument.Parse(salida.ToArray()).RootElement.Clone());
    }

    private sealed class VentasPresente : IOrderAggregateReader
    {
        public Task<OrderAggregateSnapshot> ReadCreatedBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
            => throw new InvalidOperationException("No se leen recuentos de pedidos.");
    }

    private sealed class LectorDeValor(OrderValueAggregateSnapshot respuesta) : IOrderValueAggregateReader
    {
        public (DateTimeOffset, DateTimeOffset) Periodo { get; private set; }

        public Task<OrderValueAggregateSnapshot> ReadCreatedBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
        {
            Periodo = (startInclusive, endExclusive);
            return Task.FromResult(respuesta);
        }
    }

    private sealed class LectorDePagos(OrderPaymentAggregateCapabilities capacidades, OrderPaymentAggregateSnapshot respuesta)
        : IOrderPaymentAggregateReader
    {
        public int Llamadas { get; private set; }

        public OrderPaymentAggregateCapabilities Capabilities => capacidades;

        public Task<OrderPaymentAggregateSnapshot> ReadRegisteredBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
        {
            Llamadas++;
            return Task.FromResult(respuesta);
        }
    }
}
#endif
