using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Host.ReportsBridge;
using Sillar.Modules.Reports;
using Sillar.Shared.Replication;

namespace Sillar.Host.ReportsBridge.Tests;

public sealed class ReportsHostCompositionTests
{
    private const string CadenaSinServidor =
        "Host=127.0.0.1;Port=1;Database=no_se_usa;Username=nadie;Password=nada;Timeout=2";
    private const string ZoneRoute = "/api/admin/reporting/installation-zone";
    private const string AuditRoute = "/api/admin/reporting/orders/export-audit";
    private const string Token = "token-host-reports";

    [Fact]
    public void Sin_opt_in_el_host_no_publica_los_puertos_candidatos()
    {
        var services = new ServiceCollection();

        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IInstallationTimeZoneResolver>());
        Assert.Null(provider.GetService<IExportAudit>());
    }

    [Fact]
    public async Task La_composicion_real_publica_Lima_solo_como_inicial_y_bloquea_M10()
    {
        var writer = new RecordingWriter();
        await using var app = CreateHost(new MissingAdmin(), writer);

        var (zoneStatus, zoneBody) = await Call(app, "GET", ZoneRoute);
        var (auditStatus, auditBody) = await Call(
            app,
            "POST",
            AuditRoute,
            new { zone = IInstallationTimeZoneResolver.InitialZoneId, dataRows = 3 });

        Assert.Equal(StatusCodes.Status200OK, zoneStatus);
        Assert.Equal("approved_initial", zoneBody.GetProperty("kind").GetString());
        Assert.Equal(IInstallationTimeZoneResolver.InitialZoneId, zoneBody.GetProperty("zone").GetString());
        Assert.Equal(JsonValueKind.Null, zoneBody.GetProperty("canonical").ValueKind);
        Assert.Equal(StatusCodes.Status409Conflict, auditStatus);
        Assert.Contains("tiene que estar verificada", auditBody.GetProperty("title").GetString());
        Assert.Empty(writer.Entries);
    }

    [Fact]
    public async Task Sin_identidad_autoritativa_la_auditoria_real_deniega_y_nunca_registra()
    {
        var writer = new RecordingWriter();
        await using var provider = Provider(new MissingAdmin(), writer);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExportAudit>().RecordAsync(
            new ExportAuditRequest("reporting", "order_indicators", "csv", 3),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsRecorded);
        Assert.Null(result.ExportId);
        Assert.Equal(ExportAuditFailure.ActorMissing, result.Failure);
        Assert.Empty(writer.Entries);
    }

    [Fact]
    public async Task Si_la_auditoria_durable_falla_el_puerto_real_rechaza_sin_estado_recorded()
    {
        await using var provider = Provider(new FixedAdmin(), new FailingWriter());
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IExportAudit>().RecordAsync(
            new ExportAuditRequest("reporting", "order_indicators", "csv", 3),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsRecorded);
        Assert.Null(result.ExportId);
        Assert.Equal(ExportAuditFailure.AuditUnavailable, result.Failure);
    }

    private static WebApplication CreateHost(ICurrentAdmin admin, IAuditWriter writer)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddRouting();
        RegisterDependencies(builder.Services, admin, writer);
        builder.Services.AddReportsCoreCandidates();

        var reports = new ReportsModule();
        reports.RegisterServices(builder.Services, new ConfigurationBuilder().Build());
        var app = builder.Build();
        reports.MapEndpoints(app);
        return app;
    }

    private static ServiceProvider Provider(ICurrentAdmin admin, IAuditWriter writer)
    {
        var services = new ServiceCollection();
        RegisterDependencies(services, admin, writer);
        services.AddReportsCoreCandidates();
        return services.BuildServiceProvider();
    }

    private static void RegisterDependencies(
        IServiceCollection services,
        ICurrentAdmin admin,
        IAuditWriter writer)
    {
        services.AddLogging();
        services.AddSingleton<ISettingsReader>(new MissingSettings());
        services.AddSingleton(admin);
        services.AddSingleton(writer);
        services.AddScoped(_ => new CoreDbContext(
            CoreDataServiceExtensions.BuildOptions(CadenaSinServidor),
            new NodeIdentity("host-prueba-reports"),
            TimeProvider.System));
    }

    private static async Task<(int Status, JsonElement Body)> Call(
        WebApplication app,
        string method,
        string route,
        object? body = null)
    {
        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == route);
        await using var scope = app.Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = method;
        context.Request.Path = route;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(CsrfEndpointFilter.ClaimType, SessionTokens.Hash(Token))],
            "host-prueba"));
        context.Request.Headers[CsrfEndpointFilter.HeaderName] = Token;
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body));
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = bytes.Length;
            context.Request.Body = new MemoryStream(bytes);
            context.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestWithBody());
        }

        using var output = new MemoryStream();
        context.Response.Body = output;
        await endpoint.RequestDelegate!(context);
        return (context.Response.StatusCode, JsonDocument.Parse(output.ToArray()).RootElement.Clone());
    }

    private sealed class MissingSettings : ISettingsReader
    {
        public string? Get(string key) => null;

        public T? Get<T>(string key) => throw new NotSupportedException();

        public IReadOnlyDictionary<string, string> GetPublic() => throw new NotSupportedException();
    }

    private sealed class MissingAdmin : ICurrentAdmin
    {
        public int? AdminUserId => null;
        public string? Email => null;
        public string? Role => null;
        public string? DisplayName => null;
        public string? HomeNode => null;
        public bool IsInRole(string role) => false;
    }

    private sealed class FixedAdmin : ICurrentAdmin
    {
        public int? AdminUserId => 41;
        public string? Email => "auditor@host.test";
        public string? Role => AdminRole.Admin;
        public string? DisplayName => "Auditor Host";
        public string? HomeNode => "host-prueba-reports";
        public bool IsInRole(string role) => role is AdminRole.Editor or AdminRole.Admin;
    }

    private sealed class RecordingWriter : IAuditWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingWriter : IAuditWriter
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
            => throw new InvalidOperationException("auditoría durable no disponible");
    }

    private sealed class RequestWithBody : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}
