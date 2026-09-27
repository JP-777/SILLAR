using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Services.Domain;
using Sillar.Modules.Services.Dtos;
using Sillar.Modules.Services.Services;

namespace Sillar.Modules.Services.Endpoints;
public static class ServiceEndpoints
{
    public static IEndpointRouteBuilder MapServiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/services", async (ServiceShowcaseService service, CancellationToken ct) => Results.Ok(await service.ListPublicAsync(ct)))
            .WithTags("Servicios").WithName("ListPublicServices").WithSummary("Lista la vitrina publicada.");
        endpoints.MapGet("/api/services/{slug}", async (string slug, ServiceShowcaseService service, CancellationToken ct) =>
            await service.GetPublicAsync(slug, ct) is { } value ? Results.Ok(value) : Results.NotFound())
            .WithTags("Servicios").WithName("GetPublicService").WithSummary("Obtiene un servicio publicado.");
        var admin = endpoints.MapGroup("/api/admin/services").WithTags("Servicios — Administración")
            .RequireAuthorization(AdminRole.Editor).AddEndpointFilter<CsrfEndpointFilter>();
        admin.MapGet("", async (ServiceShowcaseService service, CancellationToken ct) => Results.Ok(await service.ListAdminAsync(ct))).WithName("ListAdminServices");
        admin.MapGet("/{id:int}", async (int id, ServiceShowcaseService service, CancellationToken ct) =>
            await service.GetAdminAsync(id, ct) is { } value ? Results.Ok(value) : Results.NotFound()).WithName("GetAdminService");
        admin.MapPost("", Create).WithName("CreateService");
        admin.MapPut("/{id:int}", Update).WithName("UpdateService");
        admin.MapPost("/{id:int}/publish", (int id, ServiceShowcaseService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct) => Transition(id, PublicationState.Published, "Publicación", s, a, u, ct)).WithName("PublishService");
        admin.MapPost("/{id:int}/unpublish", (int id, ServiceShowcaseService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct) => Transition(id, PublicationState.Draft, "Retiro", s, a, u, ct)).WithName("UnpublishService");
        admin.MapPost("/{id:int}/archive", (int id, ServiceShowcaseService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct) => Transition(id, PublicationState.Archived, "Archivo", s, a, u, ct)).WithName("ArchiveService");
        admin.MapPut("/order", Reorder).WithName("ReorderServices");
        return endpoints;
    }

    private static async Task<IResult> Create(SaveServiceRequest request, ServiceShowcaseService service, IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var op = await service.CreateAsync(request, ct);
        if (op.Outcome == ServiceOutcome.Ok) await Audit(audit, user, AuditAction.Create, op.Value!, "Alta", ct);
        return Result(op, x => Results.Created($"/api/admin/services/{x.Id}", x));
    }
    private static async Task<IResult> Update(int id, SaveServiceRequest request, ServiceShowcaseService service, IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var op = await service.UpdateAsync(id, request, ct);
        if (op.Outcome == ServiceOutcome.Ok) await Audit(audit, user, AuditAction.Update, op.Value!, "Modificación", ct);
        return Result(op, Results.Ok);
    }
    private static async Task<IResult> Transition(int id, PublicationState target, string action, ServiceShowcaseService service, IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var op = await service.TransitionAsync(id, target, ct);
        if (op.Outcome == ServiceOutcome.Ok) await Audit(audit, user, AuditAction.Update, op.Value!, action, ct);
        return Result(op, Results.Ok);
    }
    private static async Task<IResult> Reorder(ReorderServicesRequest request, ServiceShowcaseService service, IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var op = await service.ReorderAsync(request, ct);
        if (op.Outcome == ServiceOutcome.Ok) await audit.WriteAsync(new AuditEntry(AuditAction.Update) { AdminUserId = user.AdminUserId, AdminUserEmail = user.Email, ModuleCode = ServicesModule.ModuleCode, EntityType = "service_entry", Summary = "Reordenamiento completo de la vitrina de servicios." }, ct);
        return op.Outcome == ServiceOutcome.Ok ? Results.Ok(op.Value) : Results.Problem(title: op.Error, statusCode: 409);
    }
    private static IResult Result<T>(ServiceOperation<T> op, Func<T, IResult> success) => op.Outcome switch
    {
        ServiceOutcome.Ok => success(op.Value!), ServiceOutcome.NotFound => Results.NotFound(),
        ServiceOutcome.Conflict => Results.Problem(title: op.Error, statusCode: 409),
        _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["service"] = [op.Error!] }, title: "Los datos del servicio no son válidos.")
    };
    private static Task Audit(IAuditWriter audit, ICurrentAdmin user, string action, ServiceAdminResponse value, string verb, CancellationToken ct) =>
        audit.WriteAsync(new AuditEntry(action) { AdminUserId = user.AdminUserId, AdminUserEmail = user.Email, ModuleCode = ServicesModule.ModuleCode,
            EntityType = "service_entry", EntityId = value.Id.ToString(), Summary = $"{verb} del servicio «{value.Name}»." }, ct);
}
