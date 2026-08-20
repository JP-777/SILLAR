namespace Sillar.Modules.Catalog.Contracts;

/// <summary>
/// Lo que se vende y lo que se cuenta. Congelado en el momento de la operación:
/// el pedido y la venta guardan esto, no una referencia viva.
/// </summary>
/// <param name="ItemId">
/// Identificador de la variante, no del producto: quien vende, cuenta o
/// factura lo hace contra ella (SPEC §4.2 y §7).
/// </param>
/// <param name="ProductId">
/// Del producto al que pertenece. Solo para agrupar en informes: nada vende ni
/// cuenta contra él.
/// </param>
/// <param name="ProductName">Nombre del producto.</param>
/// <param name="VariantValue">
/// Lo que distingue a la variante, por ejemplo <c>"Verde"</c>. Nulo si el
/// producto no tiene más que su variante única.
/// </param>
/// <param name="Code">Código visible del negocio, si tiene.</param>
/// <param name="Barcode">Código de barras, si tiene.</param>
/// <param name="Price">
/// Ya resuelto: <c>price_override ?? list_price</c>. Nulo significa «consultar
/// precio»; nunca se confunde con cero, que es gratis.
/// </param>
/// <param name="SaleUnit">Unidad de venta, texto libre.</param>
public sealed record ItemSnapshot(
    Guid ItemId,
    Guid ProductId,
    string ProductName,
    string? VariantValue,
    string? Code,
    string? Barcode,
    decimal? Price,
    string? SaleUnit);

/// <summary>
/// Lo que se vende y lo que se cuenta. Lo implementa M01 y lo usan sus
/// dependientes (M03, M09, M13, M15) sin ver su schema.
/// </summary>
public interface ICatalogService
{
    /// <summary>Una variante por su identificador.</summary>
    Task<ItemSnapshot?> ObtenerItemAsync(Guid itemId, CancellationToken ct);

    /// <summary>Código exacto o código de barras. Lo que usa la caja con la lectora.</summary>
    Task<ItemSnapshot?> BuscarPorCodigoAsync(string codigo, CancellationToken ct);

    /// <summary>Texto libre, sin distinguir mayúsculas ni tildes. Devuelve variantes.</summary>
    Task<IReadOnlyList<ItemSnapshot>> BuscarAsync(string texto, int limite, CancellationToken ct);

    /// <summary>Las variantes de un producto. Para elegir color en pantalla.</summary>
    Task<IReadOnlyList<ItemSnapshot>> VariantesDeAsync(Guid productId, CancellationToken ct);

    /// <summary>Si la variante existe y está activa. Para validar antes de vender o contar.</summary>
    Task<bool> ItemExisteYEstaActivoAsync(Guid itemId, CancellationToken ct);
}
