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
        var puerto = new PuertoFijo(Cohorte(57, (null, 57, null)));

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
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("withoutCurrencyCount").ValueKind);
    }

    [Fact]
    public async Task Una_cohorte_con_PEN_USD_y_pedidos_sin_moneda_publica_una_suma_por_moneda_y_ninguna_entre_monedas()
    {
        var puerto = new PuertoFijo(Cohorte(57, ("USD", 3, 310.00m), (null, 13, null), ("PEN", 41, 12840.50m)));

        var (codigo, cuerpo) = await Consultar(Aplicacion(ventas: true, puerto));

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("with_data", cuerpo.GetProperty("status").GetString());
        Assert.Equal("partial", cuerpo.GetProperty("coverage").GetString());
        Assert.Equal(57, cuerpo.GetProperty("createdCount").GetInt64());
        Assert.Equal(13, cuerpo.GetProperty("withoutCurrencyCount").GetInt64());
        var monedas = cuerpo.GetProperty("byCurrency").EnumerateArray()
            .Select(m => (m.GetProperty("currencyCode").GetString(), m.GetProperty("count").GetInt64(), m.GetProperty("amount").GetString()))
            .ToArray();
        Assert.Equal([("PEN", 41L, "12840.50"), ("USD", 3L, "310.00")], monedas);
        // Ni un total entre monedas ni un importe atribuido a los pedidos sin moneda.
        Assert.DoesNotContain("13150.5", cuerpo.GetRawText());
        Assert.False(cuerpo.TryGetProperty("amount", out _));
    }

    [Fact]
    public async Task Una_cohorte_con_pedidos_y_ninguna_moneda_los_cuenta_y_no_publica_importe()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Cohorte(13, (null, 13, null)))));

        Assert.Equal("currency_not_accredited", cuerpo.GetProperty("status").GetString());
        Assert.Equal("none", cuerpo.GetProperty("coverage").GetString());
        Assert.Equal(13, cuerpo.GetProperty("createdCount").GetInt64());
        Assert.Equal(13, cuerpo.GetProperty("withoutCurrencyCount").GetInt64());
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
    public async Task Con_una_sola_moneda_la_cobertura_es_completa()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(ventas: true, new PuertoFijo(Cohorte(2, ("PEN", 2, 80m)))));

        Assert.Equal("with_data", cuerpo.GetProperty("status").GetString());
        Assert.Equal("complete", cuerpo.GetProperty("coverage").GetString());
        Assert.Equal(0, cuerpo.GetProperty("withoutCurrencyCount").GetInt64());
    }

    public static TheoryData<string> Incoherencias =>
        ["moneda_repetida", "sin_moneda_repetido", "importe_sin_moneda", "moneda_sin_importe", "moneda_minuscula",
         "moneda_no_iso", "importe_negativo", "no_suma", "otro_periodo", "recuento_negativo", "importe_sin_pedidos"];

    [Theory]
    [MemberData(nameof(Incoherencias))]
    public async Task Una_respuesta_de_ventas_que_no_cuadra_responde_503_sin_ninguna_cifra(string caso)
    {
        var cohorte = caso switch
        {
            "moneda_repetida" => Cohorte(5, ("PEN", 2, 10m), ("PEN", 3, 20m)),
            "sin_moneda_repetido" => Cohorte(5, (null, 2, null), (null, 3, null)),
            "importe_sin_moneda" => Cohorte(5, (null, 5, 99m)),
            "moneda_sin_importe" => Cohorte(5, ("PEN", 5, null)),
            "moneda_minuscula" => Cohorte(5, ("pen", 5, 10m)),
            "moneda_no_iso" => Cohorte(5, ("S/.", 5, 10m)),
            "importe_negativo" => Cohorte(5, ("PEN", 5, -1m)),
            "no_suma" => Cohorte(6, ("PEN", 5, 10m)),
            "otro_periodo" => Cohorte(5, ("PEN", 5, 10m)) with { EndExclusive = FinUtc.AddDays(1) },
            "recuento_negativo" => Cohorte(-1, ("PEN", -1, 0m)),
            _ => Cohorte(0, ("PEN", 0, 5m)),
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

    private static OrderValueAggregate Cohorte(long creados, params (string? Moneda, long Pedidos, decimal? Importe)[] grupos)
        => new(InicioUtc, FinUtc, Observado, creados,
            [.. grupos.Select(g => new OrderValueGroup(g.Moneda, g.Pedidos, g.Importe))]);

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

    private sealed class PuertoFijo(OrderValueAggregate respuesta) : IOrderValueAggregatePort
    {
        public bool Falla { get; init; }
        public int Llamadas { get; private set; }
        public (DateTimeOffset Inicio, DateTimeOffset Fin) Periodo { get; private set; }

        public Task<OrderValueAggregate> ReadCreatedBetweenAsync(
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
