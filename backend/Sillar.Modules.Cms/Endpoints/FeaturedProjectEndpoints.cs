using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Cms.Dtos;
using Sillar.Modules.Cms.Services;

namespace Sillar.Modules.Cms.Endpoints;

/// <summary>Trabajos destacados públicos y administración editorial.</summary>
public static class FeaturedProjectEndpoints
{
    /// <summary>Monta todas las rutas de trabajos destacados.</summary>
    public static IEndpointRouteBuilder MapFeaturedProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/cms/featured-projects", ListPublic)
            .WithTags("Contenido — Trabajos").WithName("ListPublicCmsFeaturedProjects")
            .WithSummary("Trabajos activos, en el orden de la galería.")
            .Produces<IReadOnlyList<FeaturedProjectResponse>>();
        var admin = endpoints.MapGroup("/api/admin/cms/featured-projects")
            .WithTags("Contenido — Trabajos")
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();
        admin.MapGet("", List).WithName("ListAdminCmsFeaturedProjects")
            .WithSummary("Lista todos los trabajos, incluidos los inactivos.")
            .Produces<IReadOnlyList<FeaturedProjectAdminResponse>>();
        admin.MapGet("/{id:int}", Get).WithName("GetAdminCmsFeaturedProject")
            .WithSummary("Obtiene un trabajo para editar.")
            .Produces<FeaturedProjectAdminResponse>().Produces(StatusCodes.Status404NotFound);
        admin.MapPost("", Create).WithName("CreateCmsFeaturedProject")
            .WithSummary("Crea un trabajo al final de la galería.")
            .Produces<FeaturedProjectAdminResponse>(StatusCodes.Status201Created).ProducesValidationProblem();
        admin.MapPut("/{id:int}", Update).WithName("UpdateCmsFeaturedProject")
            .WithSummary("Modifica un trabajo sin cambiar su orden ni su estado activo.")
            .Produces<FeaturedProjectAdminResponse>().ProducesValidationProblem().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/order", Reorder).WithName("ReorderCmsFeaturedProjects")
            .WithSummary("Sustituye atómicamente el orden completo de los trabajos.")
            .Produces<IReadOnlyList<int>>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapDelete("/{id:int}", Deactivate).WithName("DeactivateCmsFeaturedProject")
            .WithSummary("Desactiva un trabajo sin borrarlo.")
            .RequireAuthorization(AdminRole.Admin)
            .Produces<FeaturedProjectAdminResponse>().Produces(StatusCodes.Status404NotFound);
        return endpoints;
    }

    /// <summary>Lista los trabajos activos.</summary>
    private static async Task<IResult> ListPublic(FeaturedProjectService service, CancellationToken ct)
        => Results.Ok(await service.ListPublicAsync(ct));

    /// <summary>Lista todos los trabajos para administración.</summary>
    private static async Task<IResult> List(FeaturedProjectService service, CancellationToken ct)
        => Results.Ok(await service.ListAsync(ct));

    /// <summary>Obtiene un trabajo por identificador.</summary>
    private static async Task<IResult> Get(int id, FeaturedProjectService service, CancellationToken ct)
        => await service.GetAsync(id, ct) is { } value ? Results.Ok(value) : Results.NotFound();

    /// <summary>Crea un trabajo y audita el alta.</summary>
    private static async Task<IResult> Create(CreateFeaturedProjectRequest request, FeaturedProjectService service,
        IAuditWriter audit, ICurrentUser user, CancellationToken ct)
    {
        var operation = await service.CreateAsync(request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await Audit(audit, user, AuditAction.Create, operation.Value!.Id, "Alta de un trabajo destacado.", ct);
        return CmsEndpointSupport.Result(operation, "trabajo",
            value => Results.Created($"/api/admin/cms/featured-projects/{value.Id}", value));
    }

    /// <summary>Modifica un trabajo y audita la edición.</summary>
    private static async Task<IResult> Update(int id, UpdateFeaturedProjectRequest request,
        FeaturedProjectService service, IAuditWriter audit, ICurrentUser user, CancellationToken ct)
    {
        var operation = await service.UpdateAsync(id, request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await Audit(audit, user, AuditAction.Update, id, "Modificación de un trabajo destacado.", ct);
        return CmsEndpointSupport.Result(operation, "trabajo", Results.Ok);
    }

    /// <summary>Desactiva un trabajo; requiere rol administrador.</summary>
    private static async Task<IResult> Deactivate(int id, FeaturedProjectService service,
        IAuditWriter audit, ICurrentUser user, CancellationToken ct)
    {
        var operation = await service.DeactivateAsync(id, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await Audit(audit, user, AuditAction.Delete, id, "Baja de un trabajo destacado.", ct);
        return CmsEndpointSupport.Result(operation, "trabajo", Results.Ok);
    }

    /// <summary>Reordena todos los trabajos en una transacción.</summary>
    private static async Task<IResult> Reorder(ReorderCmsRequest request, FeaturedProjectService service,
        IAuditWriter audit, ICurrentUser user, CancellationToken ct)
    {
        var operation = await service.ReorderAsync(request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await CmsEndpointSupport.AuditAsync(audit, user, AuditAction.Update, "featured_project", null,
                "Reordenamiento completo de los trabajos destacados.", ct);
        return CmsEndpointSupport.Result(operation, "orden", Results.Ok);
    }

    private static Task Audit(IAuditWriter audit, ICurrentUser user, string action, int id,
        string summary, CancellationToken ct)
        => CmsEndpointSupport.AuditAsync(audit, user, action, "featured_project", id.ToString(), summary, ct);
}
