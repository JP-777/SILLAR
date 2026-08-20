using Sillar.Shared.Paging;

namespace Sillar.Modules.Catalog.Dtos;

/// <summary>Una categoría con sus productos públicos, paginados.</summary>
/// <param name="Slug">Para la URL pública.</param>
/// <param name="Name">Nombre visible.</param>
/// <param name="Breadcrumb">
/// De la raíz hasta esta categoría. Vacía si algún antecesor está desactivado:
/// nunca un enlace a algo invisible.
/// </param>
/// <param name="ImageUrl">Portada, si tiene.</param>
/// <param name="Products">Sus productos activos y públicos, paginados.</param>
public sealed record CategoryDetailResponse(
    string Slug,
    string Name,
    IReadOnlyList<BreadcrumbItemResponse> Breadcrumb,
    string? ImageUrl,
    PagedResult<ProductCardResponse> Products);

/// <summary>Un nodo del árbol público de categorías.</summary>
/// <param name="Slug">Para la URL pública. El <c>uuid</c> no se muestra nunca (regla 14).</param>
/// <param name="Name">Nombre visible.</param>
/// <param name="ImageUrl">Portada, si tiene.</param>
/// <param name="Children">Hijas activas, en el mismo formato.</param>
public sealed record CategoryTreeNodeResponse(
    string Slug,
    string Name,
    string? ImageUrl,
    IReadOnlyList<CategoryTreeNodeResponse> Children);

/// <summary>Una categoría vista desde la administración.</summary>
/// <param name="Id">Identificador. No se muestra en la interfaz (regla 14); lo usa el propio panel para operar.</param>
/// <param name="ParentId">Categoría padre, si tiene.</param>
/// <param name="Name">Nombre visible.</param>
/// <param name="Slug">Para la URL pública.</param>
/// <param name="Description">Cabecera de la categoría.</param>
/// <param name="ImageId">Portada, en <c>core.media_assets</c>.</param>
/// <param name="ImageUrl">Portada ya resuelta.</param>
/// <param name="SortOrder">Orden de presentación.</param>
/// <param name="IsActive">Baja lógica.</param>
/// <param name="ProductCount">
/// Cuántos productos **activos** tienen esta categoría hoy.
/// <para>
/// Viaja en el listado porque la regla 9 del SPEC pide avisar cuántos se
/// quedan sin ella **antes** de desactivar, para que la persona decida. El
/// recuento que devuelve la baja llega después de haber decidido, así que no
/// sirve para eso. Mismo patrón que <c>ModuleResponse.RestartsAutomatically</c>:
/// el listado carga lo que el diálogo de confirmación necesita saber antes de
/// actuar.
/// </para>
/// </param>
public sealed record CategoryAdminResponse(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Slug,
    string? Description,
    Guid? ImageId,
    string? ImageUrl,
    int SortOrder,
    bool IsActive,
    int ProductCount);

/// <summary>Da de alta una categoría.</summary>
/// <param name="Name">Obligatorio.</param>
/// <param name="Slug">Opcional: si falta, se genera del nombre.</param>
/// <param name="ParentId">Categoría padre, opcional.</param>
/// <param name="Description">Opcional.</param>
/// <param name="ImageId">Portada, opcional. Debe existir en <c>core.media_assets</c> y estar activa.</param>
/// <param name="SortOrder">Opcional, por defecto 0.</param>
public sealed record CreateCategoryRequest(
    string? Name,
    string? Slug,
    Guid? ParentId,
    string? Description,
    Guid? ImageId,
    int? SortOrder);

/// <summary>Modifica una categoría.</summary>
/// <param name="Name">Obligatorio.</param>
/// <param name="Slug">
/// Obligatorio. <b>No se recalcula del nombre</b> (regla 3): quien quiera
/// cambiarlo lo escribe a propósito.
/// </param>
/// <param name="ParentId">Categoría padre, opcional.</param>
/// <param name="Description">Opcional.</param>
/// <param name="ImageId">Portada, opcional.</param>
/// <param name="SortOrder">Orden de presentación.</param>
/// <param name="IsActive">Baja lógica.</param>
public sealed record UpdateCategoryRequest(
    string? Name,
    string? Slug,
    Guid? ParentId,
    string? Description,
    Guid? ImageId,
    int? SortOrder,
    bool IsActive);

/// <summary>Resultado de desactivar una categoría.</summary>
/// <param name="Category">Cómo quedó.</param>
/// <param name="ProductsLosingThisCategory">
/// Cuántos productos activos la tenían y se quedan sin ella. No se desactiva
/// ninguno: la baja no actúa en cascada (regla 9). Es un aviso, no un bloqueo.
/// </param>
public sealed record DeactivateCategoryResponse(CategoryAdminResponse Category, int ProductsLosingThisCategory);
