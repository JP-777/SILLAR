using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Contracts.Events;
using Sillar.Modules.Catalog.Data;
using Sillar.Modules.Catalog.Domain;
using Sillar.Modules.Catalog.Dtos;
using Sillar.Shared.Events;
using Sillar.Shared.Paging;
using static Sillar.Modules.Catalog.Services.Breadcrumb;

namespace Sillar.Modules.Catalog.Services;

/// <summary>Cómo terminó una operación sobre categorías.</summary>
internal enum CategoryOutcome
{
    Ok,
    NotFound,
    Invalid,
    Conflict
}

/// <summary>Resultado de una operación sobre categorías.</summary>
internal sealed record CategoryOperation(
    CategoryOutcome Outcome,
    string? Error = null,
    CategoryAdminResponse? Category = null);

/// <summary>Categorías del catálogo: árbol público y administración.</summary>
internal sealed class CategoryService(
    CatalogDbContext database,
    IMediaStorage media,
    IAuditWriter audit,
    IEventPublisher events,
    TimeProvider clock)
{
    /// <summary>Árbol de categorías activas, para la web pública.</summary>
    public async Task<IReadOnlyList<CategoryTreeNodeResponse>> GetPublicTreeAsync(CancellationToken cancellationToken)
    {
        var rows = await database.Categories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new { category.Id, category.ParentId, category.Name, category.Slug, category.ImageId })
            .ToListAsync(cancellationToken);

        var byParent = rows.ToLookup(row => row.ParentId);

        // Solo cuelga una categoría de su padre si el padre también está en
        // este conjunto (activo). Una rama entera bajo una categoría
        // desactivada no se promueve a la raíz: eso confundiría más de lo que
        // ayuda, y no lo pide ninguna regla. Simplemente no aparece, igual que
        // "nunca un enlace a algo invisible" en la miga de pan del producto.
        return Build(null);

        IReadOnlyList<CategoryTreeNodeResponse> Build(Guid? parentId)
            => [.. byParent[parentId].Select(row => new CategoryTreeNodeResponse(
                row.Slug,
                row.Name,
                row.ImageId is { } imageId ? media.GetPublicUrl(imageId) : null,
                Build(row.Id)))];
    }

    /// <summary>
    /// Una categoría activa con sus productos públicos, paginados.
    /// </summary>
    /// <remarks>
    /// 404, no una lista vacía disfrazada: una categoría inexistente o
    /// desactivada no se distingue de una que no tiene productos, porque
    /// ninguna de las dos existe para quien navega la web.
    /// </remarks>
    public async Task<(bool Found, CategoryTreeNodeResponse Category, IReadOnlyList<BreadcrumbItemResponse> Breadcrumb, PagedResult<ProductCardResponse> Products)>
        GetPublicDetailAsync(string slug, PageRequest page, CancellationToken cancellationToken)
    {
        var found = await database.Categories
            .AsNoTracking()
            .Where(category => category.Slug == slug && category.IsActive)
            .Select(category => new { category.Id, category.ParentId, category.Name, category.Slug, category.ImageId })
            .FirstOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return (false, null!, [], PagedResult<ProductCardResponse>.From(page, [], 0));
        }

        var nodes = await database.Categories
            .AsNoTracking()
            .Select(category => new CategoryNode(category.Id, category.Slug, category.Name, category.ParentId, category.IsActive))
            .ToDictionaryAsync(node => node.Id, cancellationToken);

        var trail = BuildTrail(nodes[found.Id], nodes)
            .Select(node => new BreadcrumbItemResponse(node.Slug, node.Name))
            .ToList();

        var query = database.Products
            .AsNoTracking()
            .Where(product => product.IsActive && product.IsPublic
                && product.Categories.Any(link => link.CategoryId == found.Id));

        var total = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderBy(product => product.Name)
            .Skip(page.Skip)
            .Take(page.Size)
            .Select(product => new
            {
                product.Slug,
                product.Name,
                product.ShortDescription,
                product.ListPrice,
                // Principal si hay una marcada; si no, la de menor sort_order
                // (regla 11): OrderByDescending(bool) pone primero la marcada.
                CardImageId = product.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => (Guid?)image.MediaAssetId)
                    .FirstOrDefault(),
                // El precio propio de cada presentación **activa**: sin esto
                // la tarjeta enseña el de lista aunque nadie lo cobre.
                PriceOverrides = product.Items
                    .Where(item => item.IsActive)
                    .Select(item => item.PriceOverride)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var cards = items
            .Select(item =>
            {
                var (price, from) = ItemPricing.ForCard(item.PriceOverrides, item.ListPrice);

                return new ProductCardResponse(
                    item.Slug,
                    item.Name,
                    item.ShortDescription,
                    item.CardImageId is { } imageId ? media.GetPublicUrl(imageId) : null,
                    price,
                    from);
            })
            .ToList();

        var category = new CategoryTreeNodeResponse(
            found.Slug,
            found.Name,
            found.ImageId is { } categoryImageId ? media.GetPublicUrl(categoryImageId) : null,
            []);

        return (true, category, trail, PagedResult<ProductCardResponse>.From(page, cards, total));
    }

    /// <summary>Todas las categorías, activas e inactivas, para la administración.</summary>
    /// <remarks>
    /// Se materializa **antes** de proyectar, por lo mismo que
    /// <c>BrandService.ListAsync</c>: con <c>.Select(Project)</c> dentro de la
    /// consulta, EF Core aborta porque la proyección de cliente referencia al
    /// servicio a través de un método de instancia. Es el segundo caso del
    /// mismo defecto en este módulo; lo vigila ahora `api-traduccion.spec.ts`.
    /// </remarks>
    public async Task<IReadOnlyList<CategoryAdminResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var categories = await database.Categories
            .AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);

        // Una sola consulta agrupada para todos los recuentos, no una por
        // categoría: con veinte categorías serían veinte viajes a la base
        // para pintar una pantalla.
        var counts = await database.ProductCategories
            .Where(pc => pc.Product!.IsActive)
            .GroupBy(pc => pc.CategoryId)
            .Select(group => new { CategoryId = group.Key, Total = group.Count() })
            .ToDictionaryAsync(row => row.CategoryId, row => row.Total, cancellationToken);

        return categories
            .Select(category => Project(category, counts.GetValueOrDefault(category.Id)))
            .ToList();
    }

    /// <summary>Da de alta una categoría.</summary>
    public async Task<CategoryOperation> CreateAsync(
        CreateCategoryRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Invalid("El nombre es obligatorio.");
        }

        var slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugGenerator.From(request.Name) : request.Slug.Trim();
        if (!SlugGenerator.IsValidFormat(slug))
        {
            return Invalid("El slug solo admite minúsculas, dígitos y guiones simples, sin uno al principio ni al final.");
        }

        if (request.ParentId is { } parentId && !await database.Categories.AnyAsync(c => c.Id == parentId, cancellationToken))
        {
            return Invalid("La categoría padre indicada no existe.");
        }

        var imageError = ValidateImage(request.ImageId);
        if (imageError is not null)
        {
            return Invalid(imageError);
        }

        var category = new Category
        {
            Name = request.Name.Trim(),
            Slug = slug,
            ParentId = request.ParentId,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ImageId = request.ImageId,
            SortOrder = request.SortOrder ?? 0,
            IsActive = true
        };

        database.Categories.Add(category);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return new CategoryOperation(CategoryOutcome.Conflict, "Ya existe una categoría con ese slug.");
        }

        await AuditAsync(AuditAction.Create, actingUserId, actingEmail, category,
            $"Alta de la categoría «{category.Name}».", cancellationToken);

        // Recién creada: no puede tener productos todavía. El cero es un
        // hecho, no un relleno.
        return new CategoryOperation(CategoryOutcome.Ok, Category: Project(category, 0));
    }

    /// <summary>Modifica una categoría.</summary>
    public async Task<CategoryOperation> UpdateAsync(
        Guid categoryId,
        UpdateCategoryRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Invalid("El nombre es obligatorio.");
        }

        if (!SlugGenerator.IsValidFormat(request.Slug))
        {
            return Invalid("El slug solo admite minúsculas, dígitos y guiones simples, sin uno al principio ni al final.");
        }

        var category = await database.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return new CategoryOperation(CategoryOutcome.NotFound);
        }

        if (request.ParentId is { } parentId)
        {
            if (parentId == categoryId)
            {
                return Invalid("Una categoría no puede ser su propia padre.");
            }

            var parentById = await database.Categories
                .AsNoTracking()
                .Select(c => new { c.Id, c.ParentId })
                .ToDictionaryAsync(c => c.Id, c => c.ParentId, cancellationToken);

            if (!parentById.ContainsKey(parentId))
            {
                return Invalid("La categoría padre indicada no existe.");
            }

            if (CategoryTree.CreatesCycle(parentById, categoryId, parentId))
            {
                return Invalid("Esa categoría no puede ser su padre: formaría un ciclo con una de sus propias descendientes.");
            }
        }

        var imageError = ValidateImage(request.ImageId);
        if (imageError is not null)
        {
            return Invalid(imageError);
        }

        category.Name = request.Name.Trim();
        category.Slug = request.Slug!.Trim();
        category.ParentId = request.ParentId;
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.ImageId = request.ImageId;
        category.SortOrder = request.SortOrder ?? category.SortOrder;
        category.IsActive = request.IsActive;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return new CategoryOperation(CategoryOutcome.Conflict, "Ya existe una categoría con ese slug.");
        }

        await AuditAsync(AuditAction.Update, actingUserId, actingEmail, category,
            $"Modificación de la categoría «{category.Name}».", cancellationToken);

        return new CategoryOperation(
            CategoryOutcome.Ok,
            Category: Project(category, await CountProductsAsync(category.Id, cancellationToken)));
    }

    /// <summary>
    /// Desactiva una categoría. Baja lógica, sin cascada (regla 9): avisa
    /// cuántos productos activos la tenían y se quedan sin ella.
    /// </summary>
    public async Task<(CategoryOutcome Outcome, DeactivateCategoryResponse? Result)> DeactivateAsync(
        Guid categoryId,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var category = await database.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return (CategoryOutcome.NotFound, null);
        }

        var affected = await database.ProductCategories
            .Where(pc => pc.CategoryId == categoryId && pc.Product!.IsActive)
            .CountAsync(cancellationToken);

        if (!category.IsActive)
        {
            return (CategoryOutcome.Ok, new DeactivateCategoryResponse(Project(category, affected), affected));
        }

        category.IsActive = false;
        await database.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditAction.Delete, actingUserId, actingEmail, category,
            $"Baja de la categoría «{category.Name}». {affected} producto(s) activo(s) se quedan sin ella, sin desactivarse.",
            cancellationToken);

        await events.PublishAsync(new CategoriaDesactivada(category.Id, clock.GetUtcNow()), cancellationToken);

        return (CategoryOutcome.Ok, new DeactivateCategoryResponse(Project(category, affected), affected));
    }

    /// <summary>
    /// Comprueba que un archivo de medios exista y esté activo, a través del
    /// contrato de CORE. Nunca se consulta <c>core.media_assets</c>
    /// directamente: un módulo no mapea la tabla de otro.
    /// </summary>
    private string? ValidateImage(Guid? imageId)
        => imageId is null || media.GetPublicUrl(imageId.Value) is not null
            ? null
            : "La imagen indicada no existe o no está activa.";

    private Task AuditAsync(
        string action,
        int actingUserId,
        string actingEmail,
        Category affected,
        string summary,
        CancellationToken cancellationToken)
        => audit.WriteAsync(
            new AuditEntry(action)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CatalogModule.ModuleCode,
                EntityType = "category",
                EntityId = affected.Id.ToString(),
                Summary = summary
            },
            cancellationToken);

    private static CategoryOperation Invalid(string error) => new(CategoryOutcome.Invalid, error);

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>Cuántos productos activos tienen esta categoría.</summary>
    private Task<int> CountProductsAsync(Guid categoryId, CancellationToken cancellationToken)
        => database.ProductCategories
            .Where(pc => pc.CategoryId == categoryId && pc.Product!.IsActive)
            .CountAsync(cancellationToken);

    /// <param name="productCount">
    /// Se pasa desde fuera y **nunca se inventa**: cada quien lo calcula como
    /// le sale barato —el listado con una sola consulta agrupada, el resto de
    /// uno en uno—, pero ninguno devuelve un cero de relleno. Un recuento
    /// falso en una respuesta es peor que no tenerlo, porque la pantalla lo
    /// enseña sin poder saber que miente.
    /// </param>
    /// <param name="category">La categoría que se proyecta.</param>
    private CategoryAdminResponse Project(Category category, int productCount) => new(
        category.Id,
        category.ParentId,
        category.Name,
        category.Slug,
        category.Description,
        category.ImageId,
        category.ImageId is { } imageId ? media.GetPublicUrl(imageId) : null,
        category.SortOrder,
        category.IsActive,
        productCount);
}
