using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Host.AssistantBridge;

namespace Sillar.Host.AssistantBridge.Tests;

public sealed class AssistantHostRouteCompositionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Sin_licencia_acreditada_no_existe_endpoint_protegido(
        bool modulePresent,
        bool moduleActive)
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        var mapperCalled = false;

        app.MapAssistantProtectedRoutes(
            new AssistantRouteCompositionState(
                modulePresent,
                moduleActive,
                AssistantHostEvidenceSnapshot.Denied(
                    AssistantHostDenialReason.PendingDecision)),
            endpoints =>
            {
                mapperCalled = true;
                endpoints.MapPost("/api/admin/assistant/articles", () => Results.NoContent());
            });

        var patterns = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.False(mapperCalled);
        Assert.DoesNotContain("/api/admin/assistant/articles", patterns);
    }

    [Fact]
    public void Estado_de_composicion_separa_presencia_activacion_y_licencia()
    {
        var missing = new AssistantRouteCompositionState(
            ModulePresent: false,
            ModuleActive: true,
            AssistantHostEvidenceSnapshot.Denied(AssistantHostDenialReason.Unresolved));
        var inactive = missing with { ModulePresent = true, ModuleActive = false };
        var unlicensed = missing with { ModulePresent = true, ModuleActive = true };

        Assert.False(missing.CanMountProtectedRoutes);
        Assert.False(inactive.CanMountProtectedRoutes);
        Assert.False(unlicensed.CanMountProtectedRoutes);
    }

    [Fact]
    public void Rol_administrativo_sin_identidad_vinculada_no_autoriza_al_actor()
    {
        var actor = new AssistantActorAuthorizationState(
            AdministrativeIdentityPresent: true,
            InstallationIdentityBound: false,
            ContextualPermissionGranted: true);

        Assert.False(actor.IsAuthorized);
    }
}
