using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Dtos;
using Sillar.Modules.Catalog.Services;
using Sillar.Shared.Paging;

namespace Sillar.Modules.Catalog.Endpoints;

/// <summary>Categorías: árbol público y administración.</summary>
public static class CategoryEndpoints
{
    /// <summary>Monta las rutas de categorías.</summary>
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var pub = endpoints.MapGroup("/api/catalog/categories").WithTags("Catálogo — Categorías");

        pub.MapGet("", GetTree)
            .WithName("GetCategoryTree")
            .WithSummary("Árbol de categorías activas.")
            .Produces<IReadOnlyList<CategoryTreeNodeResponse>>(StatusCodes.Status200OK);

        pub.MapGet("/{slug}", GetDetail)
            .WithName("GetCategoryDetail")
            .WithSummary("Una categoría con sus productos públicos, paginados.")
            .WithDescription("404 si no existe o está desactivada: para la web pública, las dos cosas son lo mismo.")
            .Produces<CategoryDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var admin = endpoints.MapGroup("/api/admin/catalog/categories")
            .WithTags("Catálogo — Categorías")
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("", List)
            .WithName("ListAdminCategories")
            .WithSummary("Lista todas las categorías, activas e inactivas.")
            .Produces<IReadOnlyList<CategoryAdminResponse>>(StatusCodes.Status200OK);

        admin.MapPost("", Create)
            .WithName("CreateCategory")
            .WithSummary("Da de alta una categoría.")
            .Produces<CategoryAdminResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPut("/{id:guid}", Update)
            .WithName("UpdateCategory")
            .WithSummary("Modifica una categoría.")
            .WithDescription("El slug se envía tal cual: no se recalcula del nombre (regla 3).")
            .Produces<CategoryAdminResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapDelete("/{id:guid}", Deactivate)
            .WithName("DeactivateCategory")
            .WithSummary("Desactiva una categoría.")
            .WithDescription(
                "Baja lógica, sin cascada: los productos que la tenían siguen activos. La respuesta dice " +
                "a cuántos afecta, para que la persona decida si les asigna otra categoría.")
            .Produces<DeactivateCategoryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Árbol de categorías activas.</summary>
    /// <param name="categories">Servicio de categorías.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>Las categorías de primer nivel, con sus hijas anidadas.</returns>
    private static async Task<IResult> GetTree(CategoryService categories, CancellationToken cancellationToken)
        => Results.Ok(await categories.GetPublicTreeAsync(cancellationToken));

    /// <summary>Una categoría con sus productos.</summary>
    /// <param name="slug">Slug de la categoría.</param>
    /// <param name="page">Número de página, empezando en 1.</param>
    /// <param name="pageSize">Elementos por página. Se recorta al máximo si se pide más.</param>
    /// <param name="categories">Servicio de categorías.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la categoría y sus productos, 404 si no existe o está desactivada.</returns>
    private static async Task<IResult> GetDetail(
        string slug,
        int? page,
        int? pageSize,
        CategoryService categories,
        CancellationToken cancellationToken)
    {
        var (found, category, breadcrumb, products) = await categories.GetPublicDetailAsync(
            slug, PageRequest.Of(page, pageSize), cancellationToken);

        if (!found)
        {
            return Results.NotFound();
        }

        return Results.Ok(new CategoryDetailResponse(
            category.Slug, category.Name, breadcrumb, category.ImageUrl, products));
    }

    /// <summary>Lista todas las categorías.</summary>
    /// <param name="categories">Servicio de categorías.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>Activas e inactivas, ordenadas por presentación.</returns>
    private static async Task<IResult> List(CategoryService categories, CancellationToken cancellationToken)
        => Results.Ok(await categories.ListAsync(cancellationToken));

    /// <summary>Da de alta una categoría.</summary>
    /// <param name="request">Datos de la categoría.</param>
    /// <param name="categories">Servicio de categorías.</param>
    /// <param name="currentUser">Quién la crea.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>201 con la categoría, 400 si los datos no sirven, 409 si el slug ya existe.</returns>
    private static async Task<IResult> Create(
        CreateCategoryRequest request,
        CategoryService categories,
        ICurrentAdmin currentUser,
        CancellationToken cancellationToken)
    {
        var result = await categories.CreateAsync(request, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome switch
        {
            CategoryOutcome.Ok => Results.Created($"/api/admin/catalog/categories/{result.Category!.Id}", result.Category),
            CategoryOutcome.Conflict => Problem(result.Error!, StatusCodes.Status409Conflict),
            _ => Invalid(result.Error!)
        };
    }

    /// <summary>Modifica una categoría.</summary>
    /// <param name="id">Identificador de la categoría.</param>
    /// <param name="request">Datos nuevos.</param>
    /// <param name="categories">Servicio de categorías.</param>
    /// <param name="currentUser">Quién la modifica.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la categoría, 404, 400 o 409 según el caso.</returns>
    private static async Task<IResult> Update(
        Guid id,
        UpdateCategoryRequest request,
        CategoryService categories,
        ICurrentAdmin currentUser,
        CancellationToken cancellationToken)
    {
        var result = await categories.UpdateAsync(id, request, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome switch
        {
            CategoryOutcome.Ok => Results.Ok(result.Category),
            CategoryOutcome.NotFound => Results.NotFound(),
            CategoryOutcome.Conflict => Problem(result.Error!, StatusCodes.Status409Conflict),
            _ => Invalid(result.Error!)
        };
    }

    /// <summary>Desactiva una categoría.</summary>
    /// <param name="id">Identificador de la categoría.</param>
    /// <param name="categories">Servicio de categorías.</param>
    /// <param name="currentUser">Quién la desactiva.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con el resultado y el aviso, 404 si no existe.</returns>
    private static async Task<IResult> Deactivate(
        Guid id,
        CategoryService categories,
        ICurrentAdmin currentUser,
        CancellationToken cancellationToken)
    {
        var (outcome, result) = await categories.DeactivateAsync(id, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return outcome == CategoryOutcome.Ok ? Results.Ok(result) : Results.NotFound();
    }

    private static IResult Invalid(string error) => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["categoria"] = [error] },
        title: "Los datos de la categoría no son válidos.");

    private static IResult Problem(string error, int statusCode) => Results.Problem(title: error, statusCode: statusCode);
}
