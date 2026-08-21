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

    /// <summary>
    /// Busca <b>productos</b> para elegirlos desde otro módulo y quedarse con
    /// sus datos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Devuelve productos, no presentaciones: aquí se destaca un producto, y
    /// <see cref="BuscarAsync"/> daría tres filas del mismo plumón.
    /// </para>
    /// <para>
    /// Solo productos <b>activos</b>, publicados o no —
    /// <see cref="ProductPickerItem.IsPublic"/> dice cuál es cuál. Un producto
    /// dado de baja no se puede elegir: ya no es parte del catálogo.
    /// </para>
    /// <para>
    /// <b>Aquí sí se filtran las bajas y en
    /// <see cref="ObtenerParaSeleccionAsync"/> no.</b> Es deliberado: elegir y
    /// releer no son la misma pregunta. Quien ya eligió un producto necesita
    /// saber si lo dieron de baja, y quien está eligiendo no debe verlo.
    /// </para>
    /// </remarks>
    /// <param name="texto">
    /// Texto libre, sin distinguir mayúsculas ni tildes: <c>lapiz</c> encuentra
    /// <c>LÁPIZ</c>.
    /// </param>
    /// <param name="limite">
    /// Cuántos como mucho. <b>Se acota, no se obedece</b>: un valor que llega
    /// de fuera se recorta al tope del módulo y uno menor que 1 sube a 1. Es
    /// una lectura para elegir un puñado de cosas, así que recortar no pierde
    /// nada y devolver un error obligaría a quien llama a conocer un número que
    /// no le importa.
    /// </param>
    /// <param name="ct">Cancelación.</param>
    Task<IReadOnlyList<ProductPickerItem>> BuscarParaSeleccionAsync(string texto, int limite, CancellationToken ct);

    /// <summary>
    /// Los datos de selección de <b>un</b> producto, para volver a leerlos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es el par de <see cref="BuscarParaSeleccionAsync"/>: aquél sirve para
    /// elegir, éste para <b>refrescar lo elegido</b> cuando llega un
    /// <c>ProductoActualizado</c>. Sin él, un consumidor que guarde un snapshot
    /// solo puede releer buscando por texto, que no es una identidad.
    /// </para>
    /// <para>
    /// <b>Devuelve las bajas también</b>, con
    /// <see cref="ProductPickerItem.IsActive"/> en falso — al revés que
    /// <see cref="BuscarParaSeleccionAsync"/>, que las esconde. No es un
    /// descuido: son dos preguntas distintas, y quien ya eligió necesita
    /// distinguir «lo dieron de baja, puede volver» de «ya no existe».
    /// </para>
    /// <para>
    /// <b><c>null</c> significa entonces una sola cosa: el producto no está.</b>
    /// Y como en SILLAR no hay borrado físico, eso solo ocurre si se desinstaló
    /// el módulo y se volvió a instalar. Es una respuesta, no un error: le dice
    /// a su dueño que hay que volver a elegir.
    /// </para>
    /// </remarks>
    /// <param name="productId">El producto, tal como lo guardó quien lo eligió.</param>
    /// <param name="ct">Cancelación.</param>
    Task<ProductPickerItem?> ObtenerParaSeleccionAsync(Guid productId, CancellationToken ct);
}
