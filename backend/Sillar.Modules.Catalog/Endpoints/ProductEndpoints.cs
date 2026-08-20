using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Dtos;
using Sillar.Modules.Catalog.Services;
using Sillar.Shared.Paging;

namespace Sillar.Modules.Catalog.Endpoints;

/// <summary>Productos: listado y ficha pública, administración, categorías e imágenes.</summary>
public static class ProductEndpoints
{
    /// <summary>Monta las rutas de productos.</summary>
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var pub = endpoints.MapGroup("/api/catalog/products").WithTags("Catálogo — Productos");

        pub.MapGet("", ListPublic)
            .WithName("ListPublicProducts")
            .WithSummary("Listado público, con filtros.")
            .WithDescription(
                "Solo activos y públicos. 'q' busca por texto completo en español (name y " +
                "short_description); 'category' y 'brand' filtran por slug.")
            .Produces<PagedResult<ProductCardResponse>>(StatusCodes.Status200OK);

        pub.MapGet("/{slug}", GetPublicDetail)
            .WithName("GetPublicProductDetail")
            .WithSummary("Ficha completa de un producto: imágenes y variantes disponibles.")
            .WithDescription("404 si no existe, está desactivado o despublicado: no se distingue, para no filtrar qué existe.")
            .Produces<ProductDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var admin = endpoints.MapGroup("/api/admin/catalog/products")
            .WithTags("Catálogo — Productos")
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("", List)
            .WithName("ListAdminProducts")
            .WithSummary("Lista productos con filtros, para la administración.")
            .Produces<PagedResult<ProductAdminListItemResponse>>(StatusCodes.Status200OK);

        admin.MapPost("", Create)
            .WithName("CreateProduct")
            .WithSummary("Da de alta un producto, con su variante única.")
            .WithDescription(
                "Quien llama nunca menciona una variante: si el producto necesita código o código de " +
                "barras, van en este mismo cuerpo y el servicio los coloca en la variante que crea solo.")
            .Produces<ProductAdminResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapGet("/{id:guid}", GetById)
            .WithName("GetAdminProduct")
            .WithSummary("Ficha completa de un producto, para editar.")
            .Produces<ProductAdminResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPut("/{id:guid}", Update)
            .WithName("UpdateProduct")
            .WithSummary("Modifica los datos del producto.")
            .WithDescription(
                "El slug se envía tal cual (regla 3). Categorías, imágenes y variantes tienen sus propios " +
                "endpoints.")
            .Produces<ProductAdminResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapDelete("/{id:guid}", Deactivate)
            .WithName("DeactivateProduct")
            .WithSummary("Desactiva un producto.")
            .WithDescription("Baja lógica: sigue existiendo en pedidos y ventas anteriores (regla 7).")
            .Produces<ProductAdminResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPut("/{id:guid}/categories", SetCategories)
            .WithName("SetProductCategories")
            .WithSummary("Fija el conjunto de categorías del producto y cuál es la principal.")
            .WithDescription("La principal tiene que estar entre las indicadas (regla 6).")
            .Produces<ProductAdminResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{id:guid}/images", AssociateImage)
            .WithName("AssociateProductImage")
            .WithSummary("Asocia una imagen de la galería de CORE al producto.")
            .Produces<ProductImageAdminResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapDelete("/{id:guid}/images/{imageId:guid}", RemoveImage)
            .WithName("RemoveProductImage")
            .WithSummary("Quita una imagen de la galería del producto.")
            .WithDescription("No borra el archivo de core.media_assets, solo la asociación.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPut("/{id:guid}/images/order", ReorderImages)
            .WithName("ReorderProductImages")
            .WithSummary("Reordena la galería y decide cuál es la principal.")
            .WithDescription("Máximo una imagen principal (regla 11); si ninguna lo está, se usa la de menor orden.")
            .Produces<IReadOnlyList<ProductImageAdminResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Listado público de productos.</summary>
    /// <param name="category">Filtra por slug de categoría.</param>
    /// <param name="brand">Filtra por slug de marca.</param>
    /// <param name="q">Texto libre, sin distinguir mayúsculas ni tildes.</param>
    /// <param name="page">Número de página, empezando en 1.</param>
    /// <param name="pageSize">Elementos por página. Se recorta al máximo si se pide más.</param>
    /// <param name="products">Servicio de productos.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>Página de tarjetas de producto.</returns>
    private static async Task<IResult> ListPublic(
        string? category,
        string? brand,
        string? q,
        int? page,
        int? pageSize,
        ProductService products,
        CancellationToken cancellationToken)
        => Results.Ok(await products.GetPublicListAsync(category, brand, q, PageRequest.Of(page, pageSize), cancellationToken));

    /// <summary>Ficha pública de un producto.</summary>
    /// <param name="slug">Slug del producto.</param>
    /// <param name="products">Servicio de productos.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la ficha, 404 si no existe, está desactivado o despublicado.</returns>
    private static async Task<IResult> GetPublicDetail(string slug, ProductService products, CancellationToken cancellationToken)
    {
        var detail = await products.GetPublicDetailAsync(slug, cancellationToken);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }

    /// <summary>Lista productos para la administración.</summary>
    /// <param name="q">Texto libre.</param>
    /// <param name="isActive">Filtra por baja lógica.</param>
    /// <param name="page">Número de página, empezando en 1.</param>
    /// <param name="pageSize">Elementos por página. Se recorta al máximo si se pide más.</param>
    /// <param name="products">Servicio de productos.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>Página de productos.</returns>
    private static async Task<IResult> List(
        string? q,
        bool? isActive,
        int? page,
        int? pageSize,
        ProductService products,
        CancellationToken cancellationToken)
        => Results.Ok(await products.ListAsync(q, isActive, PageRequest.Of(page, pageSize), cancellationToken));

    /// <summary>Da de alta un producto.</summary>
    /// <param name="request">Datos del producto y de su variante única.</param>
    /// <param name="products">Servicio de productos.</param>
    /// <param name="currentUser">Quién lo crea.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>201 con el producto, 400 si los datos no sirven, 409 si el slug o el código ya existen.</returns>
    private static async Task<IResult> Create(
        CreateProductRequest request,
        ProductService products,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await products.CreateAsync(request, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome switch
        {
            ProductOutcome.Ok => Results.Created($"/api/admin/catalog/products/{result.Product!.Id}", result.Product),
            ProductOutcome.Conflict => Problem(result.Error!, StatusCodes.Status409Conflict),
            _ => Invalid(result.Error!)
        };
    }

    /// <summary>Ficha de un producto para la administración.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="products">Servicio de productos.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la ficha, 404 si no existe.</returns>
    private static async Task<IResult> GetById(Guid id, ProductService products, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(id, cancellationToken);
        return product is null ? Results.NotFound() : Results.Ok(product);
    }

    /// <summary>Modifica un producto.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="request">Datos nuevos.</param>
    /// <param name="products">Servicio de productos.</param>
    /// <param name="currentUser">Quién lo modifica.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con el producto, 404, 400 o 409 según el caso.</returns>
    private static async Task<IResult> Update(
        Guid id,
        UpdateProductRequest request,
        ProductService products,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await products.UpdateAsync(id, request, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome switch
        {
            ProductOutcome.Ok => Results.Ok(result.Product),
            ProductOutcome.NotFound => Results.NotFound(),
            ProductOutcome.Conflict => Problem(result.Error!, StatusCodes.Status409Conflict),
            _ => Invalid(result.Error!)
        };
    }

    /// <summary>Desactiva un producto.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="products">Servicio de productos.</param>
    /// <param name="currentUser">Quién lo desactiva.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con el producto desactivado, 404 si no existe.</returns>
    private static async Task<IResult> Deactivate(
        Guid id,
        ProductService products,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await products.DeactivateAsync(id, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);
        return result.Outcome == ProductOutcome.Ok ? Results.Ok(result.Product) : Results.NotFound();
    }

    /// <summary>Fija las categorías de un producto.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="request">Categorías y cuál es la principal.</param>
    /// <param name="products">Servicio de productos.</param>
    /// <param name="currentUser">Quién lo modifica.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con el producto, 404 o 400 según el caso.</returns>
    private static async Task<IResult> SetCategories(
        Guid id,
        SetProductCategoriesRequest request,
        ProductService products,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await products.SetCategoriesAsync(id, request, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome switch
        {
            ProductOutcome.Ok => Results.Ok(result.Product),
            ProductOutcome.NotFound => Results.NotFound(),
            _ => Invalid(result.Error!)
        };
    }

    /// <summary>Asocia una imagen al producto.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="request">Archivo, texto alternativo y si es la principal.</param>
    /// <param name="images">Servicio de la galería del producto.</param>
    /// <param name="currentUser">Quién la asocia.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>201 con la imagen, 404 si el producto no existe, 400 o 409 según el caso.</returns>
    private static async Task<IResult> AssociateImage(
        Guid id,
        AssociateProductImageRequest request,
        ProductImageService images,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await images.AssociateAsync(id, request, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome switch
        {
            ProductImageOutcome.Ok => Results.Created($"/api/admin/catalog/products/{id}/images/{result.Image!.Id}", result.Image),
            ProductImageOutcome.NotFound => Results.NotFound(),
            ProductImageOutcome.Conflict => Problem(result.Error!, StatusCodes.Status409Conflict),
            _ => Invalid(result.Error!)
        };
    }

    /// <summary>Quita una imagen de la galería.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="imageId">Identificador de la asociación.</param>
    /// <param name="images">Servicio de la galería del producto.</param>
    /// <param name="currentUser">Quién la quita.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>204 si se quitó, 404 si no existía.</returns>
    private static async Task<IResult> RemoveImage(
        Guid id,
        Guid imageId,
        ProductImageService images,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
        => await images.RemoveAsync(id, imageId, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();

    /// <summary>Reordena la galería.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="request">Orden completo y, opcionalmente, la nueva principal.</param>
    /// <param name="images">Servicio de la galería del producto.</param>
    /// <param name="currentUser">Quién reordena.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la galería reordenada, 404 o 400 según el caso.</returns>
    private static async Task<IResult> ReorderImages(
        Guid id,
        ReorderProductImagesRequest request,
        ProductImageService images,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await images.ReorderAsync(id, request, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome switch
        {
            ProductImageOutcome.Ok => Results.Ok(result.Images),
            ProductImageOutcome.NotFound => Results.NotFound(),
            _ => Invalid(result.Error!)
        };
    }

    private static IResult Invalid(string error) => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["producto"] = [error] },
        title: "Los datos del producto no son válidos.");

    private static IResult Problem(string error, int statusCode) => Results.Problem(title: error, statusCode: statusCode);
}
