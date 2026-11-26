using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Host.AssistantBridge;
using Sillar.Modules.Assistant;
using Sillar.Modules.Assistant.Endpoints;

using static Sillar.Api.Tests.HostDePrueba;

namespace Sillar.Api.Tests;

public sealed class AssistantNegativeDeploymentTests
{
    [Fact]
    public void Assistant_y_su_bridge_llegan_al_despliegue_sin_acreditar_licencia()
    {
        var hostDirectory = Path.GetDirectoryName(RutaDelBinario())!;

        Assert.True(File.Exists(Path.Combine(
            hostDirectory, "Sillar.Modules.Assistant.dll")));
        Assert.True(File.Exists(Path.Combine(
            hostDirectory, "Sillar.Host.AssistantBridge.dll")));
    }

    [Fact]
    public void Resolver_la_composicion_negativa_no_monta_rutas_editoriales()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAssistantHostCandidates();
        using var app = builder.Build();

        new AssistantModule().MapEndpoints(app);

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Where(pattern => pattern is not null)
            .ToArray();

        Assert.DoesNotContain(routes, route =>
            route!.StartsWith("/api/admin/assistant", StringComparison.Ordinal));

        using var scope = app.Services.CreateScope();
        var evidence = scope.ServiceProvider
            .GetRequiredService<ITrustedAssistantHostEvidence>();
        Assert.False(evidence.IsAssistantLicensed);
    }
}
