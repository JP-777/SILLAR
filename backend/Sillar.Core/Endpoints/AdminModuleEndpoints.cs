using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Dtos;
using Sillar.Core.Modularity;
using Sillar.Core.Services;

namespace Sillar.Core.Endpoints;

/// <summary>Consulta y cambio de las activaciones de módulos.</summary>
/// <remarks>
/// <b>Los mensajes de error de estos endpoints son texto de interfaz.</b> El
/// panel los muestra tal cual —no los reescribe, para no tener la misma frase
/// en dos sitios—, así que se redactan para que los lea una persona que
/// administra su negocio, no un desarrollador leyendo un registro. Acortarlos a
/// algo técnico degrada la interfaz sin tocar el frontend.
/// </remarks>
public static class AdminModuleEndpoints
{
    /// <summary>Monta las rutas de módulos.</summary>
    public static IEndpointRouteBuilder MapAdminModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/modules")
            .WithTags("Módulos")
            .AddEndpointFilter<CsrfEndpointFilter>();

        group.MapGet("", List)
            .RequireAuthorization(AdminRole.Admin)
            .WithName("ListModules")
            .WithSummary("Lista el catálogo de módulos con su estado en esta instalación.")
            .WithDescription(
                "Devuelve activos e inactivos. 'canActivate', 'canDeactivate' y 'blockedBy' se calculan " +
                "en el servidor: el frontend no debe rehacer el análisis del grafo de dependencias.")
            .Produces<IReadOnlyList<ModuleResponse>>(StatusCodes.Status200OK);

        group.MapPost("/{code}/activate", Activate)
            .RequireAuthorization(AdminRole.SuperAdmin)
            .WithName("ActivateModule")
            .WithSummary("Activa un módulo.")
            .WithDescription(
                "El enrutamiento se construye al arrancar, así que el host se detiene tras responder " +
                "y el orquestador lo relanza. La respuesta indica en 'restart' qué va a ocurrir.")
            .Produces<ModuleActivationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{code}/deactivate", Deactivate)
            .RequireAuthorization(AdminRole.SuperAdmin)
            .WithName("DeactivateModule")
            .WithSummary("Desactiva un módulo.")
            .WithDescription(
                "No desactiva en cascada: si otro módulo activo depende de este de forma dura, " +
                "responde 409 nombrándolo y decide la persona en qué orden apagarlos.")
            .Produces<ModuleActivationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    /// <summary>Lista los módulos y su estado.</summary>
    /// <param name="modules">Servicio de activaciones.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>El catálogo completo, activos e inactivos.</returns>
    private static async Task<IResult> List(
        ModuleActivationService modules,
        CancellationToken cancellationToken)
        => Results.Ok(await modules.ListAsync(cancellationToken));

    /// <summary>Activa un módulo.</summary>
    /// <param name="code">Código del módulo.</param>
    /// <param name="modules">Servicio de activaciones.</param>
    /// <param name="restarter">Programador de la parada del host.</param>
    /// <param name="currentUser">Quién lo pide.</param>
    /// <param name="context">Petición en curso.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con el estado y el reinicio, 404 si no existe, 409 si el grafo no lo permite.</returns>
    private static Task<IResult> Activate(
        string code,
        ModuleActivationService modules,
        HostRestarter restarter,
        CurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
        => SetActive(code, activate: true, modules, restarter, currentUser, context, cancellationToken);

    /// <summary>Desactiva un módulo.</summary>
    /// <param name="code">Código del módulo.</param>
    /// <param name="modules">Servicio de activaciones.</param>
    /// <param name="restarter">Programador de la parada del host.</param>
    /// <param name="currentUser">Quién lo pide.</param>
    /// <param name="context">Petición en curso.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con el estado y el reinicio, 404 si no existe, 409 si el grafo no lo permite.</returns>
    private static Task<IResult> Deactivate(
        string code,
        ModuleActivationService modules,
        HostRestarter restarter,
        CurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
        => SetActive(code, activate: false, modules, restarter, currentUser, context, cancellationToken);

    private static async Task<IResult> SetActive(
        string code,
        bool activate,
        ModuleActivationService modules,
        HostRestarter restarter,
        CurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await modules.SetActiveAsync(
            code,
            activate,
            currentUser.AdminUserId!.Value,
            currentUser.Email!,
            cancellationToken);

        switch (result.Outcome)
        {
            case ActivationOutcome.NotFound:
                return Results.NotFound();

            case ActivationOutcome.Conflict:
                // Los códigos que bloquean viajan aparte del mensaje. El
                // servidor explica el motivo; convertirlos en nombres visibles y
                // en enlaces a su tarjeta es lo único que solo la interfaz puede
                // hacer, y para eso necesita los datos, no una frase.
                return Results.Problem(
                    title: result.Error,
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: result.BlockedBy is { Count: > 0 } blockedBy
                        ? new Dictionary<string, object?> { ["blockedBy"] = blockedBy }
                        : null);

            case ActivationOutcome.NoChange:
                // Ni reinicio ni auditoría: no ha pasado nada.
                return Results.Ok(new ModuleActivationResponse(
                    code,
                    result.IsActive,
                    RestartOutcome.None,
                    $"El módulo '{code}' ya estaba {(result.IsActive ? "activo" : "inactivo")}."));

            default:
                var verb = activate ? "activado" : "desactivado";
                restarter.ScheduleAfterResponse(context, $"Módulo '{code}' {verb}");

                return Results.Ok(restarter.RestartsAutomatically
                    ? new ModuleActivationResponse(
                        code,
                        result.IsActive,
                        RestartOutcome.Scheduled,
                        $"Módulo '{code}' {verb}. El sistema se está reiniciando para aplicar el cambio.")
                    : new ModuleActivationResponse(
                        code,
                        result.IsActive,
                        RestartOutcome.Required,
                        $"Módulo '{code}' {verb}. Hace falta reiniciar el sistema para aplicar el cambio."));
        }
    }
}
