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
/// La adaptación a los contratos públicos de M03 (candidato <c>30cb8c3</c>).
/// </summary>
public sealed class SalesMonetaryAdapterTests
{
    private static readonly DateTimeOffset InicioUtc = new(2026, 9, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FinUtc = new(2026, 10, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Observado = new(2026, 10, 26, 15, 42, 10, TimeSpan.Zero);

    private static readonly DateTimeOffset AcreditadoAntes = InicioUtc.AddDays(-30);

    /// <summary>Cobertura de un origen en la forma de M03: los tres indicadores con el mismo estado.</summary>
    private static IEnumerable<PaymentCoverage> Cobertura(string origen, NotMeasurableReason motivo, DateTimeOffset? desde)
        => Enum.GetValues<PaymentIndicator>().Select(i => new PaymentCoverage(origen, i,
            motivo == NotMeasurableReason.None ? MeasureState.Measured : MeasureState.NotMeasurable, motivo, desde));

    /// <summary>Lo que devuelve hoy M03: ningún origen acreditado, aunque haya filas históricas.</summary>
    private static OrderPaymentAggregateSnapshot PagosDeHoy()
        => new(InicioUtc, FinUtc, Observado,
            [new(PaymentSource.Staff, PaymentMethod.Yape, "PEN", false, 4, 400m)],
            [.. PaymentSource.All.SelectMany(s => Cobertura(s, NotMeasurableReason.WriterNotAccredited, null))]);

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
    public void Los_origenes_sobre_los_que_M03_declara_cobertura_son_los_que_espera_M10()
    {
        Assert.Equal(PaymentSource.All, OrderPaymentReport.Sources);
        Assert.Equal(Enum.GetValues<PaymentIndicator>().Length, PaymentIndicators.All.Length);
    }

    [Fact]
    public async Task Con_el_lector_publico_de_pagos_y_ningun_escritor_acreditado_I05_I07_no_son_medibles_aunque_haya_filas()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(s => s.AddSingleton<IOrderPaymentAggregateReader>(new LectorDePagos(PagosDeHoy()))),
            "/api/admin/reporting/payments");

        Assert.Equal("not_measurable", cuerpo.GetProperty("i05").GetProperty("status").GetString());
        Assert.Equal("writer_not_accredited", cuerpo.GetProperty("notMeasurableReason").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i05").GetProperty("count").ValueKind);
        Assert.DoesNotContain("400", cuerpo.GetRawText());
    }

    [Fact]
    public async Task Con_el_personal_acreditado_y_el_proveedor_no_los_datos_salen_parciales_por_origen()
    {
        var snapshot = new OrderPaymentAggregateSnapshot(InicioUtc, FinUtc, Observado,
            [new(PaymentSource.Staff, PaymentMethod.Yape, "PEN", false, 2, 70m), new(PaymentSource.Staff, PaymentMethod.Cash, null, true, 1, null)],
            [.. Cobertura(PaymentSource.Staff, NotMeasurableReason.None, AcreditadoAntes)
                .Select(c => c.Indicator == PaymentIndicator.AmountByCurrency
                    ? c with { State = MeasureState.NotMeasurable, Reason = NotMeasurableReason.CurrencyNotRecorded }
                    : c),
             .. Cobertura(PaymentSource.Provider, NotMeasurableReason.WriterNotAccredited, null)]);

        var (_, cuerpo) = await Consultar(Aplicacion(s => s.AddSingleton<IOrderPaymentAggregateReader>(new LectorDePagos(snapshot))),
            "/api/admin/reporting/payments");

        var i05 = cuerpo.GetProperty("i05");
        Assert.Equal(("with_data", 3L, "partial"), (i05.GetProperty("status").GetString(), i05.GetProperty("count").GetInt64(), i05.GetProperty("coverage").GetString()));
        Assert.Equal(AcreditadoAntes, i05.GetProperty("bySource")[0].GetProperty("coveredSinceUtc").GetDateTimeOffset());
        Assert.Equal("writer_not_accredited", i05.GetProperty("bySource")[1].GetProperty("reason").GetString());
        Assert.Equal("currency_not_accredited", cuerpo.GetProperty("i06").GetProperty("status").GetString());
        Assert.Equal(1, cuerpo.GetProperty("i06").GetProperty("withoutCurrencyCount").GetInt64());
        Assert.Equal((1L, "partial"), (cuerpo.GetProperty("i07").GetProperty("count").GetInt64(), cuerpo.GetProperty("i07").GetProperty("coverage").GetString()));
    }

    [Fact]
    public void Los_grupos_y_la_cobertura_de_M03_se_copian_campo_a_campo()
    {
        var ventana = SalesOrderPaymentAdapter.Map(PagosDeHoy());

        Assert.Equal([new PaymentGroup("staff", "yape", "PEN", false, 4, 400m)], ventana.Groups);
        Assert.Equal(6, ventana.Coverage.Count);
        Assert.All(ventana.Coverage, c => Assert.Equal((false, "writer_not_accredited", (DateTimeOffset?)null), (c.Measured, c.Reason, c.CoveredSinceUtc)));
        Assert.Equal(["count", "late_count", "amount_by_currency"], ventana.Coverage.Where(c => c.Source == "staff").Select(c => c.Indicator));
    }

    [Fact]
    public async Task Un_historico_con_origen_null_de_M03_se_presenta_como_no_acreditado_y_no_como_personal()
    {
        var snapshot = new OrderPaymentAggregateSnapshot(InicioUtc, FinUtc, Observado,
            [new(null, PaymentMethod.Yape, "PEN", false, 5, 500m), new(PaymentSource.Staff, PaymentMethod.Yape, "PEN", false, 2, 20m)],
            [.. Cobertura(PaymentSource.Staff, NotMeasurableReason.SourceNotRecorded, AcreditadoAntes),
             .. Cobertura(PaymentSource.Provider, NotMeasurableReason.WriterNotAccredited, null)]);

        var ventana = SalesOrderPaymentAdapter.Map(snapshot);
        Assert.Null(ventana.Groups[0].Source);
        Assert.Equal("source_not_recorded", ventana.Coverage[0].Reason);

        var (codigo, cuerpo) = await Consultar(Aplicacion(s => s.AddSingleton<IOrderPaymentAggregateReader>(new LectorDePagos(snapshot))),
            "/api/admin/reporting/payments");

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("not_measurable", cuerpo.GetProperty("i05").GetProperty("status").GetString());
        Assert.Equal("source_not_recorded", cuerpo.GetProperty("notMeasurableReason").GetString());
        Assert.Equal(5, cuerpo.GetProperty("historicalWithoutSource").GetProperty("count").GetInt64());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i05").GetProperty("bySource").ValueKind);
    }

    public static TheoryData<string> CoberturasIncoherentes => ["medida_con_motivo", "no_medible_sin_motivo", "motivo_desconocido", "indicador_desconocido"];

    [Theory]
    [MemberData(nameof(CoberturasIncoherentes))]
    public void Un_estado_un_motivo_o_un_indicador_de_M03_que_M10_no_reconoce_no_se_adaptan(string caso)
    {
        PaymentCoverage cobertura = caso switch
        {
            "medida_con_motivo" => new(PaymentSource.Staff, PaymentIndicator.Count, MeasureState.Measured, NotMeasurableReason.WriterNotAccredited, null),
            "no_medible_sin_motivo" => new(PaymentSource.Staff, PaymentIndicator.Count, MeasureState.NotMeasurable, NotMeasurableReason.None, null),
            "motivo_desconocido" => new(PaymentSource.Staff, PaymentIndicator.Count, MeasureState.NotMeasurable, (NotMeasurableReason)99, null),
            _ => new(PaymentSource.Staff, (PaymentIndicator)99, MeasureState.Measured, NotMeasurableReason.None, AcreditadoAntes),
        };

        Assert.Throws<SourceIncoherentException>(() => SalesOrderPaymentAdapter.Map(cobertura));
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

    private sealed class LectorDePagos(OrderPaymentAggregateSnapshot respuesta) : IOrderPaymentAggregateReader
    {
        public Task<OrderPaymentAggregateSnapshot> ReadRegisteredBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
            => Task.FromResult(respuesta);
    }
}
