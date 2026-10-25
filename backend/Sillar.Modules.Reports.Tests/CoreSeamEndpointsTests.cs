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

namespace Sillar.Modules.Reports.Tests;

/// <summary>
/// La costura con CORE, con los productores candidatos sustituidos por dobles:
/// la zona se traduce sin inventar verificación, y la exportación solo se
/// registra con zona verificada y una auditoría que confirma.
/// </summary>
public sealed class CoreSeamEndpointsTests
{
    private const string RutaZona = "/api/admin/reporting/installation-zone";
    private const string RutaAuditoria = "/api/admin/reporting/orders/export-audit";
    private const string Token = "token-de-prueba";

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
        var (_, cuerpo) = await Llamar(Aplicacion(Zona.Inicial(), auditoria: null), "GET", RutaZona);

        Assert.Equal("approved_initial", cuerpo.GetProperty("kind").GetString());
        Assert.Equal("America/Lima", cuerpo.GetProperty("zone").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("canonical").ValueKind);
    }

    [Fact]
    public async Task Una_zona_configurada_sin_verificar_conserva_el_original_y_no_se_da_por_canonica()
    {
        var (_, cuerpo) = await Llamar(Aplicacion(Zona.Configurada("America/Bogota"), auditoria: null), "GET", RutaZona);

        Assert.Equal("runtime_resolved", cuerpo.GetProperty("kind").GetString());
        Assert.Equal("America/Bogota", cuerpo.GetProperty("original").GetString());
        Assert.Equal("America/Bogota", cuerpo.GetProperty("runtimeId").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("canonical").ValueKind);
    }

    [Fact]
    public async Task Un_valor_configurado_invalido_sale_invalido_con_su_texto_y_sin_zona()
    {
        var (_, cuerpo) = await Llamar(Aplicacion(Zona.Invalida("Hora de Lima"), auditoria: null), "GET", RutaZona);

        Assert.Equal("invalid", cuerpo.GetProperty("kind").GetString());
        Assert.Equal("Hora de Lima", cuerpo.GetProperty("original").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("zone").ValueKind);
    }

    [Fact]
    public async Task Solo_una_resolucion_verificada_por_CORE_sale_verificada()
    {
        var (_, cuerpo) = await Llamar(Aplicacion(Zona.Verificada("America/Bogota"), auditoria: null), "GET", RutaZona);

        Assert.Equal("verified", cuerpo.GetProperty("kind").GetString());
        Assert.Equal("America/Bogota", cuerpo.GetProperty("canonical").GetString());
    }

    public static TheoryData<string> ZonasNoVerificadas => ["sin_conexion", "inicial", "configurada"];

    [Theory]
    [MemberData(nameof(ZonasNoVerificadas))]
    public async Task Sin_zona_verificada_la_exportacion_no_se_registra_aunque_la_auditoria_confirme(string caso)
    {
        var zona = caso switch
        {
            "inicial" => Zona.Inicial(),
            "configurada" => Zona.Configurada("America/Lima"),
            _ => null,
        };
        var auditoria = new Auditoria(ExportAuditResult.Recorded(Guid.CreateVersion7()));

        var (codigo, cuerpo) = await Llamar(Aplicacion(zona, auditoria), "POST", RutaAuditoria, new { zone = "America/Lima", dataRows = 11 });

        Assert.Equal(StatusCodes.Status409Conflict, codigo);
        Assert.Contains("tiene que estar verificada", cuerpo.GetProperty("title").GetString());
        Assert.Empty(auditoria.Pedidos);
    }

    [Theory]
    [InlineData("America/Lima")]
    [InlineData("america/bogota")]
    [InlineData("Europe/Madrid")]
    public async Task Con_zona_verificada_un_archivo_en_otra_zona_no_se_registra(string zonaDelArchivo)
    {
        var auditoria = new Auditoria(ExportAuditResult.Recorded(Guid.CreateVersion7()));

        var (codigo, _) = await Llamar(Aplicacion(Zona.Verificada("America/Bogota"), auditoria), "POST", RutaAuditoria, new { zone = zonaDelArchivo, dataRows = 11 });

        Assert.Equal(StatusCodes.Status409Conflict, codigo);
        Assert.Empty(auditoria.Pedidos);
    }

    [Fact]
    public async Task Con_zona_verificada_y_registro_confirmado_responde_recorded_sin_filtros_ni_contenido()
    {
        var id = Guid.CreateVersion7();
        var auditoria = new Auditoria(ExportAuditResult.Recorded(id));

        var (codigo, cuerpo) = await Llamar(Aplicacion(Zona.Verificada("America/Bogota"), auditoria), "POST", RutaAuditoria, new { zone = "UTC", dataRows = 11 });

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("recorded", cuerpo.GetProperty("decision").GetString());
        Assert.Equal(id, cuerpo.GetProperty("exportId").GetGuid());
        Assert.Equal(new ExportAuditRequest("reporting", "order_indicators", "csv", 11), Assert.Single(auditoria.Pedidos));
    }

    [Fact]
    public async Task Si_CORE_no_puede_escribir_la_auditoria_responde_503_y_no_recorded()
    {
        var auditoria = new Auditoria(ExportAuditResult.Rejected(ExportAuditFailure.AuditUnavailable));

        var (codigo, cuerpo) = await Llamar(Aplicacion(Zona.Verificada("America/Bogota"), auditoria), "POST", RutaAuditoria, new { zone = "America/Bogota", dataRows = 11 });

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
        Assert.False(cuerpo.TryGetProperty("decision", out _));
    }

    [Fact]
    public async Task Sin_productor_de_auditoria_responde_503_aunque_la_zona_este_verificada()
    {
        var (codigo, _) = await Llamar(Aplicacion(Zona.Verificada("America/Bogota"), auditoria: null), "POST", RutaAuditoria, new { zone = "UTC", dataRows = 11 });

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, codigo);
    }

    [Theory]
    [InlineData(ExportAuditFailure.ActorMissing)]
    [InlineData(ExportAuditFailure.MetadataInvalid)]
    public async Task Si_CORE_se_niega_a_registrarla_responde_denied(ExportAuditFailure causa)
    {
        var auditoria = new Auditoria(ExportAuditResult.Rejected(causa));

        var (codigo, cuerpo) = await Llamar(Aplicacion(Zona.Verificada("America/Bogota"), auditoria), "POST", RutaAuditoria, new { zone = "UTC", dataRows = 11 });

        Assert.Equal(StatusCodes.Status200OK, codigo);
        Assert.Equal("denied", cuerpo.GetProperty("decision").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("exportId").ValueKind);
    }

    [Fact]
    public async Task Sin_token_CSRF_no_se_registra_nada()
    {
        var auditoria = new Auditoria(ExportAuditResult.Recorded(Guid.CreateVersion7()));

        var (codigo, _) = await Llamar(Aplicacion(Zona.Verificada("America/Bogota"), auditoria), "POST", RutaAuditoria, new { zone = "UTC", dataRows = 11 }, conToken: false);

        Assert.Equal(StatusCodes.Status403Forbidden, codigo);
        Assert.Empty(auditoria.Pedidos);
    }

    [Theory]
    [InlineData("""{"zone":"UTC","dataRows":-1}""")]
    [InlineData("""{"zone":"UTC"}""")]
    [InlineData("""{"zone":"","dataRows":3}""")]
    public async Task Un_pedido_de_registro_incompleto_responde_400_sin_auditar(string json)
    {
        var auditoria = new Auditoria(ExportAuditResult.Recorded(Guid.CreateVersion7()));

        var (codigo, _) = await Llamar(Aplicacion(Zona.Verificada("America/Bogota"), auditoria), "POST", RutaAuditoria, JsonDocument.Parse(json).RootElement);

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
        WebApplication app, string metodo, string ruta, object? cuerpo = null, bool conToken = true)
    {
        var destino = ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>()
            .Single(r => r.RoutePattern.RawText == ruta);
        await using var scope = app.Services.CreateAsyncScope();
        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
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

    /// <summary>Resoluciones con la forma que produce el candidato de CORE.</summary>
    private sealed class Zona(InstallationTimeZoneResolution resolucion) : IInstallationTimeZoneResolver
    {
        public InstallationTimeZoneResolution Resolve() => resolucion;

        public static Zona Inicial() => Resuelta("America/Lima", InstallationTimeZoneOrigin.ApprovedInitial,
            InstallationTimeZoneVerification.Unverified, new(IanaZoneOutcome.Missing, null, null));

        public static Zona Configurada(string id) => Resuelta(id, InstallationTimeZoneOrigin.ConfiguredSetting,
            InstallationTimeZoneVerification.Unverified, new(IanaZoneOutcome.RuntimeResolvedCandidate, id, id));

        /// <summary>Hoy CORE no la produce: reservada a una autoridad ratificada.</summary>
        public static Zona Verificada(string id) => Resuelta(id, InstallationTimeZoneOrigin.ConfiguredSetting,
            InstallationTimeZoneVerification.Verified, new(IanaZoneOutcome.IanaCanonicalVerified, id, id));

        public static Zona Invalida(string valor) => new(new(null, null, null, InstallationTimeZoneVerification.Unverified,
            InstallationTimeZoneFailure.ConfiguredValueInvalid, new(IanaZoneOutcome.Invalid, valor, null)));

        private static Zona Resuelta(string id, InstallationTimeZoneOrigin origen, InstallationTimeZoneVerification verificacion, IanaZoneEvaluation leido)
            => new(new(TimeZoneInfo.FindSystemTimeZoneById(id), id, origen, verificacion, null, leido));
    }

    private sealed class ConCuerpo : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private sealed class Auditoria(ExportAuditResult respuesta) : IExportAudit
    {
        public List<ExportAuditRequest> Pedidos { get; } = [];

        public Task<ExportAuditResult> RecordAsync(ExportAuditRequest request, CancellationToken cancellationToken)
        {
            Pedidos.Add(request);
            return Task.FromResult(respuesta);
        }
    }
}
