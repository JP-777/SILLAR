using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sillar.Modules.Payments.Application;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Pedidos;
using Sillar.Shared.Replication;
using static Sillar.Host.CommerceBridge.Tests.ComposicionVerticalTests;

namespace Sillar.Host.CommerceBridge.Tests;

/// <summary>El SQL que los productores reales de M03 tienen que emitir, y ningún otro.</summary>
internal static class SqlEsperado
{
    public const string I01I03I04 =
        "SELECT o.status AS \"Status\", o.is_active AS \"IsActive\", count(*) AS \"Count\"\n"
        + "FROM sales.orders AS o\n"
        + "WHERE o.created_at >= @startUtc AND o.created_at < @endUtc\n"
        + "GROUP BY o.status, o.is_active";

    public const string I05I07 =
        "SELECT o.source, o.method, o.currency_code, o.was_late, count(*), COALESCE(sum(o.amount), 0.0)\n"
        + "FROM sales.order_payments AS o\n"
        + "WHERE o.registered_at >= @inicio AND o.registered_at < @fin\n"
        + "GROUP BY o.source, o.method, o.currency_code, o.was_late";

    public const string CorrelacionPorCodigo =
        "SELECT o.order_id, o.order_code\nFROM sales.orders AS o\nWHERE o.order_code = @codigo\nLIMIT 2";

    public const string CorrelacionPorId =
        "SELECT o.order_id, o.order_code\nFROM sales.orders AS o\nWHERE o.order_id = @plan_DeclaredOrderId_Value\nLIMIT 2";

    public const string Relectura =
        "SELECT o.order_id, o.order_code, o.status, o.total_amount, o.currency_code, o.is_active\n"
        + "FROM sales.orders AS o\nWHERE o.order_id = @orderId\nLIMIT 2";

    public const string PagosDelPedido =
        "SELECT o.amount, o.source, o.currency_code\nFROM sales.order_payments AS o\nWHERE o.order_id = @orderId";

    /// <summary>Compara la secuencia exacta: texto, nombres de parámetro y valores, en orden.</summary>
    public static void Afirmar(BaseVentasSimulada b, params (string Sql, (string Nombre, object Valor)[] Parametros)[] esperadas)
    {
        var observadas = b.Consultas.Select(s => s.Replace("\r\n", "\n", StringComparison.Ordinal)).ToArray();
        Assert.Equal(esperadas.Select(e => e.Sql), observadas);
        for (var i = 0; i < esperadas.Length; i++)
        {
            Assert.Equal(
                esperadas[i].Parametros.Select(p => (p.Nombre, Normalizar(p.Valor))),
                b.Parametros[i].Select(p => (p.Nombre, Normalizar(p.Valor))));
        }
    }

    private static object? Normalizar(object? v) => v switch
    {
        DateTimeOffset d => d.UtcDateTime,
        DateTime d => DateTime.SpecifyKind(d, DateTimeKind.Utc),
        _ => v,
    };
}

/// <summary>
/// P1-POST-G6-R6 (Claude A): la interfaz real de Reporting (rutas de <c>ReportsModule</c>)
/// montada en el Host de integración en proceso, sobre los lectores reales de M03 a través de
/// la composición negativa cerrada. Se ejecuta el delegado de cada ruta como en las pruebas de
/// M10, sin listener. La autenticación real no se compone (exige el almacén de sesiones de
/// CORE); la política declarada de cada ruta sí se comprueba.
/// </summary>
public sealed class ReportingHostTests
{
    private const string Inicio = "2026-10-01T00:00:00Z";
    private const string Fin = "2026-10-02T00:00:00Z";
    private static readonly DateTime InicioUtc = DateTime.Parse("2026-10-01T00:00:00Z").ToUniversalTime();
    private static readonly DateTime FinUtc = InicioUtc.AddDays(1);

    private static readonly string[] RutasDeReporting =
    [
        "GET /api/admin/reporting/orders",
        "GET /api/admin/reporting/orders/value",
        "GET /api/admin/reporting/payments",
        "GET /api/admin/reporting/installation-zone",
        "POST /api/admin/reporting/orders/export-audit",
    ];

    private sealed record HostDeReporting(WebApplication App, BaseVentasSimulada Base, SalesDbContext Ctx) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => App.DisposeAsync();
    }

    private static HostDeReporting Montar(PedidoSimulado[] pedidos, PagoSimulado[] pagos)
    {
        var b = new BaseVentasSimulada();
        b.Pedidos.AddRange(pedidos);
        b.Pagos.AddRange(pagos);
        var c = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=sin_base;Username=x;Password=x;Timeout=1").AddInterceptors(b).Options,
            new NodeIdentity("prueba"), TimeProvider.System);
        var app = NegativeCommerceReportingHost.Build(Config(), Environments.Development, s =>
        {
            s.RemoveAll<SalesDbContext>();
            s.AddSingleton(c);
        });
        return new(app, b, c);
    }

    private static IReadOnlyList<RouteEndpoint> Rutas(WebApplication app)
        => [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>()];

    private static string Firma(RouteEndpoint r)
        => $"{Assert.Single(r.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)} {r.RoutePattern.RawText}";

    /// <summary>Ejecuta la ruta real en proceso, con su propio ámbito, y devuelve código y cuerpo.</summary>
    private static async Task<(int Codigo, JsonElement Cuerpo)> Get(HostDeReporting h, string ruta, string inicio = Inicio, string fin = Fin)
    {
        var endpoint = Rutas(h.App).Single(r => Firma(r) == "GET " + ruta);
        await using var scope = h.App.Services.CreateAsyncScope();
        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        contexto.Request.Method = "GET";
        contexto.Request.Path = ruta;
        contexto.Request.QueryString = QueryString.Create(new Dictionary<string, string?> { ["start"] = inicio, ["end"] = fin });
        using var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await endpoint.RequestDelegate!(contexto);

        return (contexto.Response.StatusCode, JsonDocument.Parse(salida.ToArray()).RootElement.Clone());
    }

    private static void SinEfectos(HostDeReporting h)
    {
        Assert.Equal(0, h.Base.Guardados);
        Assert.Equal(0, h.Base.ComandosProhibidos);
        Assert.Equal(0, h.Base.Transacciones);
        Assert.Empty(h.Ctx.ChangeTracker.Entries());
    }

    private static readonly List<string> Registro = [];

    private static void Anotar(string linea)
    {
        lock (Registro)
        {
            Registro.Add(linea);
            Escribir("reporting-host.txt", Registro.Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public async Task R6_El_Host_monta_solo_las_rutas_reales_de_Reporting_y_todas_exigen_admin()
    {
        await using var h = Montar([], []);

        var rutas = Rutas(h.App);
        Assert.Equal(RutasDeReporting, rutas.Select(Firma));
        foreach (var ruta in rutas)
        {
            Assert.Equal(["admin"], ruta.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy));
            Assert.StartsWith("/api/admin/reporting/", ruta.RoutePattern.RawText, StringComparison.Ordinal);
        }

        // El contenedor del Host es el de la composición cerrada.
        await using var scope = h.App.Services.CreateAsyncScope();
        Assert.IsType<M03ReadOnlyNegativeEffectPortAdapter>(Assert.Single(scope.ServiceProvider.GetServices<IProviderCommercialEffectPort>()));
        Assert.Null(scope.ServiceProvider.GetService<IOrderValueAggregateReader>());
        Assert.Null(scope.ServiceProvider.GetService<IExternalSettlementCandidate>());
        Anotar($"rutas montadas: {string.Join(" | ", rutas.Select(Firma))} · política=admin en todas");
    }

    [Fact]
    public void R6_La_barrera_del_Host_impide_construirlo_en_Production_o_sin_opt_in()
    {
        Assert.Throws<InvalidOperationException>(() => NegativeCommerceReportingHost.Build(Config(), Environments.Production));
        Assert.Throws<InvalidOperationException>(() => NegativeCommerceReportingHost.Build(Config(optIn: null), Environments.Development));
        Assert.Throws<InvalidOperationException>(() => NegativeCommerceReportingHost.Build(Config(), Environments.Development,
            s => s.AddCandidateSalesMonetaryReaders()));
    }

    [Fact]
    public async Task R6_GET_orders_devuelve_I01_I03_completo_e_I04_exacto_con_una_sola_consulta_agregada()
    {
        await using var h = Montar(CohorteR6(), []);

        var (codigo, cuerpo) = await Get(h, "/api/admin/reporting/orders");

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("with_data", cuerpo.GetProperty("status").GetString());
        Assert.Equal(6, cuerpo.GetProperty("i01CreatedOrders").GetInt64());
        Assert.Equal(4, cuerpo.GetProperty("activeOrders").GetInt64());
        Assert.Equal(2, cuerpo.GetProperty("inactiveOrders").GetInt64());
        // C11: la distribución completa, en el orden de OrderStatus.All, y el vencido de baja contado en I04.
        Assert.Equal(I03Esperado, cuerpo.GetProperty("i03CurrentStatusCounts").EnumerateArray()
            .Select(x => (x.GetProperty("status").GetString()!, x.GetProperty("count").GetInt64())));
        Assert.Equal(2, cuerpo.GetProperty("i04CurrentlyExpiredOrders").GetInt64());
        // C08: exactamente la consulta agregada, con la ventana pedida; ninguna por fila.
        SqlEsperado.Afirmar(h.Base, (SqlEsperado.I01I03I04, [("@startUtc", InicioUtc), ("@endUtc", FinUtc)]));
        SinEfectos(h);
        Anotar($"GET /orders · 200 · I01={cuerpo.GetProperty("i01CreatedOrders")} I03={string.Join(",", I03Esperado.Select(x => $"{x.Item1}:{x.Item2}"))} "
            + $"I04={cuerpo.GetProperty("i04CurrentlyExpiredOrders")} activos=4 bajas=2 · consultas=1 agregada");
    }

    [Fact]
    public async Task R6_GET_orders_value_no_publica_importes_I02_y_no_lee_nada()
    {
        await using var h = Montar(CohorteR6(), []);

        var (codigo, cuerpo) = await Get(h, "/api/admin/reporting/orders/value");

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("not_measurable", cuerpo.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("byCurrency").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("createdCount").ValueKind);
        // Sin lector de valor admitido no hay consulta: los importes PEN de la cohorte no se tocan.
        SqlEsperado.Afirmar(h.Base);
        SinEfectos(h);
        Anotar("GET /orders/value · 200 · status=not_measurable byCurrency=null createdCount=null · consultas=0");
    }

    [Fact]
    public async Task R6_GET_payments_devuelve_I05_I07_no_medibles_con_una_sola_consulta_agregada()
    {
        await using var h = Montar(CohorteR6(), PagosR6());

        var (codigo, cuerpo) = await Get(h, "/api/admin/reporting/payments");

        Assert.Equal(StatusCodes.Status200OK, codigo);
        foreach (var indicador in new[] { "i05", "i06", "i07" })
        {
            Assert.Equal("not_measurable", cuerpo.GetProperty(indicador).GetProperty("status").GetString());
        }

        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i05").GetProperty("count").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i06").GetProperty("byCurrency").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("i07").GetProperty("count").ValueKind);
        Assert.Equal("writer_not_accredited", cuerpo.GetProperty("notMeasurableReason").GetString());
        Assert.Equal(1, cuerpo.GetProperty("historicalWithoutSource").GetProperty("count").GetInt64());
        SqlEsperado.Afirmar(h.Base, (SqlEsperado.I05I07, [("@inicio", InicioUtc), ("@fin", FinUtc)]));
        SinEfectos(h);
        Anotar("GET /payments · 200 · I05/I06/I07=not_measurable motivo=writer_not_accredited históricos_sin_origen=1 · consultas=1 agregada");
    }

    [Fact]
    public async Task R6_Un_periodo_invalido_responde_400_sin_leer_a_M03()
    {
        await using var h = Montar(CohorteR6(), []);

        var (codigo, _) = await Get(h, "/api/admin/reporting/orders", "2026-10-01T00:00:00", Fin);

        Assert.Equal(StatusCodes.Status400BadRequest, codigo);
        SqlEsperado.Afirmar(h.Base);
    }

    /// <summary>
    /// C07: el despliegue publica <c>Sillar.Api.csproj</c> (backend/Dockerfile:33-35). Ni ese
    /// proyecto ni ninguno de los que alcanza por referencia puede llegar a CommerceBridge, y su
    /// código no puede nombrarlo.
    /// </summary>
    [Fact]
    public void R6_C07_Sillar_Api_no_alcanza_CommerceBridge_ni_directa_ni_transitivamente()
    {
        var backend = new DirectoryInfo(AppContext.BaseDirectory);
        while (backend is not null && !File.Exists(Path.Combine(backend.FullName, "Sillar.sln")))
        {
            backend = backend.Parent;
        }

        Assert.NotNull(backend);
        var referencia = new Regex("<ProjectReference\\s+Include=\"([^\"]+)\"", RegexOptions.CultureInvariant);
        var visitados = new HashSet<string>(StringComparer.Ordinal);
        var pendientes = new Stack<string>([Path.Combine(backend.FullName, "Sillar.Api", "Sillar.Api.csproj")]);
        while (pendientes.Count > 0)
        {
            var proyecto = Path.GetFullPath(pendientes.Pop());
            if (!visitados.Add(proyecto)) continue;
            Assert.True(File.Exists(proyecto), proyecto);
            foreach (Match m in referencia.Matches(File.ReadAllText(proyecto)))
            {
                pendientes.Push(Path.Combine(Path.GetDirectoryName(proyecto)!, m.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar)));
            }
        }

        Assert.Contains(visitados, p => p.EndsWith("Sillar.Modules.Payments.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(visitados, p => p.Contains("CommerceBridge", StringComparison.Ordinal));
        var api = Path.Combine(backend.FullName, "Sillar.Api");
        foreach (var fuente in Directory.EnumerateFiles(api, "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".csproj", StringComparison.Ordinal) || f.EndsWith(".json", StringComparison.Ordinal))
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var texto = File.ReadAllText(fuente);
            Assert.DoesNotContain("CommerceBridge", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("AddNegativeCommerceIntegration", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("AddPaymentsNegativeComposition", texto, StringComparison.Ordinal);
        }

        Anotar($"C07 · grafo de Sillar.Api: {visitados.Count} proyectos, ninguno CommerceBridge; fuentes de Sillar.Api sin menciones");
    }
}
