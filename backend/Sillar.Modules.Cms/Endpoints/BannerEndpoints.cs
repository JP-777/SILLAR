using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Cms.Dtos;
using Sillar.Modules.Cms.Services;

namespace Sillar.Modules.Cms.Endpoints;

/// <summary>Banners públicos y administración editorial.</summary>
public static class BannerEndpoints
{
    /// <summary>Monta todas las rutas de banners.</summary>
    public static IEndpointRouteBuilder MapBannerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/cms/banners", ListPublic)
            .WithTags("Contenido — Banners")
            .WithName("ListPublicCmsBanners")
            .WithSummary("Banners vigentes y completos, en el orden de portada.")
            .Produces<IReadOnlyList<BannerResponse>>();

        var admin = endpoints.MapGroup("/api/admin/cms/banners")
            .WithTags("Contenido — Banners")
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("", List).WithName("ListAdminCmsBanners")
            .WithSummary("Lista todos los banners, incluidos programados, caducados e inactivos.")
            .Produces<IReadOnlyList<BannerAdminResponse>>();
        admin.MapGet("/{id:int}", Get).WithName("GetAdminCmsBanner")
            .WithSummary("Obtiene un banner para editar.")
            .Produces<BannerAdminResponse>().Produces(StatusCodes.Status404NotFound);
        admin.MapPost("", Create).WithName("CreateCmsBanner")
            .WithSummary("Crea un banner al final de la portada.")
            .Produces<BannerAdminResponse>(StatusCodes.Status201Created).ProducesValidationProblem();
        admin.MapPut("/{id:int}", Update).WithName("UpdateCmsBanner")
            .WithSummary("Modifica un banner sin cambiar su orden ni su estado activo.")
            .Produces<BannerAdminResponse>().ProducesValidationProblem().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/order", Reorder).WithName("ReorderCmsBanners")
            .WithSummary("Sustituye atómicamente el orden completo de los banners.")
            .Produces<IReadOnlyList<int>>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapDelete("/{id:int}", Deactivate).WithName("DeactivateCmsBanner")
            .WithSummary("Desactiva un banner sin borrarlo.")
            .RequireAuthorization(AdminRole.Admin)
            .Produces<BannerAdminResponse>().Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Lista los banners que pueden publicarse ahora.</summary>
    private static async Task<IResult> ListPublic(BannerService service, CancellationToken ct)
        => Results.Ok(await service.ListPublicAsync(ct));

    /// <summary>Lista todos los banners para administración.</summary>
    private static async Task<IResult> List(BannerService service, CancellationToken ct)
        => Results.Ok(await service.ListAsync(ct));

    /// <summary>Obtiene un banner por su identificador interno.</summary>
    private static async Task<IResult> Get(int id, BannerService service, CancellationToken ct)
        => await service.GetAsync(id, ct) is { } banner ? Results.Ok(banner) : Results.NotFound();

    /// <summary>Crea un banner y registra quién lo hizo.</summary>
    private static async Task<IResult> Create(
        CreateBannerRequest request,
        BannerService service,
        IAuditWriter audit,
        ICurrentUser user,
        CancellationToken ct)
    {
        var operation = await service.CreateAsync(request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
        {
            await CmsEndpointSupport.AuditAsync(audit, user, AuditAction.Create, "banner",
                operation.Value!.Id.ToString(), "Alta de un banner de portada.", ct);
        }

        return CmsEndpointSupport.Result(operation, "banner",
            banner => Results.Created($"/api/admin/cms/banners/{banner.Id}", banner));
    }

    /// <summary>Modifica un banner y registra la edición.</summary>
    private static async Task<IResult> Update(
        int id,
        UpdateBannerRequest request,
        BannerService service,
        IAuditWriter audit,
        ICurrentUser user,
        CancellationToken ct)
    {
        var operation = await service.UpdateAsync(id, request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
        {
            await CmsEndpointSupport.AuditAsync(audit, user, AuditAction.Update, "banner",
                id.ToString(), "Modificación de un banner de portada.", ct);
        }

        return CmsEndpointSupport.Result(operation, "banner", Results.Ok);
    }

    /// <summary>Desactiva un banner; requiere rol administrador.</summary>
    private static async Task<IResult> Deactivate(
        int id,
        BannerService service,
        IAuditWriter audit,
        ICurrentUser user,
        CancellationToken ct)
    {
        var operation = await service.DeactivateAsync(id, ct);
        if (operation.Outcome == CmsOutcome.Ok)
        {
            await CmsEndpointSupport.AuditAsync(audit, user, AuditAction.Delete, "banner",
                id.ToString(), "Baja de un banner de portada.", ct);
        }

        return CmsEndpointSupport.Result(operation, "banner", Results.Ok);
    }

    /// <summary>Reordena la lista completa en una sola transacción.</summary>
    private static async Task<IResult> Reorder(
        ReorderCmsRequest request,
        BannerService service,
        IAuditWriter audit,
        ICurrentUser user,
        CancellationToken ct)
    {
        var operation = await service.ReorderAsync(request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
        {
            await CmsEndpointSupport.AuditAsync(audit, user, AuditAction.Update, "banner", null,
                "Reordenamiento completo de los banners de portada.", ct);
        }

        return CmsEndpointSupport.Result(operation, "orden", Results.Ok);
    }
}
