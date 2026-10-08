using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Sillar.Api.Documentation;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.Tracking.Application;
using Sillar.Modules.Tracking.Dtos;
using Sillar.Modules.Tracking.Endpoints;
using Swashbuckle.AspNetCore.Swagger;

namespace Sillar.Modules.Tracking.Tests;

public sealed class ApiYFronterasTests
{
    [Fact]
    public void La_api_publica_exactamente_las_siete_rutas_administrativas_ratificadas()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<TrackingApplicationService>();
        var app = builder.Build();
        new TrackingModule().MapEndpoints(app);

        var routeEndpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
        var endpoints = routeEndpoints
            .Select(endpoint => $"{string.Join(',', endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)} {endpoint.RoutePattern.RawText}")
            .Order()
            .ToArray();

        Assert.Equal(
            [
                "DELETE /api/admin/tracking/notes/{noteId:guid}",
                "GET /api/admin/tracking/board",
                "GET /api/admin/tracking/orders/{serviceOrderId:guid}",
                "POST /api/admin/tracking/orders/{serviceOrderId:guid}/notes",
                "PUT /api/admin/tracking/orders/{serviceOrderId:guid}/due",
                "PUT /api/admin/tracking/orders/{serviceOrderId:guid}/priority",
                "PUT /api/admin/tracking/orders/{serviceOrderId:guid}/status"
            ],
            endpoints);

        Assert.All(routeEndpoints, endpoint => Assert.Contains(
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            authorization => authorization.Policy == Sillar.Core.Contracts.AdminRole.Editor));
        var delete = routeEndpoints.Single(endpoint =>
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("DELETE"));
        Assert.Contains(
            delete.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            authorization => authorization.Policy == Sillar.Core.Contracts.AdminRole.Admin);
    }

    [Fact]
    public async Task B8_el_OpenAPI_real_publica_las_siete_rutas_y_los_cuatro_ejemplos_copiables()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<TrackingApplicationService>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "M06 prueba", Version = "v1" });
            options.SchemaFilter<ModuleSchemaExamples>();
        });

        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        new TrackingModule().MapEndpoints(app);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var paths = document.Paths.Keys
            .Where(path => path.StartsWith("/api/admin/tracking", StringComparison.Ordinal))
            .Order()
            .ToArray();
        Assert.Equal(
            [
                "/api/admin/tracking/board",
                "/api/admin/tracking/notes/{noteId}",
                "/api/admin/tracking/orders/{serviceOrderId}",
                "/api/admin/tracking/orders/{serviceOrderId}/due",
                "/api/admin/tracking/orders/{serviceOrderId}/notes",
                "/api/admin/tracking/orders/{serviceOrderId}/priority",
                "/api/admin/tracking/orders/{serviceOrderId}/status"
            ],
            paths);

        var requestNames = new[]
        {
            nameof(SetTrackingPriorityRequest),
            nameof(SetTrackingDueRequest),
            nameof(AddTrackingNoteRequest),
            nameof(TransitionTrackingStatusRequest)
        };
        foreach (var requestName in requestNames)
        {
            var schemas = Assert.IsAssignableFrom<
                IReadOnlyDictionary<string, IOpenApiSchema>>(document.Components!.Schemas);
            var schema = Assert.Single(
                schemas,
                candidate => candidate.Key.EndsWith(requestName, StringComparison.Ordinal)).Value;
            Assert.NotNull(schema.Example);
            var example = schema.Example.ToString();
            Assert.StartsWith("{", example.Trim(), StringComparison.Ordinal);
        }

        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Invalid_de_M05b_es_el_mensaje_principal_observable_sin_reinterpretacion()
    {
        const string message = "Frase exacta de M05b.";
        var operation = new ServiceOrderOperation<TransitionTrackingStatusResponse>(
            ServiceOrderOutcome.Invalid,
            message);

        var result = Assert.IsType<ProblemHttpResult>(TrackingEndpoints.TransitionResult(operation));
        var details = Assert.IsType<HttpValidationProblemDetails>(result.ProblemDetails);

        Assert.Equal(message, details.Title);
        Assert.Equal([message], details.Errors["status"]);
    }

    [Fact]
    public void B2_y_B3_la_capa_productiva_solo_conoce_Contracts_y_no_ejecuta_SQL_ajeno()
    {
        var root = RepoRoot();
        var project = File.ReadAllText(Path.Combine(
            root,
            "backend",
            "Sillar.Modules.Tracking",
            "Sillar.Modules.Tracking.csproj"));

        Assert.Contains("Sillar.Modules.ServiceOrders.Contracts", project, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Sillar.Modules.ServiceOrders\\Sillar.Modules.ServiceOrders.csproj",
            project,
            StringComparison.Ordinal);

        var product = Path.Combine(root, "backend", "Sillar.Modules.Tracking");
        var sourceFiles = Directory.EnumerateFiles(product, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();
        var source = string.Join('\n', sourceFiles.Select(File.ReadAllText));

        Assert.DoesNotContain("Sillar.Modules.ServiceOrders.Domain", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sillar.Modules.ServiceOrders.Data", source, StringComparison.Ordinal);
        Assert.DoesNotContain("service_orders.", source, StringComparison.OrdinalIgnoreCase);

        var references = typeof(TrackingModule).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();
        Assert.Contains("Sillar.Modules.ServiceOrders.Contracts", references);
        Assert.DoesNotContain("Sillar.Modules.ServiceOrders", references);
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "backend", "Sillar.Modules.Tracking")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
