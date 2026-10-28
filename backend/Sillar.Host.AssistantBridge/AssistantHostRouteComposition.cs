using Microsoft.AspNetCore.Routing;

namespace Sillar.Host.AssistantBridge;

/// <summary>
/// Señales independientes que gobiernan la presencia física de rutas M12.
/// La autorización del actor sigue siendo una comprobación por petición y no
/// puede sustituir ninguna de estas tres barreras de arranque.
/// </summary>
public sealed record AssistantRouteCompositionState(
    bool ModulePresent,
    bool ModuleActive,
    AssistantHostEvidenceSnapshot LicenseEvidence)
{
    public bool CanMountProtectedRoutes =>
        ModulePresent && ModuleActive && LicenseEvidence.IsAssistantLicensed;
}

/// <summary>
/// Evaluación por petición, separada del montaje de rutas. Una sesión con rol
/// suficiente no acredita por sí sola la identidad de instalación ni el permiso
/// contextual sobre un artículo.
/// </summary>
public sealed record AssistantActorAuthorizationState(
    bool AdministrativeIdentityPresent,
    bool InstallationIdentityBound,
    bool ContextualPermissionGranted)
{
    public bool IsAuthorized => AdministrativeIdentityPresent
                                && InstallationIdentityBound
                                && ContextualPermissionGranted;
}

/// <summary>Composición Host de rutas protegidas de M12.</summary>
public static class AssistantHostRouteComposition
{
    /// <summary>
    /// Invoca el mapper de M12 solo con presencia, activación y licencia
    /// acreditadas. En FASE A el snapshot nunca acredita licencia, por lo que
    /// las rutas no se agregan al <see cref="EndpointDataSource"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapAssistantProtectedRoutes(
        this IEndpointRouteBuilder endpoints,
        AssistantRouteCompositionState state,
        Action<IEndpointRouteBuilder> mapLicensedRoutes)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(mapLicensedRoutes);

        if (state.CanMountProtectedRoutes)
        {
            mapLicensedRoutes(endpoints);
        }

        return endpoints;
    }
}
