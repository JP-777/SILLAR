using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Cms.Dtos;
using Sillar.Modules.Cms.Services;

namespace Sillar.Modules.Cms.Endpoints;

/// <summary>Redes sociales públicas y administración editorial.</summary>
public static class SocialLinkEndpoints
{
    /// <summary>Monta todas las rutas de redes sociales.</summary>
    public static IEndpointRouteBuilder MapSocialLinkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/cms/social-links", ListPublic)
            .WithTags("Contenido — Redes sociales").WithName("ListPublicCmsSocialLinks")
            .WithSummary("Enlaces sociales activos, en el orden del pie.")
            .Produces<IReadOnlyList<SocialLinkResponse>>();
        var admin = endpoints.MapGroup("/api/admin/cms/social-links")
            .WithTags("Contenido — Redes sociales")
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();
        admin.MapGet("", List).WithName("ListAdminCmsSocialLinks")
            .WithSummary("Lista todos los enlaces sociales, incluidos los inactivos.")
            .Produces<IReadOnlyList<SocialLinkAdminResponse>>();
        admin.MapGet("/{id:int}", Get).WithName("GetAdminCmsSocialLink")
            .WithSummary("Obtiene un enlace social para editar.")
            .Produces<SocialLinkAdminResponse>().Produces(StatusCodes.Status404NotFound);
        admin.MapPost("", Create).WithName("CreateCmsSocialLink")
            .WithSummary("Añade una red social al final del pie.")
            .Produces<SocialLinkAdminResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem().ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPut("/{id:int}", Update).WithName("UpdateCmsSocialLink")
            .WithSummary("Modifica una red social sin cambiar su orden ni su estado activo.")
            .Produces<SocialLinkAdminResponse>().ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPut("/order", Reorder).WithName("ReorderCmsSocialLinks")
            .WithSummary("Sustituye atómicamente el orden completo de las redes sociales.")
            .Produces<IReadOnlyList<int>>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapDelete("/{id:int}", Deactivate).WithName("DeactivateCmsSocialLink")
            .WithSummary("Desactiva una red social sin borrarla.")
            .RequireAuthorization(AdminRole.Admin)
            .Produces<SocialLinkAdminResponse>().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/{id:int}/reactivate", Reactivate).WithName("ReactivateCmsSocialLink")
            .WithSummary("Reactiva una red social conservando su identidad, contenido y orden.")
            .RequireAuthorization(AdminRole.Admin)
            .Produces<SocialLinkAdminResponse>().Produces(StatusCodes.Status404NotFound);
        return endpoints;
    }

    /// <summary>Lista las redes sociales activas.</summary>
    private static async Task<IResult> ListPublic(SocialLinkService service, CancellationToken ct)
        => Results.Ok(await service.ListPublicAsync(ct));

    /// <summary>Lista todas las redes para administración.</summary>
    private static async Task<IResult> List(SocialLinkService service, CancellationToken ct)
        => Results.Ok(await service.ListAsync(ct));

    /// <summary>Obtiene una red por identificador.</summary>
    private static async Task<IResult> Get(int id, SocialLinkService service, CancellationToken ct)
        => await service.GetAsync(id, ct) is { } value ? Results.Ok(value) : Results.NotFound();

    /// <summary>Crea una red social y audita el alta.</summary>
    private static async Task<IResult> Create(CreateSocialLinkRequest request, SocialLinkService service,
        IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var operation = await service.CreateAsync(request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await Audit(audit, user, AuditAction.Create, operation.Value!.Id, "Alta de un enlace social.", ct);
        return CmsEndpointSupport.Result(operation, "red social",
            value => Results.Created($"/api/admin/cms/social-links/{value.Id}", value));
    }

    /// <summary>Modifica una red social y audita la edición.</summary>
    private static async Task<IResult> Update(int id, UpdateSocialLinkRequest request, SocialLinkService service,
        IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var operation = await service.UpdateAsync(id, request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await Audit(audit, user, AuditAction.Update, id, "Modificación de un enlace social.", ct);
        return CmsEndpointSupport.Result(operation, "red social", Results.Ok);
    }

    /// <summary>Desactiva una red social; requiere rol administrador.</summary>
    private static async Task<IResult> Deactivate(int id, SocialLinkService service,
        IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var operation = await service.DeactivateAsync(id, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await Audit(audit, user, AuditAction.Delete, id, "Baja de un enlace social.", ct);
        return CmsEndpointSupport.Result(operation, "red social", Results.Ok);
    }

    /// <summary>Reactiva una red social; requiere rol administrador.</summary>
    internal static async Task<IResult> Reactivate(int id, SocialLinkService service,
        IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var operation = await service.ReactivateAsync(id, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await Audit(audit, user, AuditAction.Activate, id, "Reactivación de un enlace social.", ct);
        return CmsEndpointSupport.Result(operation, "red social", Results.Ok);
    }

    /// <summary>Reordena todas las redes sociales en una transacción.</summary>
    private static async Task<IResult> Reorder(ReorderCmsRequest request, SocialLinkService service,
        IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var operation = await service.ReorderAsync(request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await CmsEndpointSupport.AuditAsync(audit, user, AuditAction.Update, "social_link", null,
                "Reordenamiento completo de los enlaces sociales.", ct);
        return CmsEndpointSupport.Result(operation, "orden", Results.Ok);
    }

    private static Task Audit(IAuditWriter audit, ICurrentAdmin user, string action, int id,
        string summary, CancellationToken ct)
        => CmsEndpointSupport.AuditAsync(audit, user, action, "social_link", id.ToString(), summary, ct);
}
