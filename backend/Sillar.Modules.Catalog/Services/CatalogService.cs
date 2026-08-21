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
    /// <summary>
    /// Tope de resultados de <see cref="BuscarParaSeleccionAsync"/>.
    /// </summary>
    /// <remarks>
    /// <b>Un límite que llega de fuera se acota, no se obedece</b>, igual que
    /// el <c>pageSize</c> de los listados. Es un selector para elegir un puñado
    /// de productos: pedir quinientos no es un caso de uso, es un descuido o un
    /// abuso, y como es una lectura no se pierde nada al recortar.
    /// </remarks>
    private const int TopeSeleccion = 50;

    /// <summary>
    /// Acota el límite pedido al de la casa.
    /// </summary>
    /// <remarks>
    /// Está aparte del método que consulta para que <b>lo pueda afirmar una
    /// prueba</b>: el tope superior no se puede comprobar contra la base de
    /// demostración, que tiene veinte productos y nunca llegaría a cincuenta.
    /// Una regla que solo se comprueba cuando los datos dan la casualidad de
    /// alcanzarla no está comprobada.
    /// </remarks>
    /// <param name="limite">Lo que pidió quien llama.</param>
    /// <returns>Entre 1 y <see cref="TopeSeleccion"/>.</returns>
    internal static int AcotarSeleccion(int limite) => Math.Clamp(limite, 1, TopeSeleccion);

    private sealed record Row(ProductItem Item, Product Product);

    private IQueryable<Row> Rows()
        => database.ProductItems.AsNoTracking()
            .Join(
                database.Products.AsNoTracking(),
                item => item.ProductId,
                product => product.Id,
                (item, product) => new Row(item, product));

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductPickerItem>> BuscarParaSeleccionAsync(
        string texto,
        int limite,
        CancellationToken ct)
    {
        var cuantos = AcotarSeleccion(limite);

        return await SeleccionAsync(
            database.Products.AsNoTracking()
                .Where(product => product.IsActive
                    && EF.Functions.ToTsVector("spanish", product.Name)
                        .Matches(EF.Functions.PlainToTsQuery("spanish", texto)))
                .OrderBy(product => product.Name)
                .Take(cuantos),
            ct);
    }

    /// <inheritdoc />
    public async Task<ProductPickerItem?> ObtenerParaSeleccionAsync(Guid productId, CancellationToken ct)
    {
        // **Aquí no se filtra por activo, y en la búsqueda sí.** Es deliberado
        // y por eso está escrito: no se puede *elegir* un producto de baja,
        // pero quien ya lo eligió necesita distinguir «lo dieron de baja» de
        // «ya no existe» — dos estados con dos respuestas distintas en su
        // panel. La baja vuelve marcada con `IsActive` en falso; `null` queda
        // para lo que de verdad no está.
        var encontrado = await SeleccionAsync(
            database.Products.AsNoTracking().Where(product => product.Id == productId),
            ct);

        return encontrado.Count == 0 ? null : encontrado[0];
    }

    /// <summary>
    /// Proyecta productos a <see cref="ProductPickerItem"/>.
    /// </summary>
    /// <remarks>
    /// <b>Una sola composición para buscar y para releer.</b> Si cada uno
    /// armara su resultado, el día que cambie cómo se elige la categoría o
    /// cómo se resuelve el precio, uno de los dos se quedaría atrás — y el que
    /// se queda atrás es siempre el que está más lejos de los casos reales.
    ///
    /// <b>El filtro de activo vive en cada llamador, no aquí.</b> Buscar
    /// devuelve solo altas —no se elige lo que está de baja— y releer devuelve
    /// también las bajas, marcadas. La asimetría es intencionada; tenerla en
    /// los llamadores es lo que la deja a la vista de quien lee cada uno.
    ///
    /// Se traen los datos crudos y la categoría efectiva se resuelve fuera:
    /// `ChooseTarget` es la regla del módulo y **se aplica una sola vez, en un
    /// sitio** (`Breadcrumb.cs:29`). Reescribirla como SQL daría una segunda
    /// versión de la misma regla.
    /// </remarks>
    private async Task<IReadOnlyList<ProductPickerItem>> SeleccionAsync(
        IQueryable<Product> productos,
        CancellationToken ct)
    {
        var filas = await productos
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.Slug,
                product.IsPublic,
                product.IsActive,
                product.ListPrice,
                product.PrimaryCategoryId,
                PrimaryImageId = product.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => (Guid?)image.MediaAssetId)
                    .FirstOrDefault(),
                // El precio de la tarjeta sale de las presentaciones activas,
                // no del `list_price` a secas: con una que cueste distinto, el
                // de lista es un número que no se cobra.
                PriceOverrides = product.Items
                    .Where(item => item.IsActive)
                    .Select(item => item.PriceOverride)
                    .ToList(),
                Categorias = product.Categories
                    .Select(link => new Breadcrumb.CategoryNode(
                        link.Category!.Id,
                        link.Category.Slug,
                        link.Category.Name,
                        link.Category.ParentId,
                        link.Category.IsActive))
                    .ToList()
            })
            .ToListAsync(ct);

        return
        [
            .. filas.Select(fila =>
            {
                var (price, varies) = ItemPricing.ForCard(fila.PriceOverrides, fila.ListPrice);

                var primary = fila.Categorias.FirstOrDefault(c => c.Id == fila.PrimaryCategoryId);
                var others = fila.Categorias.Where(c => c.Id != fila.PrimaryCategoryId).ToList();

                return new ProductPickerItem(
                    fila.Id,
                    fila.Name,
                    fila.Slug,
                    fila.PrimaryImageId,
                    Breadcrumb.ChooseTarget(primary, others)?.Name,
                    price,
                    varies,
                    fila.IsPublic,
                    fila.IsActive);
            })
        ];
    }

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
