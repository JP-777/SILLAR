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
using static Sillar.Modules.Reports.Tests.ZonaDeCore;

namespace Sillar.Modules.Reports.Tests;

/// <summary>
/// La costura con CORE (<c>1d7c968</c>), con los productores sustituidos por dobles:
/// la zona se traduce sin inventar verificación, y la exportación solo se registra
/// con zona verificada y atestada, que sigue siéndolo después del registro.
/// </summary>
public sealed class CoreSeamEndpointsTests
{
    private const string RutaZona = "/api/admin/reporting/installation-zone";
    private const string RutaAuditoria = "/api/admin/reporting/orders/export-audit";
    private const string Token = "token-de-prueba";
    private const string Bogota = "America/Bogota";
    private static readonly object PedidoUtc = new { zone = "UTC", dataRows = 11 };

    [Fact]
    public async Task Sin_el_productor_de_zona_de_CORE_la_zona_sale_sin_conexion_y_no_como_Lima()
    {
        var (codigo, cuerpo) = await Llamar(Aplicacion(zona: null, auditoria: null), "GET", RutaZona);

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("unconnected", cuerpo.GetProperty("kind").GetString());
        Assert.DoesNotContain("America/Lima", cuerpo.GetRawText());
    }

    [Fact]
    public async Task La_zona_inicial_aprobada_sale_como_inicial_y_nunca_como_verificada()
    {
        var (_, cuerpo) = await Llamar(Aplicacion(Siempre(Inicial()), auditoria: null), "GET", RutaZona);

        Assert.Equal("approved_initial", cuerpo.GetProperty("kind").GetString());
        Assert.Equal("America/Lima", cuerpo.GetProperty("zone").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("canonical").ValueKind);
    }

    public static TheoryData<TimeZoneVerificationStatus, TimeZoneVerificationReason, bool> NoVerificadas => new()
    {
        { TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.NotAttested, false },
        { TimeZoneVerificationStatus.Unverified, TimeZoneVerificationReason.AttestationAuthorityUnratified, true },
        { TimeZoneVerificationStatus.Stale, TimeZoneVerificationReason.StaleValueChanged, true },
        { TimeZoneVerificationStatus.Revoked, TimeZoneVerificationReason.AttestationRevoked, true },
        { TimeZoneVerificationStatus.SourceNotVerifiable, TimeZoneVerificationReason.TzdbSourceUnavailable, false },
    };

    [Theory]
    [MemberData(nameof(NoVerificadas))]
    public async Task Una_zona_reconocida_pero_sin_verificar_caducada_o_revocada_no_sale_verificada_ni_se_audita(
        TimeZoneVerificationStatus estado, TimeZoneVerificationReason motivo, bool atestada)
    {
        var zona = Siempre(Configurada(Bogota, estado, motivo, atestada ? AtestacionA : null));
        var auditoria = AuditoriaDeCore.Registra();
        var app = Aplicacion(zona, auditoria);

        var (_, observada) = await Llamar(app, "GET", RutaZona);
        var (codigo, _) = await Llamar(app, "POST", RutaAuditoria, new { zone = Bogota, dataRows = 11 });

        Assert.Equal("runtime_resolved", observada.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, observada.GetProperty("canonical").ValueKind);
        Assert.Equal(StatusCodes.Status409Conflict, codigo);
        Assert.Empty(auditoria.Pedidos);
    }

    [Fact]
    public async Task Un_Verified_sin_atestacion_o_sobre_la_zona_inicial_no_se_acepta()
    {
        var sinAtestacion = Configurada(Bogota, TimeZoneVerificationStatus.Verified, TimeZoneVerificationReason.AttestationAuthorityUnratified);
        var inicial = Inicial() with { Status = TimeZoneVerificationStatus.Verified, AttestationId = AtestacionA };

        foreach (var resolucion in new[] { sinAtestacion, inicial, Verificada(Bogota, Guid.Empty) })
        {
            var auditoria = AuditoriaDeCore.Registra();
            var (codigo, _) = await Llamar(Aplicacion(Siempre(resolucion), auditoria), "POST", RutaAuditoria, PedidoUtc);

            Assert.Equal(StatusCodes.Status409Conflict, codigo);
            Assert.Empty(auditoria.Pedidos);
        }
    }

    [Fact]
    public async Task Un_valor_configurado_invalido_sale_invalido_con_su_texto_y_sin_zona()
    {
        var (_, cuerpo) = await Llamar(Aplicacion(Siempre(Invalida("Hora de Lima")), auditoria: null), "GET", RutaZona);

        Assert.Equal("invalid", cuerpo.GetProperty("kind").GetString());
        Assert.Equal("Hora de Lima", cuerpo.GetProperty("original").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("zone").ValueKind);
    }

    [Fact]
    public async Task Solo_una_resolucion_verificada_y_atestada_por_CORE_sale_verificada()
    {
        var (_, cuerpo) = await Llamar(Aplicacion(Siempre(Verificada(Bogota)), auditoria: null), "GET", RutaZona);

        Assert.Equal("verified", cuerpo.GetProperty("kind").GetString());
        Assert.Equal(Bogota, cuerpo.GetProperty("canonical").GetString());
    }

    [Fact]
    public async Task Si_CORE_no_puede_resolver_la_zona_responde_503_y_no_se_audita()
    {
        var auditoria = AuditoriaDeCore.Registra();
        var app = Aplicacion(Averiada(), auditoria);

        var (lectura, _) = await Llamar(app, "GET", RutaZona);
        var (registro, cuerpo) = await Llamar(app, "POST", RutaAuditoria, PedidoUtc);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, lectura);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, registro);
        Assert.DoesNotContain("no respondió", cuerpo.GetRawText());
        Assert.Empty(auditoria.Pedidos);
    }

    [Fact]
    public async Task Una_cancelacion_durante_la_resolucion_se_propaga_y_no_se_audita()
    {
        using var abortada = new CancellationTokenSource();
        abortada.Cancel();
        var auditoria = AuditoriaDeCore.Registra();
        var app = Aplicacion(Siempre(Verificada(Bogota)), auditoria);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Llamar(app, "GET", RutaZona, abortada: abortada.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Llamar(app, "POST", RutaAuditoria, PedidoUtc, abortada: abortada.Token));
        Assert.Empty(auditoria.Pedidos);
    }

    [Theory]
    [InlineData("America/Lima")]
    [InlineData("america/bogota")]
    [InlineData("Europe/Madrid")]
    public async Task Con_zona_verificada_un_archivo_en_otra_zona_no_se_registra(string zonaDelArchivo)
    {
        var auditoria = AuditoriaDeCore.Registra();

        var (codigo, _) = await Llamar(Aplicacion(Siempre(Verificada(Bogota)), auditoria), "POST", RutaAuditoria, new { zone = zonaDelArchivo, dataRows = 11 });

        Assert.Equal(StatusCodes.Status409Conflict, codigo);
        Assert.Empty(auditoria.Pedidos);
    }

    [Fact]
    public async Task Con_zona_verificada_y_registro_legitimo_responde_recorded_sin_filtros_ni_contenido()
    {
        var id = Guid.CreateVersion7();
        var auditoria = new AuditoriaDeCore(ExportAuditResult.Recorded(id, AuditoriaDeCore.Actor));

        var (codigo, cuerpo) = await Llamar(Aplicacion(Siempre(Verificada(Bogota)), auditoria), "POST", RutaAuditoria, PedidoUtc);

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("recorded", cuerpo.GetProperty("decision").GetString());
        Assert.Equal(id, cuerpo.GetProperty("exportId").GetGuid());
        Assert.Equal(new ExportAuditRequest("reporting", "order_indicators", "csv", 11), Assert.Single(auditoria.Pedidos));
    }

    public static TheoryData<string> RegistrosNoLegitimos =>
        ["uuid_vacio", "uuid_v4", "sin_actor", "actor_sin_id", "actor_sin_correo", "actor_sin_nodo", "recorded_con_causa"];

    [Theory]
    [MemberData(nameof(RegistrosNoLegitimos))]
    public async Task Un_registro_sin_uuid_v7_o_sin_actor_completo_no_cuenta_como_recorded(string caso)
    {
        var v7 = Guid.CreateVersion7();
        var resultado = caso switch
        {
            "uuid_vacio" => ExportAuditResult.Recorded(Guid.Empty, AuditoriaDeCore.Actor),
            "uuid_v4" => ExportAuditResult.Recorded(Guid.Parse("0199a6f0-0000-4000-8000-000000000001"), AuditoriaDeCore.Actor),
            "sin_actor" => ExportAuditResult.Recorded(v7, null!),
            "actor_sin_id" => ExportAuditResult.Recorded(v7, AuditoriaDeCore.Actor with { AdminUserId = 0 }),
            "actor_sin_correo" => ExportAuditResult.Recorded(v7, AuditoriaDeCore.Actor with { Email = "" }),
            "actor_sin_nodo" => ExportAuditResult.Recorded(v7, AuditoriaDeCore.Actor with { HomeNode = "" }),
            _ => new ExportAuditResult(ExportAuditOutcome.Recorded, v7, ExportAuditFailure.AuditUnavailable, AuditoriaDeCore.Actor),
        };

        var (codigo, cuerpo) = await Llamar(Aplicacion(Siempre(Verificada(Bogota)), new AuditoriaDeCore(resultado)), "POST", RutaAuditoria, PedidoUtc);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
        Assert.False(cuerpo.TryGetProperty("decision", out _));
    }

    [Theory]
    [InlineData(ExportAuditFailure.ExportActionNotAdmitted)]
    [InlineData(ExportAuditFailure.AuditNotDurable)]
    [InlineData(ExportAuditFailure.AuditUnavailable)]
    public async Task Si_CORE_no_puede_registrar_de_forma_durable_responde_503_y_no_recorded(ExportAuditFailure causa)
    {
        var (codigo, cuerpo) = await Llamar(Aplicacion(Siempre(Verificada(Bogota)), new AuditoriaDeCore(ExportAuditResult.Rejected(causa))),
            "POST", RutaAuditoria, new { zone = Bogota, dataRows = 11 });

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
        Assert.False(cuerpo.TryGetProperty("decision", out _));
    }

    [Fact]
    public async Task Sin_productor_de_auditoria_responde_503_aunque_la_zona_este_verificada()
    {
        var (codigo, _) = await Llamar(Aplicacion(Siempre(Verificada(Bogota)), auditoria: null), "POST", RutaAuditoria, PedidoUtc);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
    }

    [Theory]
    [InlineData(ExportAuditFailure.ActorMissing)]
    [InlineData(ExportAuditFailure.MetadataInvalid)]
    public async Task Si_CORE_se_niega_a_registrarla_responde_denied(ExportAuditFailure causa)
    {
        var (codigo, cuerpo) = await Llamar(Aplicacion(Siempre(Verificada(Bogota)), new AuditoriaDeCore(ExportAuditResult.Rejected(causa))),
            "POST", RutaAuditoria, PedidoUtc);

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("denied", cuerpo.GetProperty("decision").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("exportId").ValueKind);
    }

    public static TheoryData<string> CambiosTrasElRegistro =>
        ["revocada", "otra_atestacion_misma_zona", "otro_valor_configurado_misma_zona", "observacion_anterior", "otra_zona", "averia"];

    [Theory]
    [MemberData(nameof(CambiosTrasElRegistro))]
    public async Task Si_la_zona_deja_de_valer_durante_el_registro_queda_el_registro_y_no_se_responde_recorded(string caso)
    {
        var despues = caso switch
        {
            "revocada" => ZonaDeCore.EnOrden(Verificada(Bogota),
                Configurada(Bogota, TimeZoneVerificationStatus.Revoked, TimeZoneVerificationReason.AttestationRevoked, AtestacionA)),
            "otra_atestacion_misma_zona" => ZonaDeCore.EnOrden(Verificada(Bogota), Verificada(Bogota, AtestacionB)),
            "otro_valor_configurado_misma_zona" => ZonaDeCore.EnOrden(Verificada(Bogota), Verificada(Bogota, original: "America/Bogota ")),
            "observacion_anterior" => ZonaDeCore.EnOrden(Verificada(Bogota), Verificada(Bogota, observado: Observado.AddMinutes(-5))),
            "otra_zona" => ZonaDeCore.EnOrden(Verificada(Bogota), Verificada("America/Lima")),
            _ => new ZonaDeCore(() => Verificada(Bogota), () => throw new InvalidOperationException("caída")),
        };
        var auditoria = AuditoriaDeCore.Registra();

        var (codigo, cuerpo) = await Llamar(Aplicacion(despues, auditoria), "POST", RutaAuditoria, PedidoUtc);

        Assert.Equal(caso == "averia" ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status409Conflict, codigo);
        Assert.Single(auditoria.Pedidos);
        Assert.False(cuerpo.TryGetProperty("decision", out _));
    }

    [Fact]
    public async Task Sin_token_CSRF_no_se_registra_nada()
    {
        var auditoria = AuditoriaDeCore.Registra();

        var (codigo, _) = await Llamar(Aplicacion(Siempre(Verificada(Bogota)), auditoria), "POST", RutaAuditoria, PedidoUtc, conToken: false);

        Assert.Equal(StatusCodes.Status403Forbidden, codigo);
        Assert.Empty(auditoria.Pedidos);
    }

    [Theory]
    [InlineData("""{"zone":"UTC","dataRows":-1}""")]
    [InlineData("""{"zone":"UTC"}""")]
    [InlineData("""{"zone":"","dataRows":3}""")]
    public async Task Un_pedido_de_registro_incompleto_responde_400_sin_auditar(string json)
    {
        var auditoria = AuditoriaDeCore.Registra();

        var (codigo, _) = await Llamar(Aplicacion(Siempre(Verificada(Bogota)), auditoria), "POST", RutaAuditoria, JsonDocument.Parse(json).RootElement);

        Assert.Equal(StatusCodes.Status400BadRequest, codigo);
        Assert.Empty(auditoria.Pedidos);
    }

    private static WebApplication Aplicacion(IInstallationTimeZoneResolver? zona, IExportAudit? auditoria)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddRouting();
        // Los productores de CORE son opcionales: el host todavía no los registra.
        if (zona is not null) builder.Services.AddSingleton(zona);
        if (auditoria is not null) builder.Services.AddSingleton(auditoria);
        new ReportsModule().RegisterServices(builder.Services, new ConfigurationBuilder().Build());
        var app = builder.Build();
        new ReportsModule().MapEndpoints(app);
        return app;
    }

    private static async Task<(int Codigo, JsonElement Cuerpo)> Llamar(
        WebApplication app, string metodo, string ruta, object? cuerpo = null, bool conToken = true, CancellationToken abortada = default)
    {
        var destino = ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>()
            .Single(r => r.RoutePattern.RawText == ruta);
        await using var scope = app.Services.CreateAsyncScope();
        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = abortada };
        contexto.Request.Method = metodo;
        contexto.Request.Path = ruta;
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CsrfEndpointFilter.ClaimType, SessionTokens.Hash(Token))], "prueba"));
        if (conToken) contexto.Request.Headers[CsrfEndpointFilter.HeaderName] = Token;
        if (cuerpo is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cuerpo));
            contexto.Request.ContentType = "application/json";
            contexto.Request.ContentLength = bytes.Length;
            contexto.Request.Body = new MemoryStream(bytes);
            // DefaultHttpContext dice que no hay cuerpo si no se le declara.
            contexto.Features.Set<IHttpRequestBodyDetectionFeature>(new ConCuerpo());
        }
        using var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await destino.RequestDelegate!(contexto);

        return (contexto.Response.StatusCode, JsonDocument.Parse(salida.ToArray()).RootElement.Clone());
    }

    private sealed class ConCuerpo : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}
