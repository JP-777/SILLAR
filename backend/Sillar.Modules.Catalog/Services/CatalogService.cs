using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Modules.Catalog.Data;
using Sillar.Modules.Catalog.Domain;

namespace Sillar.Modules.Catalog.Services;

/// <summary>
/// Implementación de <see cref="ICatalogService"/>: lo que M03, M09, M13 y
/// M15 ven de M01, sin conocer su schema (SPEC §7).
/// </summary>
internal sealed class CatalogService(CatalogDbContext database) : ICatalogService
{
    /// <inheritdoc />
    /// <remarks>
    /// Sin filtrar por activo a propósito: es la consulta «qué es esto», que
    /// un pedido antiguo necesita aunque la variante ya se haya dado de baja.
    /// Comprobar si sigue vendible es <see cref="ItemExisteYEstaActivoAsync"/>.
    /// </remarks>
    public async Task<ItemSnapshot?> ObtenerItemAsync(Guid itemId, CancellationToken ct)
        => await Rows().Where(row => row.Item.Id == itemId).Select(ToSnapshot).FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<ItemSnapshot?> BuscarPorCodigoAsync(string codigo, CancellationToken ct)
        => await Rows()
            .Where(row => row.Item.IsActive && row.Product.IsActive && (row.Item.Code == codigo || row.Item.Barcode == codigo))
            .Select(ToSnapshot)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// Sobre el mismo índice de texto completo que la búsqueda pública de
    /// productos: <c>to_tsvector('spanish', ...)</c>, no <c>LIKE</c> — el
    /// nombre lleva colación no determinista (DATOS.md §4).
    /// </remarks>
    public async Task<IReadOnlyList<ItemSnapshot>> BuscarAsync(string texto, int limite, CancellationToken ct)
        => await Rows()
            .Where(row => row.Item.IsActive && row.Product.IsActive
                && EF.Functions.ToTsVector("spanish", row.Product.Name).Matches(EF.Functions.PlainToTsQuery("spanish", texto)))
            .OrderBy(row => row.Product.Name)
            .Take(Math.Max(limite, 0))
            .Select(ToSnapshot)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ItemSnapshot>> VariantesDeAsync(Guid productId, CancellationToken ct)
        => await Rows()
            .Where(row => row.Item.ProductId == productId && row.Item.IsActive)
            .OrderBy(row => row.Item.SortOrder)
            .Select(ToSnapshot)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<bool> ItemExisteYEstaActivoAsync(Guid itemId, CancellationToken ct)
        => database.ProductItems.AnyAsync(item => item.Id == itemId && item.IsActive, ct);

    /// <summary>Una variante junto con su producto, sin proyectar todavía.</summary>
    private sealed record Row(ProductItem Item, Product Product);

    private IQueryable<Row> Rows()
        => database.ProductItems.AsNoTracking()
            .Join(
                database.Products.AsNoTracking(),
                item => item.ProductId,
                product => product.Id,
                (item, product) => new Row(item, product));

    private static readonly Expression<Func<Row, ItemSnapshot>> ToSnapshot = row => new ItemSnapshot(
        row.Item.Id,
        row.Product.Id,
        row.Product.Name,
        row.Item.VariantValue,
        row.Item.Code,
        row.Item.Barcode,
        row.Item.PriceOverride ?? row.Product.ListPrice,
        row.Product.SaleUnit);
}
