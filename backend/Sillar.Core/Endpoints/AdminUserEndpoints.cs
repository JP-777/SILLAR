using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Dtos;
using Sillar.Core.Services;

namespace Sillar.Core.Endpoints;

/// <summary>Administración de usuarios y de sus sesiones. Solo <c>super_admin</c>.</summary>
public static class AdminUserEndpoints
{
    /// <summary>Monta las rutas de usuarios y sesiones.</summary>
    public static IEndpointRouteBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var users = endpoints.MapGroup("/api/admin/users")
            .WithTags("Usuarios")
            .RequireAuthorization(AdminRole.SuperAdmin)
            .AddEndpointFilter<CsrfEndpointFilter>();

        users.MapGet("", List)
            .WithName("ListAdminUsers")
            .WithSummary("Lista los administradores.")
            .Produces<IReadOnlyList<AdminUserResponse>>(StatusCodes.Status200OK);

        users.MapPost("", Create)
            .WithName("CreateAdminUser")
            .WithSummary("Da de alta un administrador.")
            .WithDescription("La contraseña la fija quien lo crea y pasa la misma política que las demás.")
            .Produces<AdminUserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        users.MapPut("/{id:int}", Update)
            .WithName("UpdateAdminUser")
            .WithSummary("Modifica un administrador.")
            .WithDescription("Cambiar el rol o desactivar revoca sus sesiones de inmediato.")
            .Produces<AdminUserResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        users.MapDelete("/{id:int}", Deactivate)
            .WithName("DeactivateAdminUser")
            .WithSummary("Desactiva un administrador.")
            .WithDescription("Desactivación lógica: la cuenta no se borra nunca, y sus sesiones se revocan.")
            .Produces<AdminUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var sessions = endpoints.MapGroup("/api/admin/sessions")
            .WithTags("Sesiones")
            .RequireAuthorization(AdminRole.SuperAdmin)
            .AddEndpointFilter<CsrfEndpointFilter>();

        sessions.MapGet("", ListSessions)
            .WithName("ListAdminSessions")
            .WithSummary("Lista las sesiones, empezando por las vivas.")
            .Produces<IReadOnlyList<AdminSessionResponse>>(StatusCodes.Status200OK);

        sessions.MapDelete("/{id:guid}", RevokeSession)
            .WithName("RevokeAdminSession")
            .WithSummary("Revoca una sesión.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Lista los administradores.</summary>
    /// <param name="users">Servicio de usuarios.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>Los administradores, sin el hash de sus contraseñas.</returns>
    private static async Task<IResult> List(AdminUserService users, CancellationToken cancellationToken)
        => Results.Ok(await users.ListAsync(cancellationToken));

    /// <summary>Da de alta un administrador.</summary>
    /// <param name="request">Datos del usuario nuevo.</param>
    /// <param name="users">Servicio de usuarios.</param>
    /// <param name="currentUser">Quién está creando.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>201 con el usuario creado, 400 si los datos no sirven, 409 si el correo ya existe.</returns>
    private static async Task<IResult> Create(
        CreateAdminUserRequest request,
        AdminUserService users,
        CurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await users.CreateAsync(
            request,
            currentUser.AdminUserId!.Value,
            currentUser.Email!,
            cancellationToken);

        return result.Outcome switch
        {
            AdminUserOutcome.Ok => Results.Created($"/api/admin/users/{result.User!.Id}", result.User),
            AdminUserOutcome.Conflict => Problem(result.Error!, StatusCodes.Status409Conflict),
            _ => Invalid(result.Error!)
        };
    }

    /// <summary>Modifica un administrador.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="request">Datos nuevos.</param>
    /// <param name="users">Servicio de usuarios.</param>
    /// <param name="currentUser">Quién está modificando.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con el usuario, 404, 400 o 409 según el caso.</returns>
    private static async Task<IResult> Update(
        int id,
        UpdateAdminUserRequest request,
        AdminUserService users,
        CurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await users.UpdateAsync(
            id,
            request,
            currentUser.AdminUserId!.Value,
            currentUser.Email!,
            cancellationToken);

        return result.Outcome switch
        {
            AdminUserOutcome.Ok => Results.Ok(result.User),
            AdminUserOutcome.NotFound => Results.NotFound(),
            AdminUserOutcome.Conflict => Problem(result.Error!, StatusCodes.Status409Conflict),
            _ => Invalid(result.Error!)
        };
    }

    /// <summary>Desactiva un administrador.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="users">Servicio de usuarios.</param>
    /// <param name="currentUser">Quién está desactivando.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con el usuario desactivado, 404 o 409.</returns>
    private static async Task<IResult> Deactivate(
        int id,
        AdminUserService users,
        CurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await users.DeactivateAsync(
            id,
            currentUser.AdminUserId!.Value,
            currentUser.Email!,
            cancellationToken);

        return result.Outcome switch
        {
            AdminUserOutcome.Ok => Results.Ok(result.User),
            AdminUserOutcome.NotFound => Results.NotFound(),
            _ => Problem(result.Error!, StatusCodes.Status409Conflict)
        };
    }

    /// <summary>Lista las sesiones.</summary>
    /// <param name="users">Servicio de usuarios.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>Las sesiones, vivas primero.</returns>
    private static async Task<IResult> ListSessions(AdminUserService users, CancellationToken cancellationToken)
        => Results.Ok(await users.ListSessionsAsync(cancellationToken));

    /// <summary>Revoca una sesión.</summary>
    /// <param name="id">Identificador de la sesión.</param>
    /// <param name="users">Servicio de usuarios.</param>
    /// <param name="currentUser">Quién está revocando.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>204 si se revocó, 404 si no existía o ya estaba revocada.</returns>
    private static async Task<IResult> RevokeSession(
        Guid id,
        AdminUserService users,
        CurrentUser currentUser,
        CancellationToken cancellationToken)
        => await users.RevokeSessionAsync(id, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();

    private static IResult Invalid(string error) => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["usuario"] = [error] },
        title: "Los datos del usuario no son válidos.");

    private static IResult Problem(string error, int statusCode)
        => Results.Problem(title: error, statusCode: statusCode);
}
