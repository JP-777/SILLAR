using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Dtos;
using Sillar.Core.Modularity;
using Sillar.Core.Services;

namespace Sillar.Core.Endpoints;

/// <summary>Rutas del modo instalación.</summary>
/// <remarks>
/// <c>POST /api/setup</c> solo existe mientras la instalación esté pendiente: una
/// vez completada deja de existir, y por eso responde 404 y no 403 — lo que no
/// está disponible no existe. <c>GET /api/setup/status</c> es distinto por
/// naturaleza: existe justamente para que algo que todavía no sabe en qué modo
/// está el servidor pueda preguntarlo, así que se monta en los dos modos —ver
/// <see cref="MapAlwaysAvailableStatus"/> para su versión en modo normal—, que
/// es la que responde casi siempre y nunca en 404.
/// </remarks>
public static class SetupEndpoints
{
    /// <summary>Monta <c>/api/setup</c> y <c>/api/setup/status</c> para el modo instalación.</summary>
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/setup").WithTags("Instalación");

        group.MapGet("/status", GetStatus)
            .WithName("GetSetupStatus")
            .WithSummary("Indica que la instalación sigue pendiente.")
            .WithDescription("Público. En modo normal responde la misma ruta, ver MapAlwaysAvailableStatus.")
            .Produces<SetupStatusResponse>(StatusCodes.Status200OK);

        group.MapPost("", Complete)
            .WithName("CompleteSetup")
            .WithSummary("Crea la instalación y su primer super_admin.")
            .WithDescription(
                "Todo en una transacción. No inicia sesión: devuelve 201 y el frontend redirige al login. " +
                "Al terminar, el host se detiene para volver a arrancar en modo normal.")
            .Produces<SetupResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Monta <c>GET /api/setup/status</c> en modo normal, siempre en 200.</summary>
    /// <remarks>
    /// El arranque de la interfaz (F-08 §5) empieza preguntando esta ruta antes de
    /// saber si el servidor está en modo instalación o modo normal — es la propia
    /// pregunta que decide qué pantalla montar. Si en modo normal no existiera,
    /// cada arranque de la aplicación generaría un 404 real, visible en la consola
    /// del navegador, por una pregunta que la interfaz siempre hace y que no es un
    /// error de nadie. Por eso esta ruta, a diferencia del resto de
    /// <see cref="MapSetupEndpoints"/>, se monta en ambos modos.
    /// </remarks>
    public static IEndpointRouteBuilder MapAlwaysAvailableStatus(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/setup/status", () => Results.Ok(new SetupStatusResponse(false)))
            .WithTags("Instalación")
            .WithName("GetSetupStatusCompleted")
            .WithSummary("Indica que la instalación ya está completa.")
            .Produces<SetupStatusResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    /// <summary>Dice si la instalación sigue pendiente.</summary>
    /// <param name="setup">Servicio de instalación.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>El estado de la instalación, o 404 si ya se completó.</returns>
    private static async Task<IResult> GetStatus(SetupService setup, CancellationToken cancellationToken)
        => await setup.IsSetupRequiredAsync(cancellationToken)
            ? Results.Ok(new SetupStatusResponse(true))
            : Results.NotFound();

    /// <summary>Completa la instalación.</summary>
    /// <param name="request">Negocio, licencia y primer administrador.</param>
    /// <param name="setup">Servicio de instalación.</param>
    /// <param name="restarter">Programador de la parada del host.</param>
    /// <param name="context">Petición en curso.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>201 con los datos creados, 400 si los datos no sirven, 404 si ya estaba instalado.</returns>
    private static async Task<IResult> Complete(
        SetupRequest request,
        SetupService setup,
        HostRestarter restarter,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await setup.CompleteAsync(request, cancellationToken);

        switch (result.Outcome)
        {
            case SetupOutcome.Invalid:
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["instalacion"] = [result.Error!] },
                    title: "Los datos de instalación no son válidos.");

            case SetupOutcome.AlreadyInstalled:
                // Segunda petición simultánea, o alguien que llega tarde.
                return Results.NotFound();

            default:
                // Sin consultar Modules:RestartAfterActivation: terminar la
                // instalación cambia el modo de arranque, así que el host tiene
                // que volver a levantarse aunque estemos en desarrollo.
                restarter.StopAfterResponse(context, "Instalación completada");

                return Results.Created("/api/admin/auth/login", result.Response);
        }
    }
}
