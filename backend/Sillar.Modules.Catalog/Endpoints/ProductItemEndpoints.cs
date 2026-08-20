using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Dtos;
using Sillar.Modules.Catalog.Services;

namespace Sillar.Modules.Catalog.Endpoints;

/// <summary>Variantes: la segunda y siguientes de un producto, su administración y la resolución por código.</summary>
public static class ProductItemEndpoints
{
    /// <summary>Monta las rutas de variantes.</summary>
    public static IEndpointRouteBuilder MapProductItemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/catalog")
            .WithTags("Catálogo — Variantes")
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("/products/{id:guid}/items", ListByProduct)
            .WithName("ListProductItems")
            .WithSummary("Las variantes de un producto.")
            .Produces<IReadOnlyList<ProductItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/products/{id:guid}/items", Create)
            .WithName("CreateProductItem")
            .WithSummary("Crea la segunda variante de un producto, o siguiente.")
            .WithDescription("La primera nace sola con el producto (regla 2): este endpoint es solo para la segunda en adelante.")
            .Produces<ProductItemResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPut("/items/{itemId:guid}", Update)
            .WithName("UpdateProductItem")
            .WithSummary("Modifica una variante.")
            .Produces<ProductItemResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapDelete("/items/{itemId:guid}", Deactivate)
            .WithName("DeactivateProductItem")
            .WithSummary("Desactiva una variante.")
            .WithDescription(
                "No se puede desactivar la última variante activa de un producto activo (regla 8): el " +
                "409 propone desactivar el producto, no un error genérico.")
            .Produces<ProductItemResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapGet("/items/lookup", Lookup)
            .WithName("LookupProductItem")
            .WithSummary("Resolución exacta por código o código de barras, para la caja.")
            .Produces<ItemLookupResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Lista las variantes de un producto.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="items">Servicio de variantes.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la lista, 404 si el producto no existe.</returns>
    private static async Task<IResult> ListByProduct(Guid id, ProductItemService items, CancellationToken cancellationToken)
    {
        var list = await items.ListByProductAsync(id, cancellationToken);
        return list is null ? Results.NotFound() : Results.Ok(list);
    }

    /// <summary>Crea una variante.</summary>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="request">Datos de la variante.</param>
    /// <param name="items">Servicio de variantes.</param>
    /// <param name="currentUser">Quién la crea.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>201 con la variante, 404 si el producto no existe, 400 o 409 según el caso.</returns>
    private static async Task<IResult> Create(
        Guid id,
        CreateProductItemRequest request,
        ProductItemService items,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await items.CreateAsync(id, request, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome switch
        {
            ProductItemOutcome.Ok => Results.Created($"/api/admin/catalog/items/{result.Item!.Id}", result.Item),
            ProductItemOutcome.NotFound => Results.NotFound(),
            ProductItemOutcome.Conflict => Problem(result.Error!, StatusCodes.Status409Conflict),
            _ => Invalid(result.Error!)
        };
    }

    /// <summary>Modifica una variante.</summary>
    /// <param name="itemId">Identificador de la variante.</param>
    /// <param name="request">Datos nuevos.</param>
    /// <param name="items">Servicio de variantes.</param>
    /// <param name="currentUser">Quién la modifica.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la variante, 404, 400 o 409 según el caso.</returns>
    private static async Task<IResult> Update(
        Guid itemId,
        UpdateProductItemRequest request,
        ProductItemService items,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await items.UpdateAsync(itemId, request, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome switch
        {
            ProductItemOutcome.Ok => Results.Ok(result.Item),
            ProductItemOutcome.NotFound => Results.NotFound(),
            ProductItemOutcome.Conflict => Problem(result.Error!, StatusCodes.Status409Conflict),
            _ => Invalid(result.Error!)
        };
    }

    /// <summary>Desactiva una variante.</summary>
    /// <param name="itemId">Identificador de la variante.</param>
    /// <param name="items">Servicio de variantes.</param>
    /// <param name="currentUser">Quién la desactiva.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la variante desactivada, 404 si no existe, 409 si es la última activa de un producto activo.</returns>
    private static async Task<IResult> Deactivate(
        Guid itemId,
        ProductItemService items,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await items.DeactivateAsync(itemId, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome switch
        {
            ProductItemOutcome.Ok => Results.Ok(result.Item),
            ProductItemOutcome.NotFound => Results.NotFound(),
            _ => Problem(result.Error!, StatusCodes.Status409Conflict)
        };
    }

    /// <summary>Resuelve una variante por código exacto.</summary>
    /// <param name="codigo">Código del negocio o código de barras.</param>
    /// <param name="items">Servicio de variantes.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la variante y su producto, 404 si no hay ninguna activa con ese código.</returns>
    private static async Task<IResult> Lookup(string? codigo, ProductItemService items, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Results.NotFound();
        }

        var found = await items.LookupAsync(codigo.Trim(), cancellationToken);
        return found is null ? Results.NotFound() : Results.Ok(found);
    }

    private static IResult Invalid(string error) => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["variante"] = [error] },
        title: "Los datos de la variante no son válidos.");

    private static IResult Problem(string error, int statusCode) => Results.Problem(title: error, statusCode: statusCode);
}
