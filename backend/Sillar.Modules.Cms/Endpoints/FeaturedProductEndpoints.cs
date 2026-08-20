using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Modules.Cms.Dtos;
using Sillar.Modules.Cms.Services;

namespace Sillar.Modules.Cms.Endpoints;

/// <summary>Productos destacados publicados desde snapshots editoriales.</summary>
public static class FeaturedProductEndpoints
{
    /// <summary>Monta las rutas que no requieren el contrato de selección pendiente de M01.</summary>
    public static IEndpointRouteBuilder MapFeaturedProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/cms/featured-products", ListPublic)
            .WithTags("Contenido — Productos destacados").WithName("ListPublicCmsFeaturedProducts")
            .WithSummary("Productos destacados vigentes; sin Catálogo activo devuelve una lista vacía.")
            .Produces<IReadOnlyList<FeaturedProductResponse>>();

        var admin = endpoints.MapGroup("/api/admin/cms/featured-products")
            .WithTags("Contenido — Productos destacados")
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();
        admin.MapGet("", List).WithName("ListAdminCmsFeaturedProducts")
            .WithSummary("Lista todos los destacados y señala los pendientes de volver a enlazar.")
            .Produces<IReadOnlyList<FeaturedProductAdminResponse>>();
        admin.MapGet("/{id:int}", Get).WithName("GetAdminCmsFeaturedProduct")
            .WithSummary("Obtiene un producto destacado para editar.")
            .Produces<FeaturedProductAdminResponse>().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/{id:int}", Update).WithName("UpdateCmsFeaturedProduct")
            .WithSummary("Modifica solo la vigencia; nunca refresca el snapshot automáticamente.")
            .Produces<FeaturedProductAdminResponse>().ProducesValidationProblem().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/order", Reorder).WithName("ReorderCmsFeaturedProducts")
            .WithSummary("Sustituye atómicamente el orden completo de los productos destacados.")
            .Produces<IReadOnlyList<int>>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapDelete("/{id:int}", Deactivate).WithName("DeactivateCmsFeaturedProduct")
            .WithSummary("Quita un producto destacado de la portada sin borrar su snapshot.")
            .RequireAuthorization(AdminRole.Admin)
            .Produces<FeaturedProductAdminResponse>().Produces(StatusCodes.Status404NotFound);
        return endpoints;
    }

    /// <summary>Lista snapshots vigentes solo cuando el contrato de Catálogo está disponible.</summary>
    private static async Task<IResult> ListPublic(
        IServiceProvider services,
        FeaturedProductService service,
        CancellationToken ct)
    {
        if (services.GetService(typeof(ICatalogService)) is null)
        {
            return Results.Ok(Array.Empty<FeaturedProductResponse>());
        }

        return Results.Ok(await service.ListPublicAsync(ct));
    }

    /// <summary>Lista todos los snapshots, incluidos los huérfanos.</summary>
    private static async Task<IResult> List(FeaturedProductService service, CancellationToken ct)
        => Results.Ok(await service.ListAsync(ct));

    /// <summary>Obtiene un snapshot por identificador.</summary>
    private static async Task<IResult> Get(int id, FeaturedProductService service, CancellationToken ct)
        => await service.GetAsync(id, ct) is { } value ? Results.Ok(value) : Results.NotFound();

    /// <summary>Modifica la vigencia y audita la edición.</summary>
    private static async Task<IResult> Update(int id, UpdateFeaturedProductRequest request,
        FeaturedProductService service, IAuditWriter audit, ICurrentUser user, CancellationToken ct)
    {
        var operation = await service.UpdateAsync(id, request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await Audit(audit, user, AuditAction.Update, id, "Modificación de la vigencia de un producto destacado.", ct);
        return CmsEndpointSupport.Result(operation, "producto destacado", Results.Ok);
    }

    /// <summary>Desactiva un destacado; requiere rol administrador.</summary>
    private static async Task<IResult> Deactivate(int id, FeaturedProductService service,
        IAuditWriter audit, ICurrentUser user, CancellationToken ct)
    {
        var operation = await service.DeactivateAsync(id, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await Audit(audit, user, AuditAction.Delete, id, "Baja de un producto destacado.", ct);
        return CmsEndpointSupport.Result(operation, "producto destacado", Results.Ok);
    }

    /// <summary>Reordena todos los destacados en una transacción.</summary>
    private static async Task<IResult> Reorder(ReorderCmsRequest request, FeaturedProductService service,
        IAuditWriter audit, ICurrentUser user, CancellationToken ct)
    {
        var operation = await service.ReorderAsync(request, ct);
        if (operation.Outcome == CmsOutcome.Ok)
            await CmsEndpointSupport.AuditAsync(audit, user, AuditAction.Update, "featured_product", null,
                "Reordenamiento completo de los productos destacados.", ct);
        return CmsEndpointSupport.Result(operation, "orden", Results.Ok);
    }

    private static Task Audit(IAuditWriter audit, ICurrentUser user, string action, int id,
        string summary, CancellationToken ct)
        => CmsEndpointSupport.AuditAsync(audit, user, action, "featured_product", id.ToString(), summary, ct);
}
