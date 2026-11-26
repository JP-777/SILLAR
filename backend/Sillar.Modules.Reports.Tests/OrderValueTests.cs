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
/// I02 por la ruta que monta el módulo: una suma por moneda, los pedidos sin
/// moneda contados aparte y ningún 0 que no venga de una cohorte vacía.
/// </summary>
public sealed class OrderValueTests
{
    private const string Ruta = "/api/admin/reporting/orders/value";
    private const string Inicio = "2026-09-01T00:00:00-05:00";
    private const string Fin = "2026-10-01T00:00:00-05:00";
    private static readonly DateTimeOffset InicioUtc = new(2026, 9, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FinUtc = new(2026, 10, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Observado = new(2026, 10, 26, 15, 42, 10, TimeSpan.Zero);

    [Fact]
    public async Task Sin_el_modulo_de_ventas_I02_sale_fuente_no_disponible_y_sin_ninguna_cifra()
    {
        var puerto = new PuertoFijo(Cohorte(57, sinMoneda: (57, 0)));

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: false, puerto));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("source_unavailable", cuerpo.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("createdCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("byCurrency").ValueKind);
        Assert.Equal(0, puerto.Llamadas);
    }

    [Fact]
    public async Task Con_ventas_pero_sin_lector_de_valor_I02_es_no_medible_y_nunca_cero()
    {
        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, puerto: null));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("not_measurable", cuerpo.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("createdCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("withoutCurrency").ValueKind);
    }

    [Fact]
    public async Task Una_cohorte_con_PEN_USD_y_pedidos_sin_moneda_publica_cada_moneda_con_activos_y_bajas_separados()
    {
        var puerto = new PuertoFijo(Cohorte(57, sinMoneda: (10, 3),
            ("USD", 3, 310.00m, 0, 0m), ("PEN", 39, 12340.50m, 2, 500m)));

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, puerto));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("with_data", cuerpo.GetProperty("status").GetString());
        Assert.Equal("partial", cuerpo.GetProperty("coverage").GetString());
        Assert.Equal(57, cuerpo.GetProperty("createdCount").GetInt64());
        Assert.Equal(10, cuerpo.GetProperty("withoutCurrency").GetProperty("active").GetInt64());
        Assert.Equal(3, cuerpo.GetProperty("withoutCurrency").GetProperty("inactive").GetInt64());
        var monedas = cuerpo.GetProperty("byCurrency").EnumerateArray()
            .Select(m => (m.GetProperty("currencyCode").GetString(),
                m.GetProperty("activeCount").GetInt64(), m.GetProperty("activeAmount").GetString(),
                m.GetProperty("inactiveCount").GetInt64(), m.GetProperty("inactiveAmount").GetString()))
            .ToArray();
        Assert.Equal([("PEN", 39L, "12340.50", 2L, "500"), ("USD", 3L, "310.00", 0L, "0")], monedas);
        // Ni un total entre monedas, ni activos y bajas sumados, ni importe para lo que no tiene moneda.
        Assert.DoesNotContain("12840.5", cuerpo.GetRawText());
        Assert.DoesNotContain("13150.5", cuerpo.GetRawText());
        Assert.False(cuerpo.GetProperty("withoutCurrency").TryGetProperty("amount", out _));
    }

    [Fact]
    public async Task Una_cohorte_con_pedidos_y_ninguna_moneda_los_cuenta_y_no_publica_importe()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Cohorte(13, sinMoneda: (12, 1)))));

        Assert.Equal("currency_not_accredited", cuerpo.GetProperty("status").GetString());
        Assert.Equal("none", cuerpo.GetProperty("coverage").GetString());
        Assert.Equal(13, cuerpo.GetProperty("createdCount").GetInt64());
        Assert.Equal(12, cuerpo.GetProperty("withoutCurrency").GetProperty("active").GetInt64());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("byCurrency").ValueKind);
    }

    [Fact]
    public async Task Solo_una_cohorte_medida_y_sin_pedidos_publica_cero()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Cohorte(0))));

        Assert.Equal("empty", cuerpo.GetProperty("status").GetString());
        Assert.Equal(0, cuerpo.GetProperty("createdCount").GetInt64());
        Assert.Empty(cuerpo.GetProperty("byCurrency").EnumerateArray());
    }

    [Fact]
    public async Task Con_todas_las_monedas_registradas_la_cobertura_es_completa()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Cohorte(2, sinMoneda: (0, 0), ("PEN", 2, 80m, 0, 0m)))));

        Assert.Equal("with_data", cuerpo.GetProperty("status").GetString());
        Assert.Equal("complete", cuerpo.GetProperty("coverage").GetString());
    }

    public static TheoryData<string> Incoherencias =>
        ["moneda_repetida", "moneda_nula", "moneda_minuscula", "moneda_no_iso", "importe_negativo", "baja_negativa",
         "no_suma", "otro_periodo", "recuento_negativo", "sin_moneda_negativo", "importe_sin_pedidos", "moneda_sin_pedidos"];

    [Theory]
    [MemberData(nameof(Incoherencias))]
    public async Task Una_respuesta_de_ventas_que_no_cuadra_responde_503_sin_ninguna_cifra(string caso)
    {
        var cohorte = caso switch
        {
            "moneda_repetida" => Cohorte(5, (0, 0), ("PEN", 2, 10m, 0, 0m), ("PEN", 3, 20m, 0, 0m)),
            "moneda_nula" => Cohorte(5, (0, 0), (null!, 5, 10m, 0, 0m)),
            "moneda_minuscula" => Cohorte(5, (0, 0), ("pen", 5, 10m, 0, 0m)),
            "moneda_no_iso" => Cohorte(5, (0, 0), ("S/.", 5, 10m, 0, 0m)),
            "importe_negativo" => Cohorte(5, (0, 0), ("PEN", 5, -1m, 0, 0m)),
            "baja_negativa" => Cohorte(5, (0, 0), ("PEN", 4, 10m, 1, -1m)),
            "no_suma" => Cohorte(6, (0, 0), ("PEN", 5, 10m, 0, 0m)),
            "otro_periodo" => Cohorte(5, (0, 0), ("PEN", 5, 10m, 0, 0m)) with { EndExclusive = FinUtc.AddDays(1) },
            "recuento_negativo" => Cohorte(-1, (0, 0), ("PEN", -1, 0m, 0, 0m)),
            "sin_moneda_negativo" => Cohorte(0, (2, -2)),
            "importe_sin_pedidos" => Cohorte(1, (0, 0), ("PEN", 1, 10m, 0, 5m)),
            _ => Cohorte(0, (0, 0), ("PEN", 0, 0m, 0, 0m)),
        };

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(cohorte)));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
        Assert.Contains("no cuadran", cuerpo.GetProperty("title").GetString());
        Assert.False(cuerpo.TryGetProperty("createdCount", out _));
    }

    [Fact]
    public async Task Un_fallo_del_lector_de_valor_responde_503_y_no_cero()
    {
        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Cohorte(0)) { Falla = true }));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
        Assert.Contains("no respondió", cuerpo.GetProperty("title").GetString());
        Assert.DoesNotContain("Valor no respondió.", cuerpo.GetRawText());
    }

    [Fact]
    public async Task Una_consulta_cancelada_se_propaga_y_no_se_disfraza_de_fallo_de_ventas()
    {
        using var cancelada = new CancellationTokenSource();
        cancelada.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Consultar(Aplicacion(ventas: true, new PuertoFijo(Cohorte(0))), abortada: cancelada.Token));
    }

    [Theory]
    [InlineData("2026-09-01T00:00:00", Fin)]
    [InlineData(Fin, Inicio)]
    public async Task Un_periodo_invalido_responde_400_sin_consultar_ventas(string inicio, string fin)
    {
        var puerto = new PuertoFijo(Cohorte(0));

        var (codigo, _) = await Consultar(Aplicacion(ventas: true, puerto), inicio, fin);

        Assert.Equal(StatusCodes.Status400BadRequest, codigo);
        Assert.Equal(0, puerto.Llamadas);
    }

    [Fact]
    public async Task El_lector_recibe_el_periodo_en_UTC_con_el_fin_excluido()
    {
        var puerto = new PuertoFijo(Cohorte(0));

        await Consultar(Aplicacion(ventas: true, puerto));

        Assert.Equal((InicioUtc, FinUtc), puerto.Periodo);
        Assert.Equal(TimeSpan.Zero, puerto.Periodo.Inicio.Offset);
    }

    private static OrderValueCohort Cohorte(long total, (long Activos, long Bajas) sinMoneda = default,
        params (string Moneda, long Activos, decimal ImporteActivos, long Bajas, decimal ImporteBajas)[] monedas)
        => new(InicioUtc, FinUtc, Observado, total,
            [.. monedas.Select(m => new OrderValueCurrencyGroup(m.Moneda, m.Activos, m.ImporteActivos, m.Bajas, m.ImporteBajas))],
            sinMoneda.Activos, sinMoneda.Bajas);

    private static WebApplication Aplicacion(bool ventas, IOrderValueAggregatePort? puerto)
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

    private static async Task<(int Codigo, JsonElement Cuerpo)> Consultar(
        WebApplication app, string inicio = Inicio, string fin = Fin, CancellationToken abortada = default)
    {
        var ruta = ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>()
            .Single(r => r.RoutePattern.RawText == Ruta);
        await using var scope = app.Services.CreateAsyncScope();
        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = abortada };
        contexto.Request.Method = "GET";
        contexto.Request.Path = Ruta;
        contexto.Request.QueryString = QueryString.Create(new Dictionary<string, string?> { ["start"] = inicio, ["end"] = fin });
        using var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await ruta.RequestDelegate!(contexto);

        return (contexto.Response.StatusCode, JsonDocument.Parse(salida.ToArray()).RootElement.Clone());
    }

    /// <summary>Solo su presencia importa: dice que ventas está instalado.</summary>
    private sealed class VentasPresente : IOrderAggregateReader
    {
        public Task<OrderAggregateSnapshot> ReadCreatedBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
            => throw new InvalidOperationException("I02 no debe leer recuentos de I01.");
    }

    private sealed class PuertoFijo(OrderValueCohort respuesta) : IOrderValueAggregatePort
    {
        public bool Falla { get; init; }
        public int Llamadas { get; private set; }
        public (DateTimeOffset Inicio, DateTimeOffset Fin) Periodo { get; private set; }

        public Task<OrderValueCohort> ReadCreatedBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
        {
            Llamadas++;
            Periodo = (startInclusive, endExclusive);
            cancellationToken.ThrowIfCancellationRequested();
            if (Falla) throw new InvalidOperationException("Valor no respondió.");
            return Task.FromResult(respuesta);
        }
    }
}
