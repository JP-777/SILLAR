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
/// I05–I07 por la ruta que monta el módulo, con la forma del lector de pagos de
/// M03: solo cuenta lo que la cobertura declara medido, por origen e indicador;
/// sin ningún escritor acreditado, «no medible» aunque haya filas.
/// </summary>
public sealed class PaymentIndicatorsTests
{
    private const string Ruta = "/api/admin/reporting/payments";
    private const string Inicio = "2026-09-01T00:00:00-05:00";
    private const string Fin = "2026-10-01T00:00:00-05:00";
    private static readonly DateTimeOffset InicioUtc = new(2026, 9, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FinUtc = new(2026, 10, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Observado = new(2026, 10, 26, 15, 42, 10, TimeSpan.Zero);
    private static readonly DateTimeOffset AcreditadoAntes = InicioUtc.AddDays(-30);
    private static readonly DateTimeOffset AcreditadoDespues = InicioUtc.AddDays(3);
    private static readonly string[] Indicadores = ["i05", "i06", "i07"];

    /// <summary>Cómo está cubierto un origen en la ventana.</summary>
    private enum Cubierto { Medido, SinEscritor, DesdeDespues, SinMoneda, SinOrigen }

    private static IEnumerable<PaymentCoverageEntry> Cobertura(string origen, Cubierto cubierto)
        => cubierto switch
        {
            Cubierto.Medido => PaymentIndicators.All.Select(i => new PaymentCoverageEntry(origen, i, true, null, AcreditadoAntes)),
            Cubierto.SinEscritor => PaymentIndicators.All.Select(i => new PaymentCoverageEntry(origen, i, false, "writer_not_accredited", null)),
            Cubierto.DesdeDespues => PaymentIndicators.All.Select(i => new PaymentCoverageEntry(origen, i, false, "window_starts_before_coverage", AcreditadoDespues)),
            Cubierto.SinOrigen => PaymentIndicators.All.Select(i => new PaymentCoverageEntry(origen, i, false, "source_not_recorded", AcreditadoAntes)),
            _ => PaymentIndicators.All.Select(i => i == PaymentIndicators.AmountByCurrency
                ? new PaymentCoverageEntry(origen, i, false, "currency_not_recorded", AcreditadoAntes)
                : new PaymentCoverageEntry(origen, i, true, null, AcreditadoAntes)),
        };

    private static PaymentWindow Ventana(Cubierto personal, Cubierto proveedor, params PaymentGroup[] grupos)
        => new(InicioUtc, FinUtc, Observado, grupos, [.. Cobertura("staff", personal), .. Cobertura("provider", proveedor)]);

    private static PaymentGroup Grupo(string? origen, string medio, string? moneda, bool tardio, long pagos, decimal? importe)
        => new(origen, medio, moneda, tardio, pagos, importe);

    [Fact]
    public async Task Sin_el_modulo_de_ventas_los_tres_indicadores_salen_fuente_no_disponible()
    {
        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: false, new PuertoFijo(Ventana(Cubierto.Medido, Cubierto.Medido))));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Estados(cuerpo, "source_unavailable");
        SinCifras(cuerpo);
    }

    [Fact]
    public async Task Con_ventas_y_sin_lector_de_cobros_los_tres_son_no_medibles_y_ninguno_es_cero()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, puerto: null));

        Estados(cuerpo, "not_measurable");
        Assert.Equal("reader_absent", cuerpo.GetProperty("notMeasurableReason").GetString());
        SinCifras(cuerpo);
    }

    [Fact]
    public async Task Sin_ningun_escritor_acreditado_es_no_medible_aunque_haya_filas()
    {
        var ventana = Ventana(Cubierto.SinEscritor, Cubierto.SinEscritor, Grupo("staff", "yape", "PEN", false, 4, 400m));

        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(ventana)));

        Estados(cuerpo, "not_measurable");
        Assert.Equal("writer_not_accredited", cuerpo.GetProperty("notMeasurableReason").GetString());
        SinCifras(cuerpo);
        Assert.DoesNotContain("400", cuerpo.GetRawText());
    }

    [Fact]
    public async Task Con_escritores_acreditados_despues_del_inicio_de_la_ventana_es_no_medible_y_lo_dice()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Ventana(Cubierto.DesdeDespues, Cubierto.SinEscritor))));

        Estados(cuerpo, "not_measurable");
        Assert.Equal("window_starts_before_coverage", cuerpo.GetProperty("notMeasurableReason").GetString());
    }

    [Fact]
    public async Task Con_el_proveedor_sin_escritor_un_cero_del_personal_no_es_un_periodo_vacio_ni_cobertura_total()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Ventana(Cubierto.Medido, Cubierto.SinEscritor))));

        var i05 = cuerpo.GetProperty("i05");
        Assert.Equal("with_data", i05.GetProperty("status").GetString());
        Assert.Equal("partial", i05.GetProperty("coverage").GetString());
        Assert.Equal(0, i05.GetProperty("count").GetInt64());
        var origenes = i05.GetProperty("bySource").EnumerateArray().Select(s => (
            s.GetProperty("source").GetString(), s.GetProperty("status").GetString(),
            s.GetProperty("count").ValueKind == JsonValueKind.Null ? (long?)null : s.GetProperty("count").GetInt64(),
            s.GetProperty("reason").GetString())).ToArray();
        Assert.Equal([("staff", "measured", 0L, null), ("provider", "not_measurable", null, "writer_not_accredited")], origenes);
        Assert.Equal("partial", cuerpo.GetProperty("i06").GetProperty("coverage").GetString());
        Assert.Equal("partial", cuerpo.GetProperty("i07").GetProperty("coverage").GetString());
        Assert.DoesNotContain("\"empty\"", cuerpo.GetRawText());
    }

    [Fact]
    public async Task Las_filas_de_un_origen_no_medido_no_se_suman_a_las_del_medido()
    {
        var ventana = Ventana(Cubierto.Medido, Cubierto.SinEscritor,
            Grupo("staff", "yape", "PEN", false, 2, 20m), Grupo("provider", "yape", "PEN", false, 5, 50m));

        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(ventana)));

        Assert.Equal(2, cuerpo.GetProperty("i05").GetProperty("count").GetInt64());
        Assert.Equal("20", cuerpo.GetProperty("i06").GetProperty("byCurrency")[0].GetProperty("amount").GetString());
        Assert.DoesNotContain("\"70\"", cuerpo.GetRawText());
    }

    [Fact]
    public async Task Solo_con_todos_los_origenes_medidos_y_sin_pagos_los_tres_salen_vacios()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Ventana(Cubierto.Medido, Cubierto.Medido))));

        Estados(cuerpo, "empty");
        Assert.Equal("complete", cuerpo.GetProperty("i05").GetProperty("coverage").GetString());
    }

    [Fact]
    public async Task Separa_origen_medio_moneda_y_tardanza_sin_sumar_monedas_distintas()
    {
        var ventana = Ventana(Cubierto.Medido, Cubierto.Medido,
            Grupo("staff", "yape", "PEN", false, 4, 400.00m),
            Grupo("staff", "efectivo", "USD", true, 1, 20.00m),
            Grupo("provider", "yape", "PEN", false, 2, 150m),
            Grupo("provider", "yape", "PEN", true, 1, 35.5m));

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(ventana)));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        var i05 = cuerpo.GetProperty("i05");
        Assert.Equal((8L, "complete"), (i05.GetProperty("count").GetInt64(), i05.GetProperty("coverage").GetString()));
        Assert.Equal([5L, 3L], i05.GetProperty("bySource").EnumerateArray().Select(s => s.GetProperty("count").GetInt64()));

        var i06 = cuerpo.GetProperty("i06");
        Assert.Equal(("with_data", "complete"), (i06.GetProperty("status").GetString(), i06.GetProperty("coverage").GetString()));
        Assert.Equal(
            [("efectivo", "staff", "USD", 1L, "20.00"), ("yape", "staff", "PEN", 4L, "400.00"), ("yape", "provider", "PEN", 3L, "185.5")],
            i06.GetProperty("byMethod").EnumerateArray().Select(m => (
                m.GetProperty("method").GetString(), m.GetProperty("source").GetString(), m.GetProperty("currencyCode").GetString(),
                m.GetProperty("count").GetInt64(), m.GetProperty("amount").GetString())));
        Assert.Equal([("PEN", 7L, "585.50"), ("USD", 1L, "20.00")], Monedas(i06.GetProperty("byCurrency")));

        var i07 = cuerpo.GetProperty("i07");
        Assert.Equal((2L, "complete"), (i07.GetProperty("count").GetInt64(), i07.GetProperty("coverage").GetString()));
        Assert.Equal("with_data", i07.GetProperty("amountStatus").GetString());
        Assert.Equal([("PEN", 1L, "35.5"), ("USD", 1L, "20.00")], Monedas(i07.GetProperty("amountByCurrency")));

        Assert.DoesNotContain("605.5", cuerpo.GetRawText());
        Assert.DoesNotContain("55.5", cuerpo.GetRawText());
    }

    [Fact]
    public async Task Con_pagos_sin_moneda_se_cuentan_y_su_importe_no_se_acredita()
    {
        var ventana = Ventana(Cubierto.SinMoneda, Cubierto.SinEscritor,
            Grupo("staff", "yape", "PEN", false, 1, 10m), Grupo("staff", "efectivo", null, true, 2, null));

        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(ventana)));

        Assert.Equal(3, cuerpo.GetProperty("i05").GetProperty("count").GetInt64());
        var i06 = cuerpo.GetProperty("i06");
        Assert.Equal(("currency_not_accredited", "none"), (i06.GetProperty("status").GetString(), i06.GetProperty("coverage").GetString()));
        Assert.Equal(JsonValueKind.Null, i06.GetProperty("byCurrency").ValueKind);
        Assert.Equal(2, i06.GetProperty("withoutCurrencyCount").GetInt64());
        Assert.Equal(2, cuerpo.GetProperty("i07").GetProperty("count").GetInt64());
        Assert.Equal("currency_not_accredited", cuerpo.GetProperty("i07").GetProperty("amountStatus").GetString());
    }

    [Fact]
    public async Task Cada_indicador_de_un_origen_puede_estar_acreditado_desde_una_fecha_distinta()
    {
        var ventana = new PaymentWindow(InicioUtc, FinUtc, Observado,
            [Grupo("staff", "yape", "PEN", false, 2, 20m), Grupo("staff", "efectivo", "PEN", true, 1, 5m)],
            [
                new("staff", "count", true, null, AcreditadoAntes),
                new("staff", "late_count", true, null, AcreditadoAntes.AddDays(20)),
                new("staff", "amount_by_currency", false, "window_starts_before_coverage", AcreditadoDespues),
                .. Cobertura("provider", Cubierto.SinEscritor),
            ]);

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(ventana)));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal((3L, "partial"), (cuerpo.GetProperty("i05").GetProperty("count").GetInt64(), cuerpo.GetProperty("i05").GetProperty("coverage").GetString()));
        Assert.Equal(AcreditadoAntes.AddDays(20), cuerpo.GetProperty("i07").GetProperty("bySource")[0].GetProperty("coveredSinceUtc").GetDateTimeOffset());
        Assert.Equal(1, cuerpo.GetProperty("i07").GetProperty("count").GetInt64());
        // El importe no se mide por la ventana, no por la moneda: no medible, y sin cifras.
        var i06 = cuerpo.GetProperty("i06");
        Assert.Equal("not_measurable", i06.GetProperty("status").GetString());
        Assert.Equal("window_starts_before_coverage", i06.GetProperty("bySource")[0].GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, i06.GetProperty("byCurrency").ValueKind);
        Assert.Equal("not_measurable", cuerpo.GetProperty("i07").GetProperty("amountStatus").GetString());
    }

    [Fact]
    public async Task Los_tardios_medidos_de_un_origen_cuyo_recuento_no_se_mide_se_publican_solo_en_I07()
    {
        var ventana = new PaymentWindow(InicioUtc, FinUtc, Observado,
            [Grupo("staff", "yape", "PEN", false, 2, 20m), Grupo("provider", "yape", "PEN", true, 3, 30m)],
            [
                .. Cobertura("staff", Cubierto.Medido),
                new("provider", "count", false, "window_starts_before_coverage", AcreditadoDespues),
                new("provider", "late_count", true, null, AcreditadoAntes),
                new("provider", "amount_by_currency", false, "window_starts_before_coverage", AcreditadoDespues),
            ]);

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(ventana)));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal((2L, "partial"), (cuerpo.GetProperty("i05").GetProperty("count").GetInt64(), cuerpo.GetProperty("i05").GetProperty("coverage").GetString()));
        Assert.Equal((3L, "complete"), (cuerpo.GetProperty("i07").GetProperty("count").GetInt64(), cuerpo.GetProperty("i07").GetProperty("coverage").GetString()));
        Assert.Equal("partial", cuerpo.GetProperty("i07").GetProperty("amountCoverage").GetString());
    }

    [Fact]
    public async Task Los_historicos_sin_origen_sin_escritor_acreditado_se_cuentan_aparte_y_no_dan_ninguna_cifra_por_origen()
    {
        var ventana = Ventana(Cubierto.SinEscritor, Cubierto.SinEscritor,
            Grupo(null, "yape", "PEN", false, 6, 600m), Grupo("staff", "efectivo", "PEN", false, 1, 10m));

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(ventana)));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Estados(cuerpo, "not_measurable");
        Assert.Equal("writer_not_accredited", cuerpo.GetProperty("notMeasurableReason").GetString());
        SinCifras(cuerpo);
        var historicos = cuerpo.GetProperty("historicalWithoutSource");
        Assert.Equal(6, historicos.GetProperty("count").GetInt64());
        Assert.Equal(Observado, historicos.GetProperty("observedAtUtc").GetDateTimeOffset());
        Assert.DoesNotContain("600", cuerpo.GetRawText());
    }

    [Fact]
    public async Task Con_historicos_sin_origen_ningun_origen_acreditado_es_completo_y_nada_se_imputa_al_personal()
    {
        var ventana = Ventana(Cubierto.SinOrigen, Cubierto.SinEscritor,
            Grupo(null, "yape", "PEN", true, 4, 40m), Grupo("staff", "yape", "PEN", false, 2, 20m));

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(ventana)));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Estados(cuerpo, "not_measurable");
        Assert.Equal("source_not_recorded", cuerpo.GetProperty("notMeasurableReason").GetString());
        // Ni 2 (lo observado del personal) ni 6 (personal más históricos) aparecen como cobros del personal.
        SinCifras(cuerpo);
        Assert.Equal(4, cuerpo.GetProperty("historicalWithoutSource").GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task Sin_historicos_sin_origen_no_se_informa_ninguno()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true,
            new PuertoFijo(Ventana(Cubierto.Medido, Cubierto.SinEscritor, Grupo("staff", "yape", "PEN", false, 2, 20m)))));

        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("historicalWithoutSource").ValueKind);
        Assert.Equal(2, cuerpo.GetProperty("i05").GetProperty("count").GetInt64());
    }

    public static TheoryData<string> Incoherencias =>
        ["cobertura_incompleta", "cobertura_repetida", "medido_con_motivo", "medido_acreditado_despues", "sin_escritor_con_fecha",
         "importe_completo_con_pagos_sin_moneda", "sin_moneda_sin_pagos_sin_moneda",
         "sin_moneda_en_el_recuento", "origen_desconocido", "medio_mayusculas", "grupo_repetido",
         "importe_sin_moneda", "importe_negativo", "otro_periodo",
         "origen_medido_con_historicos_sin_origen", "sin_origen_sin_historicos", "sin_origen_sin_acreditacion", "origen_vacio"];

    [Theory]
    [MemberData(nameof(Incoherencias))]
    public async Task Una_respuesta_de_cobros_que_no_cuadra_responde_503_sin_ninguna_cifra(string caso)
    {
        var ventana = Ventana(Cubierto.Medido, Cubierto.SinEscritor, Grupo("staff", "yape", "PEN", false, 2, 20m));
        PaymentWindow Cambia(string origen, string indicador, PaymentCoverageEntry nueva)
            => ventana with { Coverage = [.. ventana.Coverage.Select(c => c.Source == origen && c.Indicator == indicador ? nueva : c)] };
        ventana = caso switch
        {
            "cobertura_incompleta" => ventana with { Coverage = [.. ventana.Coverage.Skip(1)] },
            "cobertura_repetida" => ventana with { Coverage = [.. ventana.Coverage, ventana.Coverage[0]] },
            "medido_con_motivo" => Cambia("staff", "count", new("staff", "count", true, "writer_not_accredited", AcreditadoAntes)),
            "medido_acreditado_despues" => Ventana(Cubierto.Medido, Cubierto.SinEscritor) with
            {
                Coverage = [.. Cobertura("staff", Cubierto.Medido).Select(c => c with { CoveredSinceUtc = AcreditadoDespues }), .. Cobertura("provider", Cubierto.SinEscritor)],
            },
            "sin_escritor_con_fecha" => Cambia("provider", "count", new("provider", "count", false, "writer_not_accredited", AcreditadoAntes)),
            "importe_completo_con_pagos_sin_moneda" => ventana with { Groups = [Grupo("staff", "yape", null, false, 2, null)] },
            "sin_moneda_sin_pagos_sin_moneda" => Ventana(Cubierto.SinMoneda, Cubierto.SinEscritor, Grupo("staff", "yape", "PEN", false, 2, 20m)),
            "sin_moneda_en_el_recuento" => Cambia("staff", "count", new("staff", "count", false, "currency_not_recorded", AcreditadoAntes)),
            "origen_desconocido" => ventana with { Groups = [Grupo("cliente", "yape", "PEN", false, 2, 20m)] },
            "medio_mayusculas" => ventana with { Groups = [Grupo("staff", "Yape", "PEN", false, 2, 20m)] },
            "grupo_repetido" => ventana with { Groups = [Grupo("staff", "yape", "PEN", false, 1, 10m), Grupo("staff", "yape", "PEN", false, 1, 10m)] },
            "importe_sin_moneda" => ventana with { Groups = [Grupo("staff", "yape", null, false, 2, 20m)] },
            "importe_negativo" => ventana with { Groups = [Grupo("staff", "yape", "PEN", false, 2, -20m)] },
            "origen_medido_con_historicos_sin_origen" => ventana with { Groups = [Grupo(null, "yape", "PEN", false, 1, 10m), Grupo("staff", "yape", "PEN", false, 2, 20m)] },
            "sin_origen_sin_historicos" => Ventana(Cubierto.SinOrigen, Cubierto.SinEscritor, Grupo("staff", "yape", "PEN", false, 2, 20m)),
            "sin_origen_sin_acreditacion" => Ventana(Cubierto.SinEscritor, Cubierto.SinEscritor, Grupo(null, "yape", "PEN", false, 1, 10m)) with
            {
                Coverage = [.. Cobertura("staff", Cubierto.SinOrigen).Select(c => c with { CoveredSinceUtc = null }), .. Cobertura("provider", Cubierto.SinEscritor)],
            },
            "origen_vacio" => ventana with { Groups = [Grupo("", "yape", "PEN", false, 2, 20m)] },
            _ => ventana with { StartInclusive = InicioUtc.AddHours(-1) },
        };

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(ventana)));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
        Assert.Contains("no cuadran", cuerpo.GetProperty("title").GetString());
        Assert.False(cuerpo.TryGetProperty("i05", out _));
    }

    [Fact]
    public async Task Un_fallo_del_lector_de_cobros_responde_503_y_no_cero()
    {
        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Ventana(Cubierto.Medido, Cubierto.Medido)) { Falla = true }));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
        Assert.False(cuerpo.TryGetProperty("i05", out _));
    }

    [Fact]
    public async Task Una_consulta_cancelada_se_propaga()
    {
        using var cancelada = new CancellationTokenSource();
        cancelada.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Consultar(Aplicacion(ventas: true, new PuertoFijo(Ventana(Cubierto.Medido, Cubierto.Medido))), cancelada.Token));
    }

    private static (string?, long, string?)[] Monedas(JsonElement lista)
        => [.. lista.EnumerateArray().Select(m => (
            m.GetProperty("currencyCode").GetString(), m.GetProperty("count").GetInt64(), m.GetProperty("amount").GetString()))];

    private static void Estados(JsonElement cuerpo, string estado)
    {
        foreach (var indicador in Indicadores)
        {
            Assert.Equal(estado, cuerpo.GetProperty(indicador).GetProperty("status").GetString());
        }
    }

    private static void SinCifras(JsonElement cuerpo)
    {
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i05").GetProperty("count").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i05").GetProperty("bySource").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i06").GetProperty("byCurrency").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i06").GetProperty("byMethod").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i06").GetProperty("withoutCurrencyCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i07").GetProperty("count").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i07").GetProperty("amountByCurrency").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("observedAtUtc").ValueKind);
    }

    private static WebApplication Aplicacion(bool ventas, IOrderPaymentAggregatePort? puerto)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddRouting();
        if (ventas) builder.Services.AddSingleton<IOrderAggregateReader>(new VentasPresente());
        if (puerto is not null) builder.Services.AddSingleton(puerto);
        new ReportsModule().RegisterServices(builder.Services, new ConfigurationBuilder().Build());
        var app = builder.Build();
        new ReportsModule().MapEndpoints(app);
        return app;
    }

    private static async Task<(int Codigo, JsonElement Cuerpo)> Consultar(WebApplication app, CancellationToken abortada = default)
    {
        var ruta = ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>()
            .Single(r => r.RoutePattern.RawText == Ruta);
        await using var scope = app.Services.CreateAsyncScope();
        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = abortada };
        contexto.Request.Method = "GET";
        contexto.Request.Path = Ruta;
        contexto.Request.QueryString = QueryString.Create(new Dictionary<string, string?> { ["start"] = Inicio, ["end"] = Fin });
        using var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await ruta.RequestDelegate!(contexto);

        return (contexto.Response.StatusCode, JsonDocument.Parse(salida.ToArray()).RootElement.Clone());
    }

    private sealed class VentasPresente : IOrderAggregateReader
    {
        public Task<OrderAggregateSnapshot> ReadCreatedBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Los cobros no leen recuentos de pedidos.");
    }

    private sealed class PuertoFijo(PaymentWindow respuesta) : IOrderPaymentAggregatePort
    {
        public bool Falla { get; init; }

        public Task<PaymentWindow> ReadRegisteredBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Falla) throw new InvalidOperationException("Cobros no respondió.");
            return Task.FromResult(respuesta);
        }
    }
}
