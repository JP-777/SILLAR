using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Core.Modularity;
using Sillar.Core.Services;

namespace Sillar.Core.Endpoints;

public static class ModulePermissionEndpoints
{
    public sealed record PermissionRequest(bool? Allowed);
    public sealed record CurrentModulePermission(string ModuleCode, string[] Operations);

    public static IEndpointRouteBuilder MapModulePermissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var users = endpoints.MapGroup("/api/admin/users/{id:int}/module-permissions")
            .RequireAuthorization(AdminRole.SuperAdmin).WithTags("Permisos por módulo")
            .AddEndpointFilter<CsrfEndpointFilter>();
        users.MapGet("", (int id, ModulePermissionService service, CancellationToken ct) => service.ListAsync(id, ct))
            .WithName("ListModulePermissions").WithSummary("Consulta los permisos locales por operación de una cuenta.");
        users.MapPut("/{moduleCode}/{operationCode}", async (int id, string moduleCode, string operationCode,
            PermissionRequest request, ModulePermissionService service, CancellationToken ct) =>
        {
            await service.SetAsync(id, moduleCode, operationCode, request.Allowed, ct);
            return Results.NoContent();
        }).WithName("SetModulePermission").WithSummary("Establece un permiso local o restaura el heredado del rol, con auditoría.");
        endpoints.MapGet("/api/admin/module-permissions", async (DeclaredModules declared,
            IModuleAuthorization authority, CancellationToken ct) =>
        {
            var result = new List<CurrentModulePermission>();
            foreach (var module in declared.Modules)
            {
                var allowed = new List<string>();
                foreach (var operation in module.Operations)
                {
                    try { await authority.RequireAsync(module.Code, operation.Code, ct); allowed.Add(operation.Code); }
                    catch (ModuleAccessDeniedException e) when (e.StatusCode is 403 or 404) { }
                }
                if (allowed.Count > 0) result.Add(new(module.Code, allowed.ToArray()));
            }
            return result;
        }).RequireAuthorization(AdminRole.Editor).WithTags("Permisos por módulo")
            .WithName("GetCurrentModulePermissions").WithSummary("Devuelve únicamente las operaciones actualmente permitidas a la sesión.");
        return endpoints;
    }
}
