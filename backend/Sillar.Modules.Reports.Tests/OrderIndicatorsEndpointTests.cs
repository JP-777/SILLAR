using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Reports.Tests;

/// <summary>
/// El endpoint tal como lo monta el host: servicios y rutas salen de
/// <see cref="ReportsModule"/>, no de un registro hecho a mano. Se ejecuta el
/// delegado de la ruta sin servidor ni base de datos; los 401/403 por HTTP
/// son e2e y aquí solo se lee la política declarada.
/// </summary>
public sealed class OrderIndicatorsEndpointTests
{
    private const string Ruta = "/api/admin/reporting/orders";
    private const string Inicio = "2026-10-01T00:00:00-05:00";
    private const string Fin = "2026-10-08T00:00:00-05:00";

    [Fact]
    public void El_modulo_expone_sus_rutas_y_todas_exigen_el_rol_admin()
    {
        var rutas = Endpoints(Aplicacion(lector: null));

        Assert.Equal(
            [("GET", Ruta), ("GET", "/api/admin/reporting/orders/value"), ("GET", "/api/admin/reporting/payments"), ("GET", "/api/admin/reporting/installation-zone"), ("POST", "/api/admin/reporting/orders/export")],
            rutas.Select(r => (Assert.Single(r.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods), r.RoutePattern.RawText)).ToArray());
        foreach (var ruta in rutas)
        {
            Assert.Equal(["admin"], ruta.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy));
        }
    }

    [Fact]
    public async Task Sin_el_modulo_de_ventas_responde_fuente_no_disponible_con_metricas_nulas()
    {
        var (codigo, cuerpo) = await Consultar(Aplicacion(lector: null), Inicio, Fin);

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("source_unavailable", cuerpo.GetProperty("status").GetString());
        foreach (var metrica in new[] { "observedAtUtc", "i01CreatedOrders", "i03CurrentStatusCounts",
                     "i04CurrentlyExpiredOrders", "activeOrders", "inactiveOrders" })
        {
            Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty(metrica).ValueKind);
        }
    }

    [Fact]
    public async Task Una_cohorte_realmente_vacia_responde_empty_con_ceros_y_no_fuente_ausente()
    {
        var (codigo, cuerpo) = await Consultar(Aplicacion(new LectorFijo(creados: 0)), Inicio, Fin);

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("empty", cuerpo.GetProperty("status").GetString());
        Assert.Equal(0, cuerpo.GetProperty("i01CreatedOrders").GetInt64());
        Assert.Equal(0, cuerpo.GetProperty("i04CurrentlyExpiredOrders").GetInt64());
        Assert.Equal(0, cuerpo.GetProperty("activeOrders").GetInt64());
        Assert.Equal(OrderStatus.All.Length, cuerpo.GetProperty("i03CurrentStatusCounts").GetArrayLength());
        Assert.NotEqual(JsonValueKind.Null, cuerpo.GetProperty("observedAtUtc").ValueKind);
    }

    [Fact]
    public async Task Una_cohorte_con_pedidos_responde_with_data()
    {
        var (_, cuerpo) = await Consultar(Aplicacion(new LectorFijo(creados: 3)), Inicio, Fin);

        Assert.Equal("with_data", cuerpo.GetProperty("status").GetString());
        Assert.Equal(3, cuerpo.GetProperty("i01CreatedOrders").GetInt64());
    }

    [Theory]
    [InlineData("2026-10-01T00:00:00", Fin)]          // sin desplazamiento: zona implícita
    [InlineData(Inicio, "")]                          // fin ausente
    [InlineData("2026-02-30T00:00:00Z", Fin)]         // fecha imposible
    [InlineData(Fin, Inicio)]                         // fin anterior al inicio
    [InlineData(Inicio, "2026-10-01T05:00:00Z")]      // mismo instante en otra zona
    public async Task Un_periodo_invalido_responde_400_sin_consultar_ventas(string inicio, string fin)
    {
        var lector = new LectorFijo(creados: 3);

        var (codigo, cuerpo) = await Consultar(Aplicacion(lector), inicio, fin);

        Assert.Equal(StatusCodes.Status400BadRequest, codigo);
        Assert.True(cuerpo.GetProperty("errors").TryGetProperty("periodo", out _));
        Assert.Equal(0, lector.Llamadas);
    }

    [Fact]
    public async Task Un_fallo_de_ventas_responde_503_explicado_sin_ninguna_cifra()
    {
        var (codigo, cuerpo) = await Consultar(Aplicacion(new LectorFijo(creados: 3) { Falla = true }), Inicio, Fin);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
        Assert.Contains("ventas no respondió", cuerpo.GetProperty("title").GetString());
        Assert.Contains("no significa cero", cuerpo.GetProperty("detail").GetString());
        // ProblemDetails, no la vista: ni «empty», ni ceros, ni fuente ausente.
        Assert.Equal(503, cuerpo.GetProperty("status").GetInt32());
        foreach (var campo in new[] { "startInclusive", "observedAtUtc", "i01CreatedOrders", "i03CurrentStatusCounts", "i04CurrentlyExpiredOrders" })
        {
            Assert.False(cuerpo.TryGetProperty(campo, out _), campo);
        }
        // El texto técnico de la excepción va al registro, no a la respuesta.
        Assert.DoesNotContain("Ventas no respondió.", cuerpo.GetRawText());
    }

    [Fact]
    public async Task Una_consulta_cancelada_no_se_disfraza_de_fallo_de_ventas()
    {
        using var cancelada = new CancellationTokenSource();
        cancelada.Cancel();
        var app = Aplicacion(new LectorFijo(creados: 3));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Consultar(app, Inicio, Fin, cancelada.Token));
    }

    /// <summary>
    /// D-M10-05: la exportación se audita con la acción propia de CORE, y eso
    /// solo lo hace <see cref="Sillar.Core.Contracts.IExportAudit"/>. M10 nunca
    /// escribe en la auditoría directamente: ninguna exportación con una acción
    /// prestada ni con un resumen compuesto aquí.
    /// </summary>
    [Fact]
    public void Reportes_no_escribe_en_la_auditoria_directamente_solo_por_la_auditoria_de_exportaciones()
    {
        const BindingFlags todo = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var usos = typeof(ReportsModule).Assembly.GetTypes().SelectMany(tipo =>
            tipo.GetFields(todo).Select(f => (tipo, f.Name, f.FieldType))
                .Concat(tipo.GetMethods(todo).Cast<MethodBase>().Concat(tipo.GetConstructors(todo))
                    .SelectMany(m => m.GetParameters().Select(p => (tipo, m.Name, p.ParameterType)))))
            .Where(uso => uso.Item3 == typeof(Sillar.Core.Contracts.IAuditWriter))
            .Select(uso => $"{uso.tipo.FullName}.{uso.Name}");

        Assert.Empty(usos);
    }

    private static WebApplication Aplicacion(IOrderAggregateReader? lector)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.ConPermisoDePrueba();
        builder.Services.AddRouting();
        // Ventas registra su lector solo cuando está activo; aquí, igual.
        if (lector is not null) builder.Services.AddSingleton(lector);
        new ReportsModule().RegisterServices(builder.Services, new ConfigurationBuilder().Build());
        var app = builder.Build();
        new ReportsModule().MapEndpoints(app);
        return app;
    }

    private static IReadOnlyList<RouteEndpoint> Endpoints(WebApplication app) =>
        [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>()];

    private static async Task<(int Codigo, JsonElement Cuerpo)> Consultar(
        WebApplication app, string inicio, string fin, CancellationToken abortada = default)
    {
        var ruta = Endpoints(app).Single(r => r.RoutePattern.RawText == Ruta);
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

    private sealed class LectorFijo(long creados) : IOrderAggregateReader
    {
        public bool Falla { get; init; }
        public int Llamadas { get; private set; }

        public Task<OrderAggregateSnapshot> ReadCreatedBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
        {
            Llamadas++;
            if (Falla) throw new InvalidOperationException("Ventas no respondió.");
            cancellationToken.ThrowIfCancellationRequested();
            var porEstado = OrderStatus.All
                .Select(status => new OrderStatusCount(status, status == OrderStatus.PendingPayment ? creados : 0))
                .ToArray();
            return Task.FromResult(new OrderAggregateSnapshot(
                startInclusive, endExclusive, DateTimeOffset.UtcNow, creados, porEstado, 0,
                new OrderActivityCounts(creados, 0)));
        }
    }
}
