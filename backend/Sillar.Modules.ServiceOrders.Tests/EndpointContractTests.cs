using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders.Application;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Documentation;
using Sillar.Modules.Services.Contracts;

namespace Sillar.Modules.ServiceOrders.Tests;

public sealed class EndpointContractTests
{
    private static IReadOnlyList<(string Route, string Method, string[] Policies)> Routes()
    {
        var builder = WebApplication.CreateSlimBuilder();

        builder.Configuration["ConnectionStrings:Default"] =
            "Host=localhost;Database=not_used;Username=none;Password=none";

        builder.Configuration["Sillar:Node:Code"] = "principal";

        builder.Services.AddRouting();

        builder.Services.AddScoped<IServiceShowcaseSnapshots>(
            _ => throw new InvalidOperationException("no se ejecuta"));

        builder.Services.AddScoped<ISettingsReader>(
            _ => throw new InvalidOperationException("no se ejecuta"));

        builder.Services.AddScoped<IAuditWriter>(
            _ => throw new InvalidOperationException("no se ejecuta"));

        builder.Services.AddScoped<ICurrentAdmin>(
            _ => throw new InvalidOperationException("no se ejecuta"));

        builder.Services.AddSingleton(TimeProvider.System);

        var module = new ServiceOrdersModule();
        module.RegisterServices(builder.Services, builder.Configuration);

        var app = builder.Build();
        module.MapEndpoints(app);

        return
        [
            .. ((IEndpointRouteBuilder)app)
                .DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(endpoint =>
                    endpoint.RoutePattern.RawText!
                        .StartsWith(
                            "/api/admin/service-orders",
                            StringComparison.Ordinal))
                .Select(endpoint => (
                    Route: endpoint.RoutePattern.RawText!,
                    Method: endpoint.Metadata
                        .GetMetadata<HttpMethodMetadata>()!
                        .HttpMethods
                        .Single(),
                    Policies: endpoint.Metadata
                        .GetOrderedMetadata<IAuthorizeData>()
                        .Select(value => value.Policy ?? string.Empty)
                        .ToArray()))
        ];
    }

    [Fact]
    public void Api_exposes_exactly_the_seven_ratified_routes()
    {
        var routes = Routes()
            .Select(route => $"{route.Method} {route.Route}")
            .Order()
            .ToArray();

        Assert.Equal(
            [
                "GET /api/admin/service-orders",
                "GET /api/admin/service-orders/{id:guid}",
                "POST /api/admin/service-orders",
                "POST /api/admin/service-orders/{id:guid}/take",
                "POST /api/admin/service-orders/{id:guid}/transition",
                "POST /api/admin/service-orders/{id:guid}/unassign",
                "PUT /api/admin/service-orders/{id:guid}"
            ],
            routes);
    }

    [Fact]
    public void Every_service_order_route_requires_editor_or_higher()
    {
        var routes = Routes();

        Assert.Equal(7, routes.Count);

        Assert.All(
            routes,
            route => Assert.Contains(AdminRole.Editor, route.Policies));
    }

    [Fact]
    public void Module_registers_the_tracking_contract_for_future_M06()
    {
        var builder = WebApplication.CreateSlimBuilder();

        builder.Configuration["ConnectionStrings:Default"] =
            "Host=localhost;Database=not_used;Username=none;Password=none";

        var module = new ServiceOrdersModule();
        module.RegisterServices(builder.Services, builder.Configuration);

        Assert.Contains(
            builder.Services,
            descriptor =>
                descriptor.ServiceType == typeof(IServiceOrderTrackingSource));
    }

    [Fact]
    public void Swagger_provider_has_valid_examples_for_every_M05b_request_body()
    {
        var examples = new ServiceOrderExamples().Examples;

        var required = new[]
        {
            typeof(CreateServiceOrderRequest),
            typeof(UpdateServiceOrderRequest),
            typeof(ServiceOrderConcurrencyRequest),
            typeof(ServiceOrderTransitionRequest)
        };

        Assert.Equal(
            required.OrderBy(type => type.FullName),
            examples.Keys.OrderBy(type => type.FullName));

        foreach (var type in required)
        {
            Assert.True(
                examples.TryGetValue(type, out var json),
                $"Falta ejemplo para {type.Name}.");

            using var document = JsonDocument.Parse(json!);

            Assert.Equal(
                JsonValueKind.Object,
                document.RootElement.ValueKind);
        }
    }
}
