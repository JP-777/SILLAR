using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Cms.Dtos;
using Sillar.Modules.Cms.Services;

namespace Sillar.Modules.Cms.Endpoints;

/// <summary>Promociones públicas y administración editorial.</summary>
public static class PromotionEndpoints
{
    /// <summary>Monta todas las rutas de promociones.</summary>
    public static IEndpointRouteBuilder MapPromotionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/cms/promotions", ListPublic)
            .WithTags("Contenido — Promociones").WithName("ListPublicCmsPromotions")
            .WithSummary("Promociones vigentes, en el orden de portada.")
            .Produces<IReadOnlyList<PromotionResponse>>();

        var admin = endpoints.MapGroup("/api/admin/cms/promotions")
            .WithTags("Contenido — Promociones")
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("", List).WithName("ListAdminCmsPromotions")
            .WithSummary("Lista todas las promociones, incluidas programadas, caducadas e inactivas.")
            .Produces<IReadOnlyList<PromotionAdminResponse>>();
        admin.MapGet("/{id:int}", Get).WithName("GetAdminCmsPromotion")
            .WithSummary("Obtiene una promoción para editar.")
            .Produces<PromotionAdminResponse>().Produces(StatusCodes.Status404NotFound);
        admin.MapPost("", Create).WithName("CreateCmsPromotion")
            .WithSummary("Crea una promoción al final de la sección.")
            .Produces<PromotionAdminResponse>(StatusCodes.Status201Created).ProducesValidationProblem();
        admin.MapPut("/{id:int}", Update).WithName("UpdateCmsPromotion")
            .WithSummary("Modifica una promoción sin cambiar su orden ni su estado activo.")
            .Produces<PromotionAdminResponse>().ProducesValidationProblem().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/order", Reorder).WithName("ReorderCmsPromotions")
            .WithSummary("Sustituye atómicamente el orden completo de las promociones.")
            .Produces<IReadOnlyList<int>>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapDelete("/{id:int}", Deactivate).WithName("DeactivateCmsPromotion")
            .WithSummary("Desactiva una promoción sin borrarla.")
            .RequireAuthorization(AdminRole.Admin)
            .Produces<PromotionAdminResponse>().Produces(StatusCodes.Status404NotFound);
        return endpoints;
    }

    /// <summary>Lista las promociones vigentes.</summary>
    private static async Task<IResult> ListPublic(PromotionService service, CancellationToken ct)
        => Results.Ok(await service.ListPublicAsync(ct));

    /// <summary>Lista todas las promociones para administración.</summary>
    private static async Task<IResult> List(PromotionService service, CancellationToken ct)
        => Results.Ok(await service.ListAsync(ct));

    /// <summary>Obtiene una promoción por identificador.</summary>
    private static async Task<IResult> Get(int id, PromotionService service, CancellationToken ct)
        => await service.GetAsync(id, ct) is { } value ? Results.Ok(value) : Results.NotFound();

    /// <summary>Crea una promoción y audita el alta.</summary>
    private static async Task<IResult> Create(CreatePromotionRequest request, PromotionService service,
        IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var operation = await service.CreateAsync(request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await Audit(audit, user, AuditAction.Create, operation.Value!.Id, "Alta de una promoción.", ct);
        return CmsEndpointSupport.Result(operation, "promoción",
            value => Results.Created($"/api/admin/cms/promotions/{value.Id}", value));
    }

    /// <summary>Modifica una promoción y audita la edición.</summary>
    private static async Task<IResult> Update(int id, UpdatePromotionRequest request, PromotionService service,
        IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var operation = await service.UpdateAsync(id, request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await Audit(audit, user, AuditAction.Update, id, "Modificación de una promoción.", ct);
        return CmsEndpointSupport.Result(operation, "promoción", Results.Ok);
    }

    /// <summary>Desactiva una promoción; requiere rol administrador.</summary>
    private static async Task<IResult> Deactivate(int id, PromotionService service,
        IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var operation = await service.DeactivateAsync(id, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await Audit(audit, user, AuditAction.Delete, id, "Baja de una promoción.", ct);
        return CmsEndpointSupport.Result(operation, "promoción", Results.Ok);
    }

    /// <summary>Reordena todas las promociones en una transacción.</summary>
    private static async Task<IResult> Reorder(ReorderCmsRequest request, PromotionService service,
        IAuditWriter audit, ICurrentAdmin user, CancellationToken ct)
    {
        var operation = await service.ReorderAsync(request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await CmsEndpointSupport.AuditAsync(audit, user, AuditAction.Update, "promotion", null,
                "Reordenamiento completo de las promociones.", ct);
        return CmsEndpointSupport.Result(operation, "orden", Results.Ok);
    }

    private static Task Audit(IAuditWriter audit, ICurrentAdmin user, string action, int id,
        string summary, CancellationToken ct)
        => CmsEndpointSupport.AuditAsync(audit, user, action, "promotion", id.ToString(), summary, ct);
}
