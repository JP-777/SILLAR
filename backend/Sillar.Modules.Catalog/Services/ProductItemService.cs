using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Contracts.Events;
using Sillar.Modules.Catalog.Data;
using Sillar.Modules.Catalog.Domain;
using Sillar.Modules.Catalog.Dtos;
using Sillar.Shared.Events;

namespace Sillar.Modules.Catalog.Services;

/// <summary>Cómo terminó una operación sobre variantes.</summary>
internal enum ProductItemOutcome
{
    Ok,
    NotFound,
    Invalid,
    Conflict
}

/// <summary>Resultado de una operación sobre variantes.</summary>
internal sealed record ProductItemOperation(
    ProductItemOutcome Outcome,
    string? Error = null,
    ProductItemResponse? Item = null);

/// <summary>
/// Variantes (<c>product_items</c>): la segunda y siguientes de un producto, y
/// su administración. La primera nace con el producto (regla 2) y no se crea
/// aquí.
/// </summary>
internal sealed class ProductItemService(
    CatalogDbContext database,
    IMediaStorage media,
    IAuditWriter audit,
    IEventPublisher events,
    TimeProvider clock)
{
    /// <summary>Las variantes de un producto, para la administración.</summary>
    public async Task<IReadOnlyList<ProductItemResponse>?> ListByProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await database.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new { p.ListPrice })
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return null;
        }

        var items = await database.ProductItems.AsNoTracking()
            .Where(item => item.ProductId == productId)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);

        return [.. items.Select(item => Project(item, product.ListPrice))];
    }

    /// <summary>Crea la segunda variante o siguiente de un producto.</summary>
    public async Task<ProductItemOperation> CreateAsync(
        Guid productId,
        CreateProductItemRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.VariantValue))
        {
            return Invalid("El valor de la variante es obligatorio: es lo que la distingue de las demás.");
        }

        if (request.PriceOverride is < 0)
        {
            return Invalid("El precio no puede ser negativo.");
        }

        var product = await database.Products
            .Where(p => p.Id == productId)
            .Select(p => new { p.Id, p.ListPrice })
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return new ProductItemOperation(ProductItemOutcome.NotFound);
        }

        var imageError = ValidateImage(request.ImageId);
        if (imageError is not null)
        {
            return Invalid(imageError);
        }

        var maxSortOrder = await database.ProductItems
            .Where(item => item.ProductId == productId)
            .Select(item => (int?)item.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        var item = new ProductItem
        {
            ProductId = productId,
            VariantValue = request.VariantValue.Trim(),
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim(),
            PriceOverride = request.PriceOverride,
            ImageId = request.ImageId,
            SortOrder = maxSortOrder + 1,
            IsActive = true
        };

        database.ProductItems.Add(item);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return new ProductItemOperation(
                ProductItemOutcome.Conflict,
                await ConflictMessageForAsync(postgres, request.Code, request.Barcode, cancellationToken));
        }

        await AuditAsync(AuditAction.Create, actingUserId, actingEmail, item,
            $"Alta de la variante «{item.VariantValue}».", cancellationToken);

        await events.PublishAsync(new VarianteCreada(item.Id, productId, clock.GetUtcNow()), cancellationToken);

        return new ProductItemOperation(ProductItemOutcome.Ok, Item: Project(item, product.ListPrice));
    }

    /// <summary>Modifica una variante, incluida su baja o reactivación.</summary>
    public async Task<ProductItemOperation> UpdateAsync(
        Guid itemId,
        UpdateProductItemRequest request,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        if (request.PriceOverride is < 0)
        {
            return Invalid("El precio no puede ser negativo.");
        }

        var item = await database.ProductItems.FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);
        if (item is null)
        {
            return new ProductItemOperation(ProductItemOutcome.NotFound);
        }

        var isDeactivating = item.IsActive && !request.IsActive;
        if (isDeactivating)
        {
            var blocked = await BlockedDeactivationReasonAsync(item, cancellationToken);
            if (blocked is not null)
            {
                return new ProductItemOperation(ProductItemOutcome.Conflict, blocked);
            }
        }

        var imageError = ValidateImage(request.ImageId);
        if (imageError is not null)
        {
            return Invalid(imageError);
        }

        // variant_value nulo solo vale para la variante única del producto; el
        // CHECK de la base no distingue eso, así que aquí no se admite dejarlo
        // en blanco si ya tiene valor: la única con nulo es la que crea el
        // producto, y esta operación nunca fabrica una segunda variante nula.
        if (string.IsNullOrWhiteSpace(request.VariantValue) && item.VariantValue is not null)
        {
            return Invalid("El valor de la variante no puede quedar vacío.");
        }

        item.VariantValue = string.IsNullOrWhiteSpace(request.VariantValue) ? item.VariantValue : request.VariantValue.Trim();
        item.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        item.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        item.PriceOverride = request.PriceOverride;
        item.ImageId = request.ImageId;
        item.SortOrder = request.SortOrder ?? item.SortOrder;
        item.IsActive = request.IsActive;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return new ProductItemOperation(
                ProductItemOutcome.Conflict,
                await ConflictMessageForAsync(postgres, request.Code, request.Barcode, cancellationToken));
        }

        await AuditAsync(AuditAction.Update, actingUserId, actingEmail, item,
            $"Modificación de la variante «{item.VariantValue ?? "(única)"}».", cancellationToken);

        if (isDeactivating)
        {
            await events.PublishAsync(new VarianteDesactivada(item.Id, item.ProductId, clock.GetUtcNow()), cancellationToken);
        }

        var listPrice = await database.Products.Where(p => p.Id == item.ProductId).Select(p => p.ListPrice).FirstAsync(cancellationToken);
        return new ProductItemOperation(ProductItemOutcome.Ok, Item: Project(item, listPrice));
    }

    /// <summary>
    /// Desactiva una variante. Bloquea si es la última activa de un producto
    /// activo (regla 8): propone desactivar el producto, no un error genérico.
    /// </summary>
    public async Task<ProductItemOperation> DeactivateAsync(
        Guid itemId,
        int actingUserId,
        string actingEmail,
        CancellationToken cancellationToken)
    {
        var item = await database.ProductItems.FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);
        if (item is null)
        {
            return new ProductItemOperation(ProductItemOutcome.NotFound);
        }

        var listPrice = await database.Products.Where(p => p.Id == item.ProductId).Select(p => p.ListPrice).FirstAsync(cancellationToken);

        if (!item.IsActive)
        {
            return new ProductItemOperation(ProductItemOutcome.Ok, Item: Project(item, listPrice));
        }

        var blocked = await BlockedDeactivationReasonAsync(item, cancellationToken);
        if (blocked is not null)
        {
            return new ProductItemOperation(ProductItemOutcome.Conflict, blocked);
        }

        item.IsActive = false;
        await database.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditAction.Delete, actingUserId, actingEmail, item,
            $"Baja de la variante «{item.VariantValue ?? "(única)"}».", cancellationToken);

        await events.PublishAsync(new VarianteDesactivada(item.Id, item.ProductId, clock.GetUtcNow()), cancellationToken);

        return new ProductItemOperation(ProductItemOutcome.Ok, Item: Project(item, listPrice));
    }

    /// <summary>Resolución exacta por código o código de barras, para la caja.</summary>
    public async Task<ItemLookupResponse?> LookupAsync(string code, CancellationToken cancellationToken)
    {
        var found = await database.ProductItems
            .AsNoTracking()
            .Where(item => item.IsActive && item.Product!.IsActive && (item.Code == code || item.Barcode == code))
            .Select(item => new { Item = item, item.Product!.Name, item.Product.Slug, item.Product.ListPrice })
            .FirstOrDefaultAsync(cancellationToken);

        return found is null ? null : new ItemLookupResponse(Project(found.Item, found.ListPrice), found.Name, found.Slug);
    }

    /// <summary>
    /// El motivo por el que no se puede desactivar, o <c>null</c> si se puede.
    /// </summary>
    /// <remarks>
    /// Cuenta las variantes activas del producto y comprueba si el producto
    /// está activo; la decisión y el mensaje viven en
    /// <see cref="ProductItemRules.DeactivationBlockedReason"/>, pura y
    /// probada sin base de datos.
    /// </remarks>
    private async Task<string?> BlockedDeactivationReasonAsync(ProductItem item, CancellationToken cancellationToken)
    {
        var product = await database.Products
            .Where(p => p.Id == item.ProductId)
            .Select(p => new { p.IsActive })
            .FirstAsync(cancellationToken);

        if (!product.IsActive)
        {
            return null;
        }

        var otherActiveVariants = await database.ProductItems
            .CountAsync(candidate => candidate.ProductId == item.ProductId && candidate.Id != item.Id && candidate.IsActive, cancellationToken);

        return ProductItemRules.DeactivationBlockedReason(isLastActiveVariantOfActiveProduct: otherActiveVariants == 0);
    }

    private string? ValidateImage(Guid? imageId)
        => imageId is null || media.GetPublicUrl(imageId.Value) is not null
            ? null
            : "La imagen indicada no existe o no está activa.";

    /// <summary>Redacta el conflicto nombrando **con qué producto** se choca.</summary>
    /// <remarks>
    /// El código es único en toda la instalación, así que el choque casi
    /// siempre es con **otro** producto — y decir «ya existe» sin decir dónde
    /// deja a quien lo escribió buscando a ciegas entre todo el catálogo. La
    /// consulta extra solo ocurre en el camino del conflicto, que es raro.
    /// </remarks>
    private async Task<string> ConflictMessageForAsync(
        PostgresException exception,
        string? code,
        string? barcode,
        CancellationToken cancellationToken)
    {
        async Task<string?> DuenoDe(string? valor, bool porCodigo)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return null;
            }

            return await database.ProductItems
                .AsNoTracking()
                .Where(item => porCodigo ? item.Code == valor : item.Barcode == valor)
                .Select(item => item.Product!.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        switch (exception.ConstraintName)
        {
            case "uq_product_items_code":
            {
                var dueño = await DuenoDe(code, porCodigo: true);
                return dueño is null
                    ? $"El código «{code}» ya está en uso en esta instalación."
                    : $"El código «{code}» ya lo usa «{dueño}». Los códigos son únicos en toda la instalación.";
            }

            case "uq_product_items_barcode":
            {
                var dueño = await DuenoDe(barcode, porCodigo: false);
                return dueño is null
                    ? $"El código de barras «{barcode}» ya está en uso en esta instalación."
                    : $"El código de barras «{barcode}» ya lo usa «{dueño}». Son únicos en toda la instalación.";
            }

            case "uq_product_items_valor":
                return "Este producto ya tiene una presentación con ese mismo valor.";

            default:
                return "Ya existe algo con ese mismo dato único.";
        }
    }

    private Task AuditAsync(
        string action,
        int actingUserId,
        string actingEmail,
        ProductItem affected,
        string summary,
        CancellationToken cancellationToken)
        => audit.WriteAsync(
            new AuditEntry(action)
            {
                AdminUserId = actingUserId,
                AdminUserEmail = actingEmail,
                ModuleCode = CatalogModule.ModuleCode,
                EntityType = "product_item",
                EntityId = affected.Id.ToString(),
                Summary = summary
            },
            cancellationToken);

    private static ProductItemOperation Invalid(string error) => new(ProductItemOutcome.Invalid, error);

    private ProductItemResponse Project(ProductItem item, decimal? listPrice) => new(
        item.Id,
        item.ProductId,
        item.VariantValue,
        item.Code,
        item.Barcode,
        item.PriceOverride,
        ItemPricing.Effective(item.PriceOverride, listPrice),
        item.ImageId,
        item.ImageId is { } imageId ? media.GetPublicUrl(imageId) : null,
        item.SortOrder,
        item.IsActive);
}
