using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.Reports;
using Sillar.Modules.Sales;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Pedidos;

namespace Sillar.Harness.M03M10.Tests;

/// <summary>
/// Productor real de M03 (lectores y reducción) → contrato público → adaptador real de
/// M10 → endpoint real de M10. La base se sustituye por interceptores de EF: no se abre
/// ninguna conexión y cada sentencia devuelve las filas que el caso declara.
/// </summary>
public sealed class ComposicionNegativaM03M10Tests
{
    private const string Valor = "/api/admin/reporting/orders/value";
    private const string Pagos = "/api/admin/reporting/payments";

    // ---- 1. Composición productiva tal cual: SalesModule sin opt-in ----

    [Fact]
    public async Task Con_SalesModule_real_sin_opt_in_no_hay_lector_monetario_y_M10_dice_no_medible_sin_tocar_la_base()
    {
        var bd = new BaseInterceptada(_ => throw new InvalidOperationException("No debía consultarse nada."));
        await using var app = Aplicacion(bd, optIn: false);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            Assert.NotNull(scope.ServiceProvider.GetService<IOrderAggregateReader>());
            Assert.Null(scope.ServiceProvider.GetService<IOrderValueAggregateReader>());
            Assert.Null(scope.ServiceProvider.GetService<IOrderPaymentAggregateReader>());
        }

        var (cv, valor) = await Consultar(app, Valor);
        Assert.Equal(200, cv);
        Assert.Equal("not_measurable", valor.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, valor.GetProperty("byCurrency").ValueKind);

        var (cp, pagos) = await Consultar(app, Pagos);
        Assert.Equal(200, cp);
        Assert.Equal("reader_absent", pagos.GetProperty("notMeasurableReason").GetString());
        foreach (var i in new[] { "i05", "i06", "i07" })
        {
            Assert.Equal("not_measurable", pagos.GetProperty(i).GetProperty("status").GetString());
        }
        Assert.Equal(JsonValueKind.Null, pagos.GetProperty("i05").GetProperty("count").ValueKind);
        Assert.Empty(bd.Sentencias);
    }

    [Fact]
    public async Task Sin_M03_instalado_M10_dice_fuente_no_disponible_y_no_cero()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddRouting();
        new ReportsModule().RegisterServices(builder.Services, new ConfigurationBuilder().Build());
        await using var app = builder.Build();
        new ReportsModule().MapEndpoints(app);

        var (_, valor) = await Consultar(app, Valor);
        var (_, pagos) = await Consultar(app, Pagos);

        Assert.Equal("source_unavailable", valor.GetProperty("status").GetString());
        Assert.Equal("source_unavailable", pagos.GetProperty("i05").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, pagos.GetProperty("i05").GetProperty("count").ValueKind);
    }

    // ---- 2. Opt-in solo en el arnés: productor real alimentando al adaptador real ----

    [Fact]
    public async Task Con_opt_in_del_arnes_I02_lee_del_productor_real_y_cada_moneda_va_aparte_sin_sumar()
    {
        var bd = new BaseInterceptada(_ => Filas(["moneda", "activo", "pedidos", "total"],
            ["PEN", true, 3L, 300.00m],
            ["PEN", false, 1L, 50.00m],
            ["USD", true, 2L, 20.00m],
            [null, true, 4L, 999.00m]));
        await using var app = Aplicacion(bd, optIn: true);

        var (codigo, valor) = await Consultar(app, Valor);

        Assert.Equal(200, codigo);
        Assert.Equal("with_data", valor.GetProperty("status").GetString());
        Assert.Equal(10, valor.GetProperty("createdCount").GetInt64());
        var monedas = valor.GetProperty("byCurrency").EnumerateArray().ToArray();
        Assert.Equal(["PEN", "USD"], monedas.Select(m => m.GetProperty("currencyCode").GetString()));
        Assert.Equal("300.00", monedas[0].GetProperty("activeAmount").GetString());
        Assert.Equal("50.00", monedas[0].GetProperty("inactiveAmount").GetString());
        Assert.Equal("20.00", monedas[1].GetProperty("activeAmount").GetString());
        // Los 4 pedidos sin moneda se cuentan; su importe (999) no aparece en ningún sitio.
        Assert.Equal(4, valor.GetProperty("withoutCurrency").GetProperty("active").GetInt64());
        Assert.DoesNotContain("999", valor.GetRawText());
        Assert.DoesNotContain("320", valor.GetRawText());
        Assert.Equal("partial", valor.GetProperty("coverage").GetString());
        Assert.Single(bd.Sentencias);
        Assert.Contains("FROM sales.orders", bd.Sentencias[0]);
    }

    [Fact]
    public async Task Con_opt_in_del_arnes_y_pagos_reales_ningun_escritor_acreditado_da_no_medible_y_ninguna_cifra()
    {
        var bd = new BaseInterceptada(_ => Filas(["origen", "medio", "moneda", "tardio", "pagos", "importe"],
            [PaymentSource.Staff, PaymentMethod.Yape, "PEN", false, 4L, 400.00m],
            [PaymentSource.Staff, PaymentMethod.Cash, "USD", true, 1L, 10.00m],
            [PaymentSource.Provider, PaymentMethod.Yape, null, false, 2L, 77.00m]));
        await using var app = Aplicacion(bd, optIn: true);

        var (codigo, pagos) = await Consultar(app, Pagos);

        Assert.Equal(200, codigo);
        Assert.Equal("writer_not_accredited", pagos.GetProperty("notMeasurableReason").GetString());
        foreach (var i in new[] { "i05", "i06", "i07" })
        {
            Assert.Equal("not_measurable", pagos.GetProperty(i).GetProperty("status").GetString());
        }
        Assert.Equal(JsonValueKind.Null, pagos.GetProperty("i05").GetProperty("count").ValueKind);
        Assert.Equal(JsonValueKind.Null, pagos.GetProperty("i06").GetProperty("byCurrency").ValueKind);
        Assert.Equal(JsonValueKind.Null, pagos.GetProperty("i07").GetProperty("count").ValueKind);
        var texto = pagos.GetRawText();
        Assert.DoesNotContain("400", texto);
        Assert.DoesNotContain("410", texto);
        Assert.DoesNotContain("77", texto);
        Assert.Single(bd.Sentencias);
        Assert.Contains("FROM sales.order_payments", bd.Sentencias[0]);
    }

    [Fact]
    public async Task Con_opt_in_del_arnes_una_ventana_sin_filas_no_es_cero_cobros()
    {
        var bd = new BaseInterceptada(_ => Filas(["origen", "medio", "moneda", "tardio", "pagos", "importe"]));
        await using var app = Aplicacion(bd, optIn: true);

        var (_, pagos) = await Consultar(app, Pagos);

        Assert.Equal("not_measurable", pagos.GetProperty("i05").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, pagos.GetProperty("i05").GetProperty("count").ValueKind);
        Assert.Equal("writer_not_accredited", pagos.GetProperty("notMeasurableReason").GetString());
    }

    [Fact]
    public async Task Con_opt_in_del_arnes_un_historico_con_origen_NULL_no_se_atribuye_al_personal_ni_al_proveedor()
    {
        var bd = new BaseInterceptada(_ => Filas(["origen", "medio", "moneda", "tardio", "pagos", "importe"],
            [null, PaymentMethod.Cash, "PEN", false, 6L, 600.00m]));
        await using var app = Aplicacion(bd, optIn: true);

        var (_, pagos) = await Consultar(app, Pagos);

        Assert.Equal(6, pagos.GetProperty("historicalWithoutSource").GetProperty("count").GetInt64());
        Assert.Equal("not_measurable", pagos.GetProperty("i05").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, pagos.GetProperty("i05").GetProperty("count").ValueKind);
        Assert.DoesNotContain("600", pagos.GetRawText());
        // Ningún origen recibe esas 6 filas como suyas.
        foreach (var i in new[] { "i05", "i06", "i07" })
        {
            if (pagos.GetProperty(i).GetProperty("bySource") is { ValueKind: JsonValueKind.Array } porOrigen)
            {
                Assert.All(porOrigen.EnumerateArray(), s => Assert.Equal(JsonValueKind.Null, s.GetProperty("count").ValueKind));
            }
        }
    }

    [Fact]
    public async Task La_composicion_solo_emite_SELECT_y_no_rastrea_ni_guarda_nada()
    {
        var bd = new BaseInterceptada(sql => sql.Contains("order_payments")
            ? Filas(["origen", "medio", "moneda", "tardio", "pagos", "importe"], [PaymentSource.Staff, PaymentMethod.Yape, "PEN", false, 1L, 1m])
            : Filas(["moneda", "activo", "pedidos", "total"], ["PEN", true, 1L, 1m]));
        await using var app = Aplicacion(bd, optIn: true);

        await Consultar(app, Valor);
        await Consultar(app, Pagos);

        Assert.Equal(2, bd.Sentencias.Count);
        Assert.All(bd.Sentencias, s => Assert.StartsWith("SELECT", s.TrimStart()));
        Assert.Equal(0, bd.EscriturasIntentadas);
        Assert.Equal(0, bd.ConexionesReales);
    }

    [Fact]
    public async Task Una_averia_del_productor_real_no_se_convierte_en_cero()
    {
        var bd = new BaseInterceptada(_ => throw new TimeoutException("La base no respondió."));
        await using var app = Aplicacion(bd, optIn: true);

        var (cv, valor) = await Consultar(app, Valor);
        var (cp, pagos) = await Consultar(app, Pagos);

        Assert.Equal(503, cv);
        Assert.Equal(503, cp);
        Assert.False(valor.TryGetProperty("byCurrency", out _));
        Assert.False(pagos.TryGetProperty("i05", out _));
    }

    // ---- Infraestructura del arnés ----

    private static WebApplication Aplicacion(BaseInterceptada bd, bool optIn)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(TimeProvider.System);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=sin-base.invalid;Database=arnes;Username=arnes;Password=arnes",
        }).Build();
        new SalesModule().RegisterServices(builder.Services, config);
        builder.Services.ConfigureDbContext<SalesDbContext>(o => o.AddInterceptors(bd, bd.Conexion));
        if (optIn)
        {
            builder.Services.AddCandidateSalesMonetaryReaders();
        }
        new ReportsModule().RegisterServices(builder.Services, config);
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

        if (scope.ServiceProvider.GetService<SalesDbContext>() is { } db)
        {
            Assert.Empty(db.ChangeTracker.Entries());
        }
        var bytes = salida.ToArray();
        return (contexto.Response.StatusCode, bytes.Length == 0 ? default : JsonDocument.Parse(bytes).RootElement.Clone());
    }

    private static DataTable Filas(string[] columnas, params object?[][] filas)
    {
        var tabla = new DataTable();
        var tipos = columnas.Select((_, i) => filas.Select(f => f[i]).FirstOrDefault(v => v is not null)?.GetType() ?? typeof(string)).ToArray();
        // Tipos fijos por posición para que una tabla vacía tenga el esquema del productor.
        if (columnas.Length == 4) tipos = [typeof(string), typeof(bool), typeof(long), typeof(decimal)];
        if (columnas.Length == 6) tipos = [typeof(string), typeof(string), typeof(string), typeof(bool), typeof(long), typeof(decimal)];
        for (var i = 0; i < columnas.Length; i++) tabla.Columns.Add(columnas[i], tipos[i]);
        foreach (var f in filas) tabla.Rows.Add([.. f.Select(v => v ?? DBNull.Value)]);
        return tabla;
    }

    private sealed class BaseInterceptada(Func<string, DataTable> respuesta) : DbCommandInterceptor
    {
        public List<string> Sentencias { get; } = [];
        public int EscriturasIntentadas { get; private set; }
        public int ConexionesReales => Conexion.Reales;
        public SinConexion Conexion { get; } = new();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sentencias.Add(command.CommandText);
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "sql-capturado.log"), command.CommandText + "\n;\n");
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(respuesta(command.CommandText).CreateDataReader()));
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            EscriturasIntentadas++;
            throw new InvalidOperationException("El arnés no admite escrituras.");
        }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            EscriturasIntentadas++;
            throw new InvalidOperationException("El arnés no admite escrituras.");
        }
    }

    private sealed class SinConexion : DbConnectionInterceptor
    {
        public int Reales { get; private set; }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());

        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => InterceptionResult.Suppress();

        // EF avisa de «abierta» también cuando la apertura se suprimió: solo cuenta si de verdad lo está.
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            if (connection.State == ConnectionState.Open) Reales++;
        }

        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (connection.State == ConnectionState.Open) Reales++;
            return Task.CompletedTask;
        }
    }
}
