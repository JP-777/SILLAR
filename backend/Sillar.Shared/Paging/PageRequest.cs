namespace Sillar.Shared.Paging;

/// <summary>Página pedida por el cliente, ya saneada.</summary>
/// <remarks>
/// Se construye siempre con <see cref="Of"/>, que recorta los valores fuera de
/// rango en lugar de rechazarlos. Un cliente que pida la página cero o mil
/// elementos recibe la primera página y el máximo, no un error: son datos de
/// presentación, no una operación que pueda salir mal.
/// </remarks>
public sealed record PageRequest
{
    /// <summary>Tamaño de página cuando el cliente no pide ninguno.</summary>
    public const int DefaultSize = 50;

    /// <summary>Tope de tamaño de página.</summary>
    /// <remarks>
    /// Sin él, la primera consulta de una instalación con dos años de auditoría
    /// vuelca la tabla entera.
    /// </remarks>
    public const int MaxSize = 200;

    private PageRequest(int number, int size)
    {
        Number = number;
        Size = size;
    }

    /// <summary>Número de página, empezando en 1.</summary>
    public int Number { get; }

    /// <summary>Elementos por página.</summary>
    public int Size { get; }

    /// <summary>Elementos que hay que saltar para llegar a esta página.</summary>
    public int Skip => (Number - 1) * Size;

    /// <summary>Construye una página saneando lo que llegue.</summary>
    /// <param name="number">Número pedido. Por debajo de 1 se trata como 1.</param>
    /// <param name="size">Tamaño pedido. Fuera de rango se recorta.</param>
    public static PageRequest Of(int? number, int? size) => new(
        Math.Max(number ?? 1, 1),
        Math.Clamp(size ?? DefaultSize, 1, MaxSize));
}
