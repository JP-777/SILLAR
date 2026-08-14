using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sillar.Core.Dtos;
using Sillar.Core.Services;

namespace Sillar.Core.Endpoints;

/// <summary>Rutas del modo instalación.</summary>
/// <remarks>
/// El host solo las monta mientras la instalación esté pendiente. Una vez
/// completada dejan de existir, y por eso responden 404 y no 403: lo que no está
/// disponible no existe.
/// </remarks>
public static class SetupEndpoints
{
    /// <summary>Monta <c>/api/setup</c> y <c>/api/setup/status</c>.</summary>
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/setup").WithTags("Instalación");

        group.MapGet("/status", GetStatus)
            .WithName("GetSetupStatus")
            .WithSummary("Indica si queda instalación pendiente.")
            .WithDescription("Público. Deja de responder en cuanto la instalación se completa.")
            .Produces<SetupStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

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
    /// <param name="lifetime">Control del ciclo de vida del host.</param>
    /// <param name="logger">Registro de la aplicación.</param>
    /// <param name="context">Petición en curso.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>201 con los datos creados, 400 si los datos no sirven, 404 si ya estaba instalado.</returns>
    private static async Task<IResult> Complete(
        SetupRequest request,
        SetupService setup,
        IHostApplicationLifetime lifetime,
        ILogger<SetupRequest> logger,
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
                // La parada se pide DESPUÉS de enviar la respuesta: si se pidiera
                // aquí, el cliente se quedaría sin su 201.
                context.Response.OnCompleted(() =>
                {
                    logger.LogWarning(
                        "Instalación completada. El host se detiene para arrancar en modo normal. " +
                        "En Docker el contenedor reinicia solo; en desarrollo hay que volver a lanzarlo.");
                    lifetime.StopApplication();
                    return Task.CompletedTask;
                });

                return Results.Created("/api/admin/auth/login", result.Response);
        }
    }
}
