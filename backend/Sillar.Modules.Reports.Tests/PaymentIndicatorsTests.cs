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
/// M03: sin escritor acreditado, «no medible» y nunca 0; un origen no medible
/// nunca cuenta como cero ni deja afirmar cobertura total.
/// </summary>
public sealed class PaymentIndicatorsTests
{
    private const string Ruta = "/api/admin/reporting/payments";
    private const string Inicio = "2026-09-01T00:00:00-05:00";
    private const string Fin = "2026-10-01T00:00:00-05:00";
    private static readonly DateTimeOffset InicioUtc = new(2026, 9, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FinUtc = new(2026, 10, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Observado = new(2026, 10, 26, 15, 42, 10, TimeSpan.Zero);
    private static readonly string[] Indicadores = ["i05", "i06", "i07"];

    /// <summary>Lo que declara M03 hoy: sin escritor de proveedor. Lo del personal lo decide cada prueba.</summary>
    private static PaymentCapabilities Capacidades(bool personal = true, bool proveedor = false, bool origen = true, bool tardio = true, bool moneda = true)
        => new(origen, moneda, tardio, proveedor, personal);

    private static readonly PaymentMeasure SinEscritorDeProveedor = new(false, null, NotMeasurableReasons.ProviderWriterAbsent);

    [Fact]
    public async Task Sin_el_modulo_de_ventas_los_tres_indicadores_salen_fuente_no_disponible()
    {
        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: false, new PuertoFijo(Capacidades(), Cohorte())));

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
        Assert.Equal("not_measurable", cuerpo.GetProperty("i07").GetProperty("amountStatus").GetString());
        SinCifras(cuerpo);
    }

    [Fact]
    public async Task Sin_escritor_de_pagos_del_personal_acreditado_es_no_medible_y_ni_siquiera_se_consulta()
    {
        var puerto = new PuertoFijo(Capacidades(personal: false), Cohorte(staff: 0));

        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, puerto));

        Estados(cuerpo, "not_measurable");
        Assert.Equal("staff_writer_not_accredited", cuerpo.GetProperty("notMeasurableReason").GetString());
        SinCifras(cuerpo);
        Assert.Equal(0, puerto.Llamadas);
    }

    [Fact]
    public async Task Si_el_libro_no_guarda_el_origen_es_no_medible()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Capacidades(origen: false), Cohorte())));

        Estados(cuerpo, "not_measurable");
        Assert.Equal("source_not_recorded", cuerpo.GetProperty("notMeasurableReason").GetString());
    }

    [Fact]
    public async Task Con_el_proveedor_no_medible_un_cero_del_personal_no_es_un_periodo_vacio_ni_cobertura_total()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Capacidades(), Cohorte(staff: 0))));

        var i05 = cuerpo.GetProperty("i05");
        Assert.Equal("with_data", i05.GetProperty("status").GetString());
        Assert.Equal("partial", i05.GetProperty("coverage").GetString());
        Assert.Equal(0, i05.GetProperty("count").GetInt64());
        var origenes = i05.GetProperty("bySource").EnumerateArray().Select(s => (
            s.GetProperty("source").GetString(), s.GetProperty("status").GetString(),
            s.GetProperty("count").ValueKind == JsonValueKind.Null ? (long?)null : s.GetProperty("count").GetInt64(),
            s.GetProperty("reason").GetString())).ToArray();
        Assert.Equal([("staff", "measured", 0L, null), ("provider", "not_measurable", null, "provider_writer_absent")], origenes);
        Assert.Equal("partial", cuerpo.GetProperty("i06").GetProperty("coverage").GetString());
        Assert.Equal("with_data", cuerpo.GetProperty("i07").GetProperty("status").GetString());
        Assert.Equal("partial", cuerpo.GetProperty("i07").GetProperty("coverage").GetString());
        Assert.DoesNotContain("\"empty\"", cuerpo.GetRawText());
    }

    [Fact]
    public async Task Solo_con_todos_los_origenes_medidos_y_sin_pagos_los_tres_salen_vacios()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true,
            new PuertoFijo(Capacidades(proveedor: true), Cohorte(staff: 0, provider: 0))));

        Estados(cuerpo, "empty");
        Assert.Equal(0, cuerpo.GetProperty("i05").GetProperty("count").GetInt64());
        Assert.Equal("complete", cuerpo.GetProperty("i05").GetProperty("coverage").GetString());
    }

    [Fact]
    public async Task Separa_origen_moneda_y_tardanza_sin_sumar_monedas_y_sin_inventar_el_desglose_por_medio()
    {
        var cohorte = Cohorte(staff: 8, provider: 2, tardios: 3, sinMoneda: 2, ("USD", 1, 20.00m), ("PEN", 7, 735.5m));

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Capacidades(proveedor: true), cohorte)));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        var i05 = cuerpo.GetProperty("i05");
        Assert.Equal(10, i05.GetProperty("count").GetInt64());
        Assert.Equal("complete", i05.GetProperty("coverage").GetString());
        Assert.Equal([8L, 2L], i05.GetProperty("bySource").EnumerateArray().Select(s => s.GetProperty("count").GetInt64()));

        var i06 = cuerpo.GetProperty("i06");
        Assert.Equal("with_data", i06.GetProperty("status").GetString());
        Assert.Equal("partial", i06.GetProperty("coverage").GetString());
        Assert.Equal(2, i06.GetProperty("withoutCurrencyCount").GetInt64());
        Assert.Equal("not_published", i06.GetProperty("methodBreakdown").GetString());
        Assert.Equal([("PEN", 7L, "735.5"), ("USD", 1L, "20.00")],
            i06.GetProperty("byCurrency").EnumerateArray().Select(m => (
                m.GetProperty("currencyCode").GetString(), m.GetProperty("count").GetInt64(), m.GetProperty("amount").GetString())));

        var i07 = cuerpo.GetProperty("i07");
        Assert.Equal("with_data", i07.GetProperty("status").GetString());
        Assert.Equal(3, i07.GetProperty("count").GetInt64());
        Assert.Equal("not_measurable", i07.GetProperty("amountStatus").GetString());

        Assert.DoesNotContain("755.5", cuerpo.GetRawText());
    }

    [Fact]
    public async Task Aunque_M03_de_el_importe_completo_en_moneda_sin_el_proveedor_medido_la_cobertura_es_parcial()
    {
        var cohorte = Cohorte(staff: 2, sinMoneda: 0, monedas: [("PEN", 2, 50m)]);

        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Capacidades(), cohorte)));

        Assert.True(cohorte.AmountsComplete);
        Assert.Equal("partial", cuerpo.GetProperty("i06").GetProperty("coverage").GetString());
    }

    [Fact]
    public async Task Cobros_sin_ninguna_moneda_se_cuentan_y_su_importe_no_se_publica()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Capacidades(), Cohorte(staff: 2, sinMoneda: 2))));

        Assert.Equal("with_data", cuerpo.GetProperty("i05").GetProperty("status").GetString());
        Assert.Equal("currency_not_accredited", cuerpo.GetProperty("i06").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i06").GetProperty("byCurrency").ValueKind);
        Assert.Equal("none", cuerpo.GetProperty("i06").GetProperty("coverage").GetString());
    }

    [Fact]
    public async Task Sin_retraso_registrado_I07_es_no_medible_y_los_demas_siguen()
    {
        var cohorte = Cohorte(staff: 1, monedas: [("PEN", 1, 10m)]) with { Late = new(false, null, "late_not_recorded") };

        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Capacidades(tardio: false), cohorte)));

        Assert.Equal("with_data", cuerpo.GetProperty("i05").GetProperty("status").GetString());
        Assert.Equal("not_measurable", cuerpo.GetProperty("i07").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i07").GetProperty("count").ValueKind);
    }

    public static TheoryData<string> Incoherencias =>
        ["proveedor_medido_sin_escritor", "proveedor_no_medido_con_escritor", "medido_con_motivo", "no_medible_con_cifra",
         "tardio_sin_registro", "mas_tardios_que_pagos", "importes_no_suman", "completo_con_pagos_sin_moneda",
         "moneda_repetida", "moneda_minuscula", "importe_negativo", "moneda_sin_columna", "otro_periodo"];

    [Theory]
    [MemberData(nameof(Incoherencias))]
    public async Task Una_respuesta_de_cobros_que_no_cuadra_responde_503_sin_ninguna_cifra(string caso)
    {
        var capacidades = Capacidades();
        var cohorte = Cohorte(staff: 2, monedas: [("PEN", 2, 20m)]);
        switch (caso)
        {
            case "proveedor_medido_sin_escritor": cohorte = cohorte with { Provider = new(true, 0, null) }; break;
            case "proveedor_no_medido_con_escritor": capacidades = Capacidades(proveedor: true); break;
            case "medido_con_motivo": cohorte = cohorte with { Staff = new(true, 2, "provider_writer_absent") }; break;
            case "no_medible_con_cifra": cohorte = cohorte with { Provider = new(false, 0, "provider_writer_absent") }; break;
            case "tardio_sin_registro": capacidades = Capacidades(tardio: false); break;
            case "mas_tardios_que_pagos": cohorte = cohorte with { Late = new(true, 3, null) }; break;
            case "importes_no_suman": cohorte = Cohorte(staff: 3, monedas: [("PEN", 2, 20m)]); break;
            case "completo_con_pagos_sin_moneda": cohorte = Cohorte(staff: 3, sinMoneda: 1, monedas: [("PEN", 2, 20m)]) with { AmountsComplete = true }; break;
            case "moneda_repetida": cohorte = Cohorte(staff: 2, monedas: [("PEN", 1, 10m), ("PEN", 1, 10m)]); break;
            case "moneda_minuscula": cohorte = Cohorte(staff: 2, monedas: [("pen", 2, 20m)]); break;
            case "importe_negativo": cohorte = Cohorte(staff: 2, monedas: [("PEN", 2, -20m)]); break;
            case "moneda_sin_columna": capacidades = Capacidades(moneda: false); break;
            default: cohorte = cohorte with { StartInclusive = InicioUtc.AddHours(-1) }; break;
        }

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(capacidades, cohorte)));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
        Assert.Contains("no cuadran", cuerpo.GetProperty("title").GetString());
        Assert.False(cuerpo.TryGetProperty("i05", out _));
    }

    [Fact]
    public async Task Un_fallo_del_lector_de_cobros_responde_503_y_no_cero()
    {
        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Capacidades(), Cohorte()) { Falla = true }));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
        Assert.False(cuerpo.TryGetProperty("i05", out _));
    }

    [Fact]
    public async Task Una_consulta_cancelada_se_propaga()
    {
        using var cancelada = new CancellationTokenSource();
        cancelada.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Consultar(Aplicacion(ventas: true, new PuertoFijo(Capacidades(), Cohorte())), cancelada.Token));
    }

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
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i06").GetProperty("withoutCurrencyCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i07").GetProperty("count").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("observedAtUtc").ValueKind);
    }

    /// <summary>
    /// Una cohorte como la produce el reductor de M03: personal medido, proveedor
    /// no medible salvo que se dé su recuento, tardíos medidos, importe completo si
    /// y solo si no hay pagos sin moneda.
    /// </summary>
    private static PaymentCohort Cohorte(long staff = 0, long? provider = null, long tardios = 0, long sinMoneda = 0,
        params (string Moneda, long Pagos, decimal Importe)[] monedas)
        => new(InicioUtc, FinUtc, Observado,
            new(true, staff, null),
            provider is { } p ? new(true, p, null) : SinEscritorDeProveedor,
            new(true, tardios, null),
            [.. monedas.Select(m => new PaymentCurrencyGroup(m.Moneda, m.Pagos, m.Importe))],
            sinMoneda,
            sinMoneda == 0);

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

    private sealed class PuertoFijo(PaymentCapabilities capacidades, PaymentCohort respuesta) : IOrderPaymentAggregatePort
    {
        public bool Falla { get; init; }
        public int Llamadas { get; private set; }

        public PaymentCapabilities Capabilities => capacidades;

        public Task<PaymentCohort> ReadRegisteredBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
        {
            Llamadas++;
            cancellationToken.ThrowIfCancellationRequested();
            if (Falla) throw new InvalidOperationException("Cobros no respondió.");
            return Task.FromResult(respuesta);
        }
    }
}
