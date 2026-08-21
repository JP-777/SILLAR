using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Modules.Cms.Dtos;
using Sillar.Modules.Cms.Services;

namespace Sillar.Modules.Cms.Endpoints;

/// <summary>Productos destacados publicados desde snapshots editoriales.</summary>
public static class FeaturedProductEndpoints
{
    /// <summary>Monta las rutas públicas y administrativas de productos destacados.</summary>
    public static IEndpointRouteBuilder MapFeaturedProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/cms/featured-products", ListPublic)
            .WithTags("Contenido — Productos destacados").WithName("ListPublicCmsFeaturedProducts")
            .WithSummary("Productos destacados vigentes y públicos; sin Catálogo activo devuelve una lista vacía.")
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
            .WithSummary("Modifica solo la vigencia; este endpoint no altera el snapshot.")
            .Produces<FeaturedProductAdminResponse>().ProducesValidationProblem().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/order", Reorder).WithName("ReorderCmsFeaturedProducts")
            .WithSummary("Sustituye atómicamente el orden completo de los productos destacados.")
            .Produces<IReadOnlyList<int>>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapDelete("/{id:int}", Deactivate).WithName("DeactivateCmsFeaturedProduct")
            .WithSummary("Quita un producto destacado de la portada sin borrar su snapshot.")
            .RequireAuthorization(AdminRole.Admin)
            .Produces<FeaturedProductAdminResponse>().Produces(StatusCodes.Status404NotFound);

        var services = endpoints.ServiceProvider.GetRequiredService<IServiceProviderIsService>();
        if (services.IsService(typeof(ICatalogService)))
        {
            admin.MapGet("/catalog", SearchCatalog).WithName("SearchCatalogForCmsFeaturedProduct")
                .WithSummary("Busca productos activos de Catálogo para elegir un destacado.")
                .Produces<IReadOnlyList<FeaturedProductPickerResponse>>();
            admin.MapPost("", Create).WithName("CreateCmsFeaturedProduct")
                .WithSummary("Destaca un producto activo y copia su snapshot completo.")
                .Produces<FeaturedProductAdminResponse>(StatusCodes.Status201Created)
                .ProducesValidationProblem();
            admin.MapPut("/{id:int}/relink", Relink).WithName("RelinkCmsFeaturedProduct")
                .WithSummary("Vuelve a enlazar un destacado pendiente y sustituye todo su snapshot.")
                .Produces<FeaturedProductAdminResponse>().ProducesValidationProblem()
                .Produces(StatusCodes.Status404NotFound);
            admin.MapPut("/{id:int}/refresh", Refresh).WithName("RefreshCmsFeaturedProduct")
                .WithSummary("Relee en Catálogo y actualiza todos los datos de un destacado.")
                .Produces<FeaturedProductAdminResponse>().ProducesValidationProblem()
                .Produces(StatusCodes.Status404NotFound);
            admin.MapPut("/refresh", RefreshAll).WithName("RefreshAllCmsFeaturedProducts")
                .WithSummary("Relee en Catálogo y actualiza todos los snapshots enlazados.")
                .Produces<FeaturedProductRefreshResponse>();
        }
        return endpoints;
    }

    /// <summary>Lista snapshots vigentes y públicos solo cuando el contrato de Catálogo está disponible.</summary>
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

    /// <summary>Busca productos activos de M01 y resuelve la URL de su imagen para el selector.</summary>
    private static async Task<IResult> SearchCatalog(
        string? q,
        int? limit,
        ICatalogService catalog,
        IMediaStorage media,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.Ok(Array.Empty<FeaturedProductPickerResponse>());
        }

        var products = await catalog.BuscarParaSeleccionAsync(q, limit ?? 20, ct);
        return Results.Ok(products.Select(product => new FeaturedProductPickerResponse(
            product.ProductId,
            product.Name,
            product.Slug,
            product.PrimaryImageId is { } imageId ? media.GetPublicUrl(imageId) : null,
            product.PrimaryCategoryName,
            product.Price,
            product.PriceVaries,
            product.IsPublic,
            product.IsActive)).ToArray());
    }

    /// <summary>Destaca un producto activo, copia su snapshot y audita el alta.</summary>
    private static async Task<IResult> Create(
        CreateFeaturedProductRequest request,
        ICatalogService catalog,
        FeaturedProductService service,
        IAuditWriter audit,
        ICurrentUser user,
        CancellationToken ct)
    {
        if (request.ProductId is not { } productId)
        {
            return InvalidProduct("Elige un producto del catálogo.");
        }

        var product = await catalog.ObtenerParaSeleccionAsync(productId, ct);
        if (product is null)
        {
            return InvalidProduct("El producto elegido ya no existe. Busca otro producto del catálogo.");
        }

        var operation = await service.CreateAsync(request, product, ct);
        if (operation.Outcome == CmsOutcome.Ok)
        {
            await Audit(audit, user, AuditAction.Create, operation.Value!.Id,
                "Alta de un producto destacado.", ct);
        }

        return CmsEndpointSupport.Result(operation, "productId",
            value => Results.Created($"/api/admin/cms/featured-products/{value.Id}", value));
    }

    /// <summary>Reenlaza un destacado con un producto activo y sustituye todo el snapshot.</summary>
    private static async Task<IResult> Relink(
        int id,
        RelinkFeaturedProductRequest request,
        ICatalogService catalog,
        FeaturedProductService service,
        IAuditWriter audit,
        ICurrentUser user,
        CancellationToken ct)
    {
        if (request.ProductId is not { } productId)
        {
            return InvalidProduct("Elige un producto del catálogo.");
        }

        var product = await catalog.ObtenerParaSeleccionAsync(productId, ct);
        if (product is null)
        {
            return InvalidProduct("El producto elegido ya no existe. Busca otro producto del catálogo.");
        }

        var operation = await service.RelinkAsync(id, request, product, ct);
        if (operation.Outcome == CmsOutcome.Ok)
        {
            await Audit(audit, user, AuditAction.Update, id,
                "Reenlace y sustitución del snapshot de un producto destacado.", ct);
        }

        return CmsEndpointSupport.Result(operation, "productId", Results.Ok);
    }

    /// <summary>Relee un producto enlazado y devuelve el destacado ya actualizado.</summary>
    private static async Task<IResult> Refresh(
        int id,
        FeaturedProductService service,
        FeaturedProductSnapshotCoordinator snapshots,
        IAuditWriter audit,
        ICurrentUser user,
        CancellationToken ct)
    {
        var before = await service.GetAsync(id, ct);
        if (before is null)
        {
            return Results.NotFound();
        }

        if (before.ProductId is not { } productId)
        {
            return InvalidProduct("El destacado está pendiente de volver a enlazar; elige primero un producto.");
        }

        if (await snapshots.RefreshProductAsync(productId, ct) is null)
        {
            return Results.Problem("Catálogo no está disponible para actualizar el snapshot.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var updated = await service.GetAsync(id, ct);
        await Audit(audit, user, AuditAction.Update, id,
            "Actualización manual del snapshot de un producto destacado.", ct);
        return Results.Ok(updated);
    }

    /// <summary>Relee todos los productos enlazados y audita una sola reconciliación.</summary>
    private static async Task<IResult> RefreshAll(
        FeaturedProductSnapshotCoordinator snapshots,
        IAuditWriter audit,
        ICurrentUser user,
        CancellationToken ct)
    {
        var result = await snapshots.RefreshAllAsync(ct);
        if (result is null)
        {
            return Results.Problem("Catálogo no está disponible para actualizar los snapshots.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await CmsEndpointSupport.AuditAsync(audit, user, AuditAction.Update, "featured_product", null,
            "Reconciliación manual de todos los productos destacados.", ct);
        return Results.Ok(result);
    }

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

    private static IResult InvalidProduct(string message)
        => Results.ValidationProblem(
            new Dictionary<string, string[]> { ["productId"] = [message] },
            title: "El producto destacado no es válido.");

    private static Task Audit(IAuditWriter audit, ICurrentUser user, string action, int id,
        string summary, CancellationToken ct)
        => CmsEndpointSupport.AuditAsync(audit, user, action, "featured_product", id.ToString(), summary, ct);
}
