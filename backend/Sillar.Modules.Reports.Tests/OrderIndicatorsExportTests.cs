using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Modules.Reports.Endpoints;
using Sillar.Modules.Reports.Export;
using Sillar.Modules.Sales.Contracts;
using static Sillar.Modules.Reports.Tests.ZonaDeCore;

namespace Sillar.Modules.Reports.Tests;

/// <summary>
/// La exportación generada en el servidor, montada solo aquí: ningún byte del
/// archivo sale sin zona verificada, CSRF y un registro <c>Recorded</c> con uuid
/// v7, y las filas auditadas son las del archivo.
/// </summary>
public sealed class OrderIndicatorsExportTests
{
    private const string Ruta = "/api/admin/reporting/orders/export";
    private const string Token = "token-de-prueba";
    private const string Bogota = "America/Bogota";
    private static readonly object Pedido = new { startInclusive = "2026-10-01T00:00:00-05:00", endExclusive = "2026-10-08T00:00:00-05:00", zone = Bogota };

    [Fact]
    public void El_modulo_no_monta_la_exportacion_generada_en_el_servidor()
    {
        var app = Aplicacion(Siempre(Verificada(Bogota)), AuditoriaDeCore.Registra(), new Ventas(5), montarExportacion: false);

        Assert.DoesNotContain(Rutas(app), r => r.RoutePattern.RawText == Ruta);
    }

    [Fact]
    public async Task Con_todo_confirmado_entrega_el_archivo_con_las_filas_auditadas_iguales_a_las_escritas()
    {
        var id = Guid.CreateVersion7();
        var auditoria = new AuditoriaDeCore(ExportAuditResult.Recorded(id, AuditoriaDeCore.Actor));
        var ventas = new Ventas(5);

        var respuesta = await Exportar(Aplicacion(Siempre(Verificada(Bogota)), auditoria, ventas), Pedido);

        Assert.Equal(StatusCodes.Status200OK, respuesta.Codigo);
        Assert.StartsWith("text/csv", respuesta.Tipo);
        Assert.Equal(id.ToString(), respuesta.ExportId);
        var texto = Encoding.UTF8.GetString(respuesta.Bytes);
        Assert.StartsWith("﻿Cohorte,", texto);
        var filas = texto.TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var pedido = Assert.Single(auditoria.Pedidos);
        Assert.Equal(new ExportAuditRequest("reporting", "order_indicators", "csv", filas.Length - 1), pedido);
        Assert.Equal(11, pedido.RowCount);
        Assert.Contains("A,2026-10-01 00:00,2026-10-08 00:00,America/Bogota,", texto);
        Assert.Contains("Estado actual: Pendiente de pago,5", texto);
    }

    [Fact]
    public async Task Las_filas_que_declare_el_pedido_se_ignoran_y_se_audita_lo_escrito()
    {
        var auditoria = new AuditoriaDeCore(ExportAuditResult.Recorded(Guid.CreateVersion7(), AuditoriaDeCore.Actor));

        await Exportar(Aplicacion(Siempre(Verificada(Bogota)), auditoria, new Ventas(5)),
            new { startInclusive = "2026-10-01T00:00:00-05:00", endExclusive = "2026-10-08T00:00:00-05:00", zone = "UTC", dataRows = 999 });

        Assert.Equal(11, Assert.Single(auditoria.Pedidos).RowCount);
    }

    public static TheoryData<string> ZonasNoVerificadas =>
        ["sin_conexion", "inicial", "configurada", "caducada", "revocada", "sin_fuente", "verified_sin_atestacion"];

    [Theory]
    [MemberData(nameof(ZonasNoVerificadas))]
    public async Task Sin_zona_verificada_no_se_lee_ventas_ni_se_audita_ni_sale_ningun_byte(string caso)
    {
        ZonaDeCore? zona = caso switch
        {
            "inicial" => Siempre(Inicial()),
            "configurada" => Siempre(Configurada(Bogota, TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.AttestationAuthorityUnratified, AtestacionA)),
            "caducada" => Siempre(Configurada(Bogota, TimeZoneVerificationStatus.Stale, TimeZoneVerificationReason.StaleSourceChanged, AtestacionA)),
            "revocada" => Siempre(Configurada(Bogota, TimeZoneVerificationStatus.Revoked, TimeZoneVerificationReason.AttestationRevoked, AtestacionA)),
            "sin_fuente" => Siempre(Configurada(Bogota, TimeZoneVerificationStatus.SourceNotVerifiable, TimeZoneVerificationReason.TzdbSourceUnavailable)),
            "verified_sin_atestacion" => Siempre(Configurada(Bogota, TimeZoneVerificationStatus.Verified, TimeZoneVerificationReason.AttestationAuthorityUnratified)),
            _ => null,
        };
        var auditoria = AuditoriaDeCore.Registra();
        var ventas = new Ventas(5);

        var respuesta = await Exportar(Aplicacion(zona, auditoria, ventas), Pedido);

        Assert.Equal(StatusCodes.Status409Conflict, respuesta.Codigo);
        SinArchivo(respuesta, auditoria);
        Assert.Equal(0, ventas.Llamadas);
    }

    [Fact]
    public async Task Un_archivo_en_otra_zona_que_la_verificada_o_UTC_no_se_genera()
    {
        var auditoria = AuditoriaDeCore.Registra();

        var respuesta = await Exportar(Aplicacion(Siempre(Verificada(Bogota)), auditoria, new Ventas(5)),
            new { startInclusive = "2026-10-01T00:00:00-05:00", endExclusive = "2026-10-08T00:00:00-05:00", zone = "America/Lima" });

        Assert.Equal(StatusCodes.Status409Conflict, respuesta.Codigo);
        SinArchivo(respuesta, auditoria);
    }

    [Fact]
    public async Task Sin_token_CSRF_no_se_lee_ni_se_audita_nada()
    {
        var auditoria = AuditoriaDeCore.Registra();
        var ventas = new Ventas(5);

        var respuesta = await Exportar(Aplicacion(Siempre(Verificada(Bogota)), auditoria, ventas), Pedido, conToken: false);

        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.Codigo);
        SinArchivo(respuesta, auditoria);
        Assert.Equal(0, ventas.Llamadas);
    }

    [Theory]
    [InlineData(ExportAuditFailure.ActorMissing)]
    [InlineData(ExportAuditFailure.MetadataInvalid)]
    public async Task Si_la_auditoria_se_niega_responde_403_sin_archivo(ExportAuditFailure causa)
    {
        var auditoria = new AuditoriaDeCore(ExportAuditResult.Rejected(causa));

        var respuesta = await Exportar(Aplicacion(Siempre(Verificada(Bogota)), auditoria, new Ventas(5)), Pedido);

        Assert.Equal(StatusCodes.Status403Forbidden, respuesta.Codigo);
        Assert.Null(respuesta.ExportId);
        Assert.DoesNotContain("text/csv", respuesta.Tipo);
    }

    public static TheoryData<string> RegistrosNoLegitimos =>
        ["sin_productor", "no_disponible", "uuid_v4", "uuid_vacio", "sin_actor", "actor_sin_nodo", "accion_no_admitida", "no_durable"];

    [Theory]
    [MemberData(nameof(RegistrosNoLegitimos))]
    public async Task Sin_un_registro_Recorded_con_uuid_v7_responde_503_sin_archivo(string caso)
    {
        var auditoria = caso switch
        {
            "sin_productor" => null,
            "no_disponible" => new AuditoriaDeCore(ExportAuditResult.Rejected(ExportAuditFailure.AuditUnavailable)),
            "uuid_v4" => new AuditoriaDeCore(ExportAuditResult.Recorded(Guid.Parse("0199a6f0-0000-4000-8000-000000000001"), AuditoriaDeCore.Actor)),
            "sin_actor" => new AuditoriaDeCore(ExportAuditResult.Recorded(Guid.CreateVersion7(), null!)),
            "actor_sin_nodo" => new AuditoriaDeCore(ExportAuditResult.Recorded(Guid.CreateVersion7(), AuditoriaDeCore.Actor with { HomeNode = "" })),
            "accion_no_admitida" => new AuditoriaDeCore(ExportAuditResult.Rejected(ExportAuditFailure.ExportActionNotAdmitted)),
            "no_durable" => new AuditoriaDeCore(ExportAuditResult.Rejected(ExportAuditFailure.AuditNotDurable)),
            _ => new AuditoriaDeCore(ExportAuditResult.Recorded(Guid.Empty, AuditoriaDeCore.Actor)),
        };

        var respuesta = await Exportar(Aplicacion(Siempre(Verificada(Bogota)), auditoria, new Ventas(5)), Pedido);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, respuesta.Codigo);
        Assert.Null(respuesta.ExportId);
        Assert.DoesNotContain("text/csv", respuesta.Tipo);
    }

    [Theory]
    [InlineData("ausente", StatusCodes.Status409Conflict)]
    [InlineData("falla", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("incoherente", StatusCodes.Status503ServiceUnavailable)]
    public async Task Sin_una_instantanea_valida_de_ventas_no_se_audita(string caso, int esperado)
    {
        var auditoria = AuditoriaDeCore.Registra();
        var ventas = caso switch
        {
            "ausente" => null,
            "falla" => new Ventas(5) { Falla = true },
            _ => new Ventas(5) { Incoherente = true },
        };

        var respuesta = await Exportar(Aplicacion(Siempre(Verificada(Bogota)), auditoria, ventas), Pedido);

        Assert.Equal(esperado, respuesta.Codigo);
        SinArchivo(respuesta, auditoria);
    }

    [Fact]
    public async Task Si_la_zona_cambia_mientras_se_lee_no_se_audita_ni_se_entrega()
    {
        var auditoria = AuditoriaDeCore.Registra();
        var zona = EnOrden(Verificada(Bogota), Verificada("America/Lima"));

        var respuesta = await Exportar(Aplicacion(zona, auditoria, new Ventas(5)), Pedido);

        Assert.Equal(StatusCodes.Status409Conflict, respuesta.Codigo);
        SinArchivo(respuesta, auditoria);
    }

    [Fact]
    public async Task Si_la_zona_cambia_mientras_se_registra_queda_el_registro_y_no_sale_el_archivo()
    {
        var auditoria = AuditoriaDeCore.Registra();
        var zona = EnOrden(Verificada(Bogota), Verificada(Bogota), Verificada("America/Lima"));

        var respuesta = await Exportar(Aplicacion(zona, auditoria, new Ventas(5)), Pedido);

        Assert.Equal(StatusCodes.Status409Conflict, respuesta.Codigo);
        Assert.Single(auditoria.Pedidos);
        Assert.Null(respuesta.ExportId);
        Assert.DoesNotContain("text/csv", respuesta.Tipo);
    }

    [Fact]
    public async Task Una_cancelacion_durante_el_registro_se_propaga_sin_ningun_byte()
    {
        using var abortada = new CancellationTokenSource();
        var auditoria = new AuditoriaDeCore(ExportAuditResult.Recorded(Guid.CreateVersion7(), AuditoriaDeCore.Actor)) { AlRegistrar = abortada.Cancel };
        var app = Aplicacion(Siempre(Verificada(Bogota)), auditoria, new Ventas(5));
        using var salida = new MemoryStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Exportar(app, Pedido, abortada: abortada.Token, salida: salida));

        Assert.Equal(0, salida.Length);
    }

    public static TheoryData<string> ZonaQueDejaDeValer =>
        ["revocada_antes", "otra_atestacion_antes", "revocada_despues", "otra_atestacion_despues", "caducada_despues", "averia_despues"];

    [Theory]
    [MemberData(nameof(ZonaQueDejaDeValer))]
    public async Task Si_la_zona_se_revoca_o_cambia_de_atestacion_con_el_mismo_ZoneId_no_sale_ningun_byte(string caso)
    {
        var revocada = Configurada(Bogota, TimeZoneVerificationStatus.Revoked, TimeZoneVerificationReason.AttestationRevoked, AtestacionA);
        var zona = caso switch
        {
            "revocada_antes" => EnOrden(Verificada(Bogota), revocada),
            "otra_atestacion_antes" => EnOrden(Verificada(Bogota), Verificada(Bogota, AtestacionB)),
            "revocada_despues" => EnOrden(Verificada(Bogota), Verificada(Bogota), revocada),
            "otra_atestacion_despues" => EnOrden(Verificada(Bogota), Verificada(Bogota), Verificada(Bogota, AtestacionB)),
            "caducada_despues" => EnOrden(Verificada(Bogota), Verificada(Bogota),
                Configurada(Bogota, TimeZoneVerificationStatus.Stale, TimeZoneVerificationReason.StaleSourceChanged, AtestacionA)),
            _ => new ZonaDeCore(() => Verificada(Bogota), () => Verificada(Bogota), () => throw new InvalidOperationException("caída")),
        };
        var auditoria = AuditoriaDeCore.Registra();

        var respuesta = await Exportar(Aplicacion(zona, auditoria, new Ventas(5)), Pedido);

        Assert.Equal(caso == "averia_despues" ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status409Conflict, respuesta.Codigo);
        Assert.Null(respuesta.ExportId);
        Assert.DoesNotContain("text/csv", respuesta.Tipo);
        Assert.DoesNotContain("Cohorte", Encoding.UTF8.GetString(respuesta.Bytes));
        // Antes del registro no se audita; después, el registro queda sin archivo.
        Assert.Equal(caso.EndsWith("antes") ? 0 : 1, auditoria.Pedidos.Count);
    }

    [Fact]
    public async Task Si_CORE_no_puede_resolver_la_zona_responde_503_sin_leer_ventas_ni_auditar()
    {
        var auditoria = AuditoriaDeCore.Registra();
        var ventas = new Ventas(5);

        var respuesta = await Exportar(Aplicacion(Averiada(), auditoria, ventas), Pedido);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, respuesta.Codigo);
        SinArchivo(respuesta, auditoria);
        Assert.Equal(0, ventas.Llamadas);
    }

    [Fact]
    public async Task Una_cancelacion_durante_la_resolucion_de_la_zona_se_propaga_sin_ningun_byte()
    {
        using var abortada = new CancellationTokenSource();
        abortada.Cancel();
        var auditoria = AuditoriaDeCore.Registra();
        var ventas = new Ventas(5);
        using var salida = new MemoryStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Exportar(Aplicacion(Siempre(Verificada(Bogota)), auditoria, ventas), Pedido, abortada: abortada.Token, salida: salida));

        Assert.Equal(0, salida.Length);
        Assert.Empty(auditoria.Pedidos);
        Assert.Equal(0, ventas.Llamadas);
    }

    [Theory]
    [InlineData("""{"startInclusive":"2026-10-01T00:00:00","endExclusive":"2026-10-08T00:00:00-05:00","zone":"UTC"}""")]
    [InlineData("""{"startInclusive":"2026-10-08T00:00:00-05:00","endExclusive":"2026-10-01T00:00:00-05:00","zone":"UTC"}""")]
    [InlineData("""{"startInclusive":"2026-10-01T00:00:00-05:00","endExclusive":"2026-10-08T00:00:00-05:00"}""")]
    public async Task Un_pedido_invalido_responde_400_sin_leer_ni_auditar(string json)
    {
        var auditoria = AuditoriaDeCore.Registra();
        var ventas = new Ventas(5);

        var respuesta = await Exportar(Aplicacion(Siempre(Verificada(Bogota)), auditoria, ventas), JsonDocument.Parse(json).RootElement);

        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.Codigo);
        SinArchivo(respuesta, auditoria);
        Assert.Equal(0, ventas.Llamadas);
    }

    [Theory]
    [InlineData("=SUMA(A1)", "'=SUMA(A1)")]
    [InlineData(" ＋1", "' ＋1")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("di \"x\"", "\"di \"\"x\"\"\"")]
    public void Una_celda_de_texto_no_puede_ser_una_formula_y_respeta_RFC_4180(string valor, string esperado)
    {
        Assert.Equal(esperado, OrderIndicatorsCsv.Cell(valor));
    }

    [Fact]
    public void Cada_estado_de_ventas_tiene_su_frase_en_el_archivo()
    {
        Assert.Equal(OrderStatus.All.Order(), OrderIndicatorsCsv.StateLabels.Keys.Order());
    }

    private static void SinArchivo(Respuesta respuesta, AuditoriaDeCore? auditoria)
    {
        Assert.Null(respuesta.ExportId);
        Assert.DoesNotContain("text/csv", respuesta.Tipo);
        Assert.DoesNotContain("Cohorte", Encoding.UTF8.GetString(respuesta.Bytes));
        if (auditoria is not null) Assert.Empty(auditoria.Pedidos);
    }

    private static WebApplication Aplicacion(ZonaDeCore? zona, AuditoriaDeCore? auditoria, Ventas? ventas, bool montarExportacion = true)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddRouting();
        if (zona is not null) builder.Services.AddSingleton<IInstallationTimeZoneResolver>(zona);
        if (auditoria is not null) builder.Services.AddSingleton<IExportAudit>(auditoria);
        if (ventas is not null) builder.Services.AddSingleton<IOrderAggregateReader>(ventas);
        new ReportsModule().RegisterServices(builder.Services, new ConfigurationBuilder().Build());
        var app = builder.Build();
        new ReportsModule().MapEndpoints(app);
        if (montarExportacion) app.MapOrderIndicatorsCsvExport();
        return app;
    }

    private static IEnumerable<RouteEndpoint> Rutas(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>();

    private sealed record Respuesta(int Codigo, string Tipo, string? ExportId, byte[] Bytes);

    private static async Task<Respuesta> Exportar(WebApplication app, object cuerpo, bool conToken = true,
        CancellationToken abortada = default, MemoryStream? salida = null)
    {
        var destino = Rutas(app).Single(r => r.RoutePattern.RawText == Ruta);
        await using var scope = app.Services.CreateAsyncScope();
        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = abortada };
        contexto.Request.Method = "POST";
        contexto.Request.Path = Ruta;
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CsrfEndpointFilter.ClaimType, SessionTokens.Hash(Token))], "prueba"));
        if (conToken) contexto.Request.Headers[CsrfEndpointFilter.HeaderName] = Token;
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cuerpo));
        contexto.Request.ContentType = "application/json";
        contexto.Request.ContentLength = bytes.Length;
        contexto.Request.Body = new MemoryStream(bytes);
        contexto.Features.Set<IHttpRequestBodyDetectionFeature>(new ConCuerpo());
        salida ??= new MemoryStream();
        contexto.Response.Body = salida;

        await destino.RequestDelegate!(contexto);

        return new(contexto.Response.StatusCode, contexto.Response.ContentType ?? "",
            contexto.Response.Headers[OrderIndicatorsExportEndpoints.ExportIdHeader].SingleOrDefault(), salida.ToArray());
    }

    private sealed class ConCuerpo : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private sealed class Ventas(long creados) : IOrderAggregateReader
    {
        public bool Falla { get; init; }
        public bool Incoherente { get; init; }
        public int Llamadas { get; private set; }

        public Task<OrderAggregateSnapshot> ReadCreatedBetweenAsync(
            DateTimeOffset startInclusive, DateTimeOffset endExclusive, CancellationToken cancellationToken)
        {
            Llamadas++;
            if (Falla) throw new InvalidOperationException("Ventas no respondió.");
            var porEstado = OrderStatus.All
                .Select(status => new OrderStatusCount(status, status == OrderStatus.PendingPayment ? creados : 0))
                .ToArray();
            return Task.FromResult(new OrderAggregateSnapshot(
                startInclusive, endExclusive, DateTimeOffset.UtcNow, creados, porEstado,
                Incoherente ? 1 : 0, new OrderActivityCounts(creados, 0)));
        }
    }
}
