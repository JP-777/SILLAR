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
    private const string Token = "token-de-prueba";
    private const string Bogota = "America/Bogota";

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

        Assert.Equal("runtime_resolved", observada.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, observada.GetProperty("canonical").ValueKind);
        Assert.Empty(auditoria.Pedidos);
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

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, lectura);
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
