namespace Sillar.Shared.Paging;

/// <summary>Una página de resultados con lo necesario para paginar en la interfaz.</summary>
/// <typeparam name="T">Tipo de los elementos.</typeparam>
/// <param name="Items">Elementos de esta página.</param>
/// <param name="Page">Número de página, empezando en 1.</param>
/// <param name="PageSize">Elementos por página.</param>
/// <param name="TotalItems">Total de elementos que cumplen el filtro.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalItems)
{
    /// <summary>Número de páginas que ocupa el total.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)((TotalItems + PageSize - 1) / PageSize);

    /// <summary>Indica si hay página siguiente.</summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>Construye una página a partir de la petición que la originó.</summary>
    public static PagedResult<T> From(PageRequest request, IReadOnlyList<T> items, long totalItems)
        => new(items, request.Number, request.Size, totalItems);
}
