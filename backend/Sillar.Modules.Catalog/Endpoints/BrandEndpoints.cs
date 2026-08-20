using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Dtos;
using Sillar.Modules.Catalog.Services;

namespace Sillar.Modules.Catalog.Endpoints;

/// <summary>Marcas: listado público y administración.</summary>
public static class BrandEndpoints
{
    /// <summary>Monta las rutas de marcas.</summary>
    public static IEndpointRouteBuilder MapBrandEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/catalog/brands", ListPublic)
            .WithTags("Catálogo — Marcas")
            .WithName("ListPublicBrands")
            .WithSummary("Marcas activas con al menos un producto público.")
            .Produces<IReadOnlyList<BrandResponse>>(StatusCodes.Status200OK);

        var admin = endpoints.MapGroup("/api/admin/catalog/brands")
            .WithTags("Catálogo — Marcas")
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("", List)
            .WithName("ListAdminBrands")
            .WithSummary("Lista todas las marcas, activas e inactivas.")
            .Produces<IReadOnlyList<BrandAdminResponse>>(StatusCodes.Status200OK);

        admin.MapPost("", Create)
            .WithName("CreateBrand")
            .WithSummary("Da de alta una marca.")
            .Produces<BrandAdminResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPut("/{id:guid}", Update)
            .WithName("UpdateBrand")
            .WithSummary("Modifica una marca.")
            .WithDescription("El slug se envía tal cual: no se recalcula del nombre (regla 3).")
            .Produces<BrandAdminResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapDelete("/{id:guid}", Deactivate)
            .WithName("DeactivateBrand")
            .WithSummary("Desactiva una marca.")
            .WithDescription("Baja lógica: sus productos siguen existiendo y no pierden la marca.")
            .Produces<BrandAdminResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Marcas activas con al menos un producto público.</summary>
    /// <param name="brands">Servicio de marcas.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>Las marcas que tiene sentido ofrecer como filtro.</returns>
    private static async Task<IResult> ListPublic(BrandService brands, CancellationToken cancellationToken)
        => Results.Ok(await brands.ListPublicAsync(cancellationToken));

    /// <summary>Lista todas las marcas.</summary>
    /// <param name="brands">Servicio de marcas.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>Activas e inactivas.</returns>
    private static async Task<IResult> List(BrandService brands, CancellationToken cancellationToken)
        => Results.Ok(await brands.ListAsync(cancellationToken));

    /// <summary>Da de alta una marca.</summary>
    /// <param name="request">Datos de la marca.</param>
    /// <param name="brands">Servicio de marcas.</param>
    /// <param name="currentUser">Quién la crea.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>201 con la marca, 400 si los datos no sirven, 409 si el nombre o el slug ya existen.</returns>
    private static async Task<IResult> Create(
        CreateBrandRequest request,
        BrandService brands,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await brands.CreateAsync(request, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome switch
        {
            BrandOutcome.Ok => Results.Created($"/api/admin/catalog/brands/{result.Brand!.Id}", result.Brand),
            BrandOutcome.Conflict => Problem(result.Error!, StatusCodes.Status409Conflict),
            _ => Invalid(result.Error!)
        };
    }

    /// <summary>Modifica una marca.</summary>
    /// <param name="id">Identificador de la marca.</param>
    /// <param name="request">Datos nuevos.</param>
    /// <param name="brands">Servicio de marcas.</param>
    /// <param name="currentUser">Quién la modifica.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la marca, 404, 400 o 409 según el caso.</returns>
    private static async Task<IResult> Update(
        Guid id,
        UpdateBrandRequest request,
        BrandService brands,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await brands.UpdateAsync(id, request, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome switch
        {
            BrandOutcome.Ok => Results.Ok(result.Brand),
            BrandOutcome.NotFound => Results.NotFound(),
            BrandOutcome.Conflict => Problem(result.Error!, StatusCodes.Status409Conflict),
            _ => Invalid(result.Error!)
        };
    }

    /// <summary>Desactiva una marca.</summary>
    /// <param name="id">Identificador de la marca.</param>
    /// <param name="brands">Servicio de marcas.</param>
    /// <param name="currentUser">Quién la desactiva.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la marca desactivada, 404 si no existe.</returns>
    private static async Task<IResult> Deactivate(
        Guid id,
        BrandService brands,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await brands.DeactivateAsync(id, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken);

        return result.Outcome == BrandOutcome.Ok ? Results.Ok(result.Brand) : Results.NotFound();
    }

    private static IResult Invalid(string error) => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["marca"] = [error] },
        title: "Los datos de la marca no son válidos.");

    private static IResult Problem(string error, int statusCode) => Results.Problem(title: error, statusCode: statusCode);
}
