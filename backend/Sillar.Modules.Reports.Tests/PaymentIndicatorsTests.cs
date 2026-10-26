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
/// I05–I07 por la ruta que monta el módulo: sin capacidad acreditada por ventas,
/// «no medible» y nunca 0; con ella, origen, medio, moneda y tardanza separados.
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

    [Fact]
    public async Task Sin_el_modulo_de_ventas_los_tres_indicadores_salen_fuente_no_disponible()
    {
        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: false, new PuertoFijo(Medible(0))));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        foreach (var indicador in Indicadores)
        {
            Assert.Equal("source_unavailable", cuerpo.GetProperty(indicador).GetProperty("status").GetString());
        }
        SinCifras(cuerpo);
    }

    [Fact]
    public async Task Con_ventas_y_sin_lector_de_cobros_los_tres_son_no_medibles_y_ninguno_es_cero()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, puerto: null));

        foreach (var indicador in Indicadores)
        {
            Assert.Equal("not_measurable", cuerpo.GetProperty(indicador).GetProperty("status").GetString());
        }
        Assert.Equal("not_measurable", cuerpo.GetProperty("i07").GetProperty("amountStatus").GetString());
        SinCifras(cuerpo);
    }

    [Fact]
    public async Task Si_ventas_no_acredita_la_capacidad_es_no_medible_aunque_responda_un_cero()
    {
        var sinEscritor = Medible(0) with { Capability = OrderPaymentCapability.NotMeasurable };

        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(sinEscritor)));

        Assert.Equal("not_measurable", cuerpo.GetProperty("i05").GetProperty("status").GetString());
        SinCifras(cuerpo);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Una_respuesta_medible_sin_cobertura_completa_es_no_medible(bool sinRecuento, bool sinGrupos)
    {
        var incompleta = Medible(0) with
        {
            RegisteredCount = sinRecuento ? null : 0,
            Groups = sinGrupos ? null : [],
        };

        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(incompleta)));

        Assert.Equal("not_measurable", cuerpo.GetProperty("i05").GetProperty("status").GetString());
        SinCifras(cuerpo);
    }

    [Fact]
    public async Task Solo_con_capacidad_acreditada_y_sin_cobros_los_tres_salen_vacios_con_cero()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Medible(0))));

        Assert.Equal("empty", cuerpo.GetProperty("i05").GetProperty("status").GetString());
        Assert.Equal(0, cuerpo.GetProperty("i05").GetProperty("count").GetInt64());
        Assert.Equal("empty", cuerpo.GetProperty("i06").GetProperty("status").GetString());
        Assert.Equal("empty", cuerpo.GetProperty("i07").GetProperty("status").GetString());
        Assert.Equal(0, cuerpo.GetProperty("i07").GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task Separa_origen_medio_moneda_y_tardanza_sin_sumar_monedas_distintas()
    {
        var cohorte = Medible(11,
            ("yape", "staff", "PEN", false, 4, 400.00m),
            ("yape", "provider", "PEN", false, 2, 150m),
            ("efectivo", "staff", "USD", true, 1, 20.00m),
            ("efectivo", "staff", null, true, 3, null),
            ("yape", null, "PEN", true, 1, 35.5m));

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(cohorte)));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        var i05 = cuerpo.GetProperty("i05");
        Assert.Equal("with_data", i05.GetProperty("status").GetString());
        Assert.Equal(11, i05.GetProperty("count").GetInt64());
        Assert.Equal([("staff", 8L), ("provider", 2L), ("unrecorded", 1L)],
            i05.GetProperty("bySource").EnumerateArray().Select(s => (s.GetProperty("source").GetString(), s.GetProperty("count").GetInt64())));

        var i06 = cuerpo.GetProperty("i06");
        Assert.Equal("with_data", i06.GetProperty("status").GetString());
        Assert.Equal("partial", i06.GetProperty("coverage").GetString());
        Assert.Equal(
            [("efectivo", "staff", "USD", 1L, "20.00"), ("yape", "staff", "PEN", 4L, "400.00"),
             ("yape", "provider", "PEN", 2L, "150"), ("yape", "unrecorded", "PEN", 1L, "35.5")],
            i06.GetProperty("byMethod").EnumerateArray().Select(m => (
                m.GetProperty("method").GetString(), m.GetProperty("source").GetString(), m.GetProperty("currencyCode").GetString(),
                m.GetProperty("count").GetInt64(), m.GetProperty("amount").GetString())));
        Assert.Equal([("efectivo", "staff", 3L)],
            i06.GetProperty("withoutCurrency").EnumerateArray().Select(m => (
                m.GetProperty("method").GetString(), m.GetProperty("source").GetString(), m.GetProperty("count").GetInt64())));

        var i07 = cuerpo.GetProperty("i07");
        Assert.Equal("with_data", i07.GetProperty("status").GetString());
        Assert.Equal(5, i07.GetProperty("count").GetInt64());
        Assert.Equal("with_data", i07.GetProperty("amountStatus").GetString());
        Assert.Equal([("PEN", 1L, "35.5"), ("USD", 1L, "20.00")],
            i07.GetProperty("amountByCurrency").EnumerateArray().Select(m => (
                m.GetProperty("currencyCode").GetString(), m.GetProperty("count").GetInt64(), m.GetProperty("amount").GetString())));
        Assert.Equal(3, i07.GetProperty("withoutCurrencyCount").GetInt64());

        // Ninguna suma entre PEN y USD, ni en I06 ni en I07.
        Assert.DoesNotContain("605.5", cuerpo.GetRawText());
        Assert.DoesNotContain("55.5", cuerpo.GetRawText());
    }

    [Fact]
    public async Task Cobros_sin_ninguna_moneda_se_cuentan_y_su_importe_no_se_publica()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true,
            new PuertoFijo(Medible(2, ("yape", "staff", null, false, 2, null)))));

        Assert.Equal("with_data", cuerpo.GetProperty("i05").GetProperty("status").GetString());
        Assert.Equal("currency_not_accredited", cuerpo.GetProperty("i06").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i06").GetProperty("byMethod").ValueKind);
        Assert.Equal("none", cuerpo.GetProperty("i06").GetProperty("coverage").GetString());
        Assert.Equal("empty", cuerpo.GetProperty("i07").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Los_tardios_sin_moneda_se_cuentan_y_su_importe_no_esta_acreditado()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Medible(4,
            ("yape", "staff", "PEN", false, 1, 10m), ("efectivo", "staff", null, true, 3, null)))));

        var i07 = cuerpo.GetProperty("i07");
        Assert.Equal("with_data", i07.GetProperty("status").GetString());
        Assert.Equal(3, i07.GetProperty("count").GetInt64());
        Assert.Equal("currency_not_accredited", i07.GetProperty("amountStatus").GetString());
        Assert.Equal(JsonValueKind.Null, i07.GetProperty("amountByCurrency").ValueKind);
    }

    public static TheoryData<string> Incoherencias =>
        ["origen_desconocido", "medio_vacio", "medio_mayusculas", "grupo_repetido", "no_suma",
         "importe_sin_moneda", "reverso_negativo", "otro_periodo"];

    [Theory]
    [MemberData(nameof(Incoherencias))]
    public async Task Una_respuesta_de_cobros_que_no_cuadra_responde_503_sin_ninguna_cifra(string caso)
    {
        var cohorte = caso switch
        {
            "origen_desconocido" => Medible(1, ("yape", "cliente", "PEN", false, 1, 10m)),
            "medio_vacio" => Medible(1, ("", "staff", "PEN", false, 1, 10m)),
            "medio_mayusculas" => Medible(1, ("Yape", "staff", "PEN", false, 1, 10m)),
            "grupo_repetido" => Medible(2, ("yape", "staff", "PEN", false, 1, 10m), ("yape", "staff", "PEN", false, 1, 10m)),
            "no_suma" => Medible(3, ("yape", "staff", "PEN", false, 1, 10m)),
            "importe_sin_moneda" => Medible(1, ("yape", "staff", null, false, 1, 10m)),
            "reverso_negativo" => Medible(1, ("yape", "staff", "PEN", false, 1, -10m)),
            _ => Medible(0) with { StartInclusive = InicioUtc.AddHours(-1) },
        };

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(cohorte)));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
        Assert.Contains("no cuadran", cuerpo.GetProperty("title").GetString());
        Assert.False(cuerpo.TryGetProperty("i05", out _));
    }

    [Fact]
    public async Task Un_fallo_del_lector_de_cobros_responde_503_y_no_cero()
    {
        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Medible(0)) { Falla = true }));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
        Assert.False(cuerpo.TryGetProperty("i05", out _));
    }

    [Fact]
    public async Task Una_consulta_cancelada_se_propaga()
    {
        using var cancelada = new CancellationTokenSource();
        cancelada.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Consultar(Aplicacion(ventas: true, new PuertoFijo(Medible(0))), cancelada.Token));
    }

    private static void SinCifras(JsonElement cuerpo)
    {
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i05").GetProperty("count").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i05").GetProperty("bySource").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i06").GetProperty("byMethod").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i07").GetProperty("count").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("observedAtUtc").ValueKind);
    }

    private static OrderPaymentAggregate Medible(long registrados,
        params (string Medio, string? Origen, string? Moneda, bool Tardio, long Pagos, decimal? Importe)[] grupos)
        => new(InicioUtc, FinUtc, Observado, OrderPaymentCapability.Measurable, registrados,
            [.. grupos.Select(g => new OrderPaymentGroup(g.Medio, g.Origen, g.Moneda, g.Tardio, g.Pagos, g.Importe))]);

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

    private sealed class PuertoFijo(OrderPaymentAggregate respuesta) : IOrderPaymentAggregatePort
    {
        public bool Falla { get; init; }

        public Task<OrderPaymentAggregate> ReadRegisteredBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Falla) throw new InvalidOperationException("Cobros no respondió.");
            return Task.FromResult(respuesta);
        }
    }
}
