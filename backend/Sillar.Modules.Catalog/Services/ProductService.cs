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

/// <summary>Cómo terminó una operación sobre productos.</summary>
internal enum ProductOutcome
{
    Ok,
    NotFound,
    Invalid,
    Conflict
}

/// <summary>Resultado de una operación sobre productos.</summary>
internal sealed record ProductOperation(
    ProductOutcome Outcome,
    string? Error = null,
    ProductAdminResponse? Product = null);

/// <summary>Productos del catálogo: listado y ficha pública, y administración.</summary>
internal sealed class ProductService(
    CatalogDbContext database,
    IMediaStorage media,
    IAuditWriter audit,
    IEventPublisher events,
    TimeProvider clock)
{
    /// <summary>Listado público, filtrado y paginado.</summary>
    /// <remarks>
    /// Solo <c>is_active AND is_public</c> (regla del SPEC §8): lo demás no
    /// existe para quien navega la web.
    /// </remarks>
    public async Task<PagedResult<ProductCardResponse>> GetPublicListAsync(
        string? categorySlug,
        string? brandSlug,
        string? searchText,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = database.Products.AsNoTracking().Where(product => product.IsActive && product.IsPublic);

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            query = query.Where(product => product.Categories.Any(link => link.Category!.Slug == categorySlug));
        }

        if (!string.IsNullOrWhiteSpace(brandSlug))
        {
            query = query.Where(product => product.Brand != null && product.Brand.Slug == brandSlug);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            // Ni LIKE ni ILIKE: name y short_description llevan core.es_search,
            // colación no determinista, y PostgreSQL no los admite sobre ella
            // (DATOS.md §4). El índice GIN de idx_products_busqueda es la única
            // vía, y es lo que de verdad hace que "lapiz" encuentre "LÁPIZ" —
            // por spanish_stem, no por la colación (DATOS.md §4).
            query = query.Where(product => EF.Functions.ToTsVector(
                    "spanish", product.Name + " " + (product.ShortDescription ?? ""))
                .Matches(EF.Functions.PlainToTsQuery("spanish", searchText)));
        }

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

        return PagedResult<ProductCardResponse>.From(page, cards, total);
    }

    /// <summary>Ficha completa de un producto público.</summary>
    public async Task<ProductDetailResponse?> GetPublicDetailAsync(string slug, CancellationToken cancellationToken)
    {
        var product = await database.Products
            .AsNoTracking()
            .Where(candidate => candidate.Slug == slug && candidate.IsActive && candidate.IsPublic)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
                candidate.ShortDescription,
                candidate.Description,
                candidate.SaleUnit,
                candidate.VariantLabel,
                candidate.ListPrice,
                candidate.PrimaryCategoryId,
                BrandName = candidate.Brand != null ? candidate.Brand.Name : null,
                BrandSlug = candidate.Brand != null ? candidate.Brand.Slug : null,
                Categories = candidate.Categories.Select(link => link.CategoryId).ToList(),
                Images = candidate.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => new { image.MediaAssetId, image.AltText, image.IsPrimary })
                    .ToList(),
                Items = candidate.Items
                    .Where(item => item.IsActive)
                    .OrderBy(item => item.SortOrder)
                    .Select(item => new { item.VariantValue, item.Code, item.Barcode, item.PriceOverride, item.ImageId })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return null;
        }

        var breadcrumb = await BuildProductBreadcrumbAsync(product.PrimaryCategoryId, product.Categories, cancellationToken);

        // **Una fila cuyo archivo se dio de baja no viaja.** Antes salía con la
        // url vacía, que en la ficha es un hueco: el producto enseñaba una
        // imagen que ya no existe. La fila se queda en la base —la baja del
        // archivo es lógica y CORE no escribe en el schema del catálogo—, pero
        // deja de leerse.
        var images = product.Images
            .Select(image => new { Image = image, Url = media.GetPublicUrl(image.MediaAssetId) })
            .Where(par => par.Url is not null)
            .Select(par => new ProductImageResponse(par.Url!, par.Image.AltText, par.Image.IsPrimary))
            .ToList();

        var variants = product.Items
            .Select(item => new ProductVariantResponse(
                item.VariantValue,
                item.Code,
                item.Barcode,
                ItemPricing.Effective(item.PriceOverride, product.ListPrice),
                item.ImageId is { } imageId ? media.GetPublicUrl(imageId) : null))
            .ToList();

        return new ProductDetailResponse(
            slug,
            product.Name,
            product.ShortDescription,
            product.Description,
            product.BrandName,
            product.BrandSlug,
            breadcrumb,
            images,
            variants,
            product.SaleUnit,
            product.VariantLabel);
    }

    /// <summary>
    /// Elige la categoría de la miga de pan (la principal si está activa, si
    /// no otra activa del producto) y arma la ruta desde la raíz.
    /// </summary>
    private async Task<IReadOnlyList<BreadcrumbItemResponse>> BuildProductBreadcrumbAsync(
        Guid? primaryCategoryId,
        IReadOnlyList<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        if (categoryIds.Count == 0)
        {
            return [];
        }

        var nodes = await database.Categories
            .AsNoTracking()
            .Select(category => new CategoryNode(category.Id, category.Slug, category.Name, category.ParentId, category.IsActive))
            .ToDictionaryAsync(node => node.Id, cancellationToken);

        var primary = primaryCategoryId is { } id && nodes.TryGetValue(id, out var found) ? found : null;
        var others = categoryIds.Where(cid => cid != primaryCategoryId).Select(cid => nodes.GetValueOrDefault(cid)).OfType<CategoryNode>().ToList();

        var target = ChooseTarget(primary, others);

        return target is null ? [] : [.. BuildTrail(target, nodes).Select(node => new BreadcrumbItemResponse(node.Slug, node.Name))];
    }

    /// <summary>Lista productos para la administración, con filtros.</summary>
    public async Task<PagedResult<ProductAdminListItemResponse>> ListAsync(
        string? searchText,
        bool? isActive,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = database.Products.AsNoTracking();

        if (isActive is { } active)
        {
            query = query.Where(product => product.IsActive == active);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(product => EF.Functions.ToTsVector("spanish", product.Name)
                .Matches(EF.Functions.PlainToTsQuery("spanish", searchText)));
        }

        var total = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderBy(product => product.Name)
            .Skip(page.Skip)
            .Take(page.Size)
            .Select(product => new ProductAdminListItemResponse(
                product.Id,
                product.Name,
                product.Slug,
                product.Brand != null ? product.Brand.Name : null,
                product.ListPrice,
                product.IsPublic,
                product.IsActive))
            .ToListAsync(cancellationToken);

        return PagedResult<ProductAdminListItemResponse>.From(page, items, total);
    }

    /// <summary>Ficha completa de un producto para la administración.</summary>
    public async Task<ProductAdminResponse?> GetByIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await database.Products
            .AsNoTracking()
            .Include(p => p.Items)
            .Include(p => p.Images)
            .Include(p => p.Categories)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        return product is null ? null : Project(product);
    }

    /// <summary>
    /// Da de alta un producto, con su variante única (regla 2). Quien llama
    /// nunca ve ni menciona una variante.
    /// </summary>
    public async Task<ProductOperation> CreateAsync(
        CreateProductRequest request,
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

        if (request.ListPrice is < 0)
        {
            return Invalid("El precio de lista no puede ser negativo.");
        }

        var categoryIds = (request.CategoryIds ?? []).Distinct().ToList();

        if (request.PrimaryCategoryId is { } primaryId && !categoryIds.Contains(primaryId))
        {
            return Invalid("La categoría principal tiene que ser una de las categorías del producto (regla 6).");
        }

        if (categoryIds.Count > 0)
        {
            var existing = await database.Categories.Where(c => categoryIds.Contains(c.Id)).Select(c => c.Id).ToListAsync(cancellationToken);
            var missing = categoryIds.Except(existing).ToList();
            if (missing.Count > 0)
            {
                return Invalid($"{missing.Count} de las categorías indicadas no existe(n).");
            }
        }

        if (request.BrandId is { } brandId && !await database.Brands.AnyAsync(b => b.Id == brandId, cancellationToken))
        {
            return Invalid("La marca indicada no existe.");
        }

        var product = new Product
        {
            Name = request.Name.Trim(),
            Slug = slug,
            ShortDescription = string.IsNullOrWhiteSpace(request.ShortDescription) ? null : request.ShortDescription.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PrimaryCategoryId = request.PrimaryCategoryId,
            BrandId = request.BrandId,
            ListPrice = request.ListPrice,
            SaleUnit = string.IsNullOrWhiteSpace(request.SaleUnit) ? null : request.SaleUnit.Trim(),
            VariantLabel = string.IsNullOrWhiteSpace(request.VariantLabel) ? null : request.VariantLabel.Trim(),
            IsPublic = true,
            IsActive = true
        };

        database.Products.Add(product);

        // La variante única, regla 2: nace con el producto, sin nombre, sin
        // que quien llama al API la mencione. Code/Barcode del request son en
        // realidad suyos, no del producto — el formulario los muestra como si
        // lo fueran mientras solo haya una (SPEC §9).
        var initialItem = new ProductItem
        {
            Product = product,
            ProductId = product.Id,
            VariantValue = null,
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim(),
            IsActive = true
        };

        database.ProductItems.Add(initialItem);

        foreach (var categoryId in categoryIds)
        {
            database.ProductCategories.Add(new ProductCategory { ProductId = product.Id, CategoryId = categoryId });
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return new ProductOperation(ProductOutcome.Conflict, ConflictMessageFor(postgres));
        }

        await AuditAsync(AuditAction.Create, actingUserId, actingEmail, product,
            $"Alta del producto «{product.Name}».", cancellationToken);

        await events.PublishAsync(new ProductoCreado(product.Id, clock.GetUtcNow()), cancellationToken);

        var created = await database.Products
            .AsNoTracking()
            .Include(p => p.Items).Include(p => p.Images).Include(p => p.Categories)
            .FirstAsync(p => p.Id == product.Id, cancellationToken);

        return new ProductOperation(ProductOutcome.Ok, Product: Project(created));
    }

    /// <summary>Modifica los datos del producto (no sus categorías, ni sus imágenes, ni sus variantes).</summary>
    public async Task<ProductOperation> UpdateAsync(
        Guid productId,
        UpdateProductRequest request,
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

        if (request.ListPrice is < 0)
        {
            return Invalid("El precio de lista no puede ser negativo.");
        }

        var product = await database.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            return new ProductOperation(ProductOutcome.NotFound);
        }

        if (request.BrandId is { } brandId && !await database.Brands.AnyAsync(b => b.Id == brandId, cancellationToken))
        {
            return Invalid("La marca indicada no existe.");
        }

        // Los campos de la variante única, cuando quien llama los manda.
        //
        // **Se rechaza con varias, no se ignora.** Con más de una presentación
        // estos campos son de cada una, y aplicarlos a una al azar o
        // descartarlos en silencio es cómo se pierde una edición sin que nadie
        // se entere. La regla es la del SPEC: mientras haya una sola, sus
        // campos son campos del producto.
        List<ProductItem> items = [];

        if (request.SingleVariantFieldsPresent)
        {
            items = await database.ProductItems
                .Where(item => item.ProductId == productId)
                .ToListAsync(cancellationToken);

            if (items.Count != 1)
            {
                return Invalid(
                    $"«{product.Name}» tiene {items.Count} presentaciones, así que el código y el " +
                    "código de barras son de cada una y no del producto. Edítalos en su tabla.");
            }

            var única = items[0];
            única.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
            única.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        }

        product.Name = request.Name.Trim();
        product.Slug = request.Slug!.Trim();
        product.ShortDescription = string.IsNullOrWhiteSpace(request.ShortDescription) ? null : request.ShortDescription.Trim();
        product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        product.BrandId = request.BrandId;
        product.ListPrice = request.ListPrice;
        product.SaleUnit = string.IsNullOrWhiteSpace(request.SaleUnit) ? null : request.SaleUnit.Trim();
        product.VariantLabel = string.IsNullOrWhiteSpace(request.VariantLabel) ? null : request.VariantLabel.Trim();
        product.IsPublic = request.IsPublic;
        product.IsActive = request.IsActive;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return new ProductOperation(ProductOutcome.Conflict, ConflictMessageFor(postgres));
        }

        await AuditAsync(AuditAction.Update, actingUserId, actingEmail, product,
            $"Modificación del producto «{product.Name}».", cancellationToken);

        await events.PublishAsync(new ProductoActualizado(product.Id, clock.GetUtcNow()), cancellationToken);

        var updated = await database.Products
            .AsNoTracking()
            .Include(p => p.Items).Include(p => p.Images).Include(p => p.Categories)
            .FirstAsync(p => p.Id == product.Id, cancellationToken);

        return new ProductOperation(ProductOutcome.Ok, Product: Project(updated));
    }

    /// <summary>Desactiva un producto. Baja lógica: sigue existiendo en pedidos y ventas anteriores (regla 7).</summary>
    public async Task<ProductOperation> DeactivateAsync(
        Guid productId,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var product = await database.Products
            .Include(p => p.Items).Include(p => p.Images).Include(p => p.Categories)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
        {
            return new ProductOperation(ProductOutcome.NotFound);
        }

        if (!product.IsActive)
        {
            return new ProductOperation(ProductOutcome.Ok, Product: Project(product));
        }

        product.IsActive = false;
        await database.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditAction.Delete, actingUserId, actingEmail, product,
            $"Baja del producto «{product.Name}».", cancellationToken);

        await events.PublishAsync(new ProductoDesactivado(product.Id, clock.GetUtcNow()), cancellationToken);

        return new ProductOperation(ProductOutcome.Ok, Product: Project(product));
    }

    /// <summary>Fija el conjunto de categorías de un producto y cuál es la principal (regla 6).</summary>
    public async Task<ProductOperation> SetCategoriesAsync(
        Guid productId,
        SetProductCategoriesRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var product = await database.Products
            .Include(p => p.Categories)
            .Include(p => p.Items).Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
        {
            return new ProductOperation(ProductOutcome.NotFound);
        }

        var categoryIds = (request.CategoryIds ?? []).Distinct().ToList();

        if (request.PrimaryCategoryId is { } primaryId && !categoryIds.Contains(primaryId))
        {
            return Invalid("La categoría principal tiene que ser una de las categorías del producto (regla 6).");
        }

        if (categoryIds.Count > 0)
        {
            var existing = await database.Categories.Where(c => categoryIds.Contains(c.Id)).Select(c => c.Id).ToListAsync(cancellationToken);
            var missing = categoryIds.Except(existing).ToList();
            if (missing.Count > 0)
            {
                return Invalid($"{missing.Count} de las categorías indicadas no existe(n).");
            }
        }

        database.ProductCategories.RemoveRange(product.Categories);
        foreach (var categoryId in categoryIds)
        {
            database.ProductCategories.Add(new ProductCategory { ProductId = product.Id, CategoryId = categoryId });
        }

        product.PrimaryCategoryId = request.PrimaryCategoryId;

        await database.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditAction.Update, actingUserId, actingEmail, product,
            $"Categorías de «{product.Name}» actualizadas.", cancellationToken);

        // Cambiar las categorías **cambia lo que M01 publica** de este
        // producto: la efectiva alimenta la miga de pan, y quien guarde un
        // snapshot con ella se queda con un nombre que ya no es el suyo.
        await events.PublishAsync(new ProductoActualizado(product.Id, clock.GetUtcNow()), cancellationToken);

        var updated = await database.Products
            .AsNoTracking()
            .Include(p => p.Items).Include(p => p.Images).Include(p => p.Categories)
            .FirstAsync(p => p.Id == product.Id, cancellationToken);

        return new ProductOperation(ProductOutcome.Ok, Product: Project(updated));
    }

    /// <summary>Traduce una violación de restricción única al mensaje que corresponde.</summary>
    private static string ConflictMessageFor(PostgresException exception) => exception.ConstraintName switch
    {
        "uq_products_slug" => "Ya existe un producto con ese slug.",
        "uq_product_items_code" => "Ya existe una variante con ese código en toda la instalación.",
        "uq_product_items_barcode" => "Ya existe una variante con ese código de barras en toda la instalación.",
        _ => "Ya existe algo con ese mismo dato único."
    };

    private Task AuditAsync(
        string action,
        int actingUserId,
        string actingEmail,
        Product affected,
        string summary,
        CancellationToken cancellationToken)
        => audit.WriteAsync(
            new AuditEntry(action)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CatalogModule.ModuleCode,
                EntityType = "product",
                EntityId = affected.Id.ToString(),
                Summary = summary
            },
            cancellationToken);

    private static ProductOperation Invalid(string error) => new(ProductOutcome.Invalid, error);

    private ProductAdminResponse Project(Product product) => new(
        product.Id,
        product.Name,
        product.Slug,
        product.ShortDescription,
        product.Description,
        product.PrimaryCategoryId,
        product.BrandId,
        product.ListPrice,
        product.SaleUnit,
        product.VariantLabel,
        product.IsPublic,
        product.IsActive,
        [.. product.Categories.Select(link => link.CategoryId)],
        [.. product.Items.OrderBy(item => item.SortOrder).Select(item => new ProductItemResponse(
            item.Id,
            item.ProductId,
            item.VariantValue,
            item.Code,
            item.Barcode,
            item.PriceOverride,
            ItemPricing.Effective(item.PriceOverride, product.ListPrice),
            item.ImageId,
            item.ImageId is { } imageId ? media.GetPublicUrl(imageId) : null,
            item.SortOrder,
            item.IsActive))],
        // Igual que en la ficha pública: la fila de un archivo dado de baja no
        // se lee. Ver el comentario de `GetPublicDetailAsync`.
        [.. product.Images
            .OrderBy(image => image.SortOrder)
            .Select(image => new { Image = image, Url = media.GetPublicUrl(image.MediaAssetId) })
            .Where(par => par.Url is not null)
            .Select(par => new ProductImageAdminResponse(
                par.Image.Id,
                par.Image.MediaAssetId,
                par.Url!,
                par.Image.AltText,
                par.Image.SortOrder,
                par.Image.IsPrimary))]);
}
