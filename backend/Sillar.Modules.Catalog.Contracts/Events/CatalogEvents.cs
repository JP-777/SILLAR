namespace Sillar.Modules.Catalog.Contracts.Events;

/// <summary>Se creó un producto, con su variante única.</summary>
/// <param name="ProductId">Producto creado.</param>
/// <param name="OccurredAt">Cuándo ocurrió.</param>
public sealed record ProductoCreado(Guid ProductId, DateTimeOffset OccurredAt);

/// <summary>
/// Cambió algo que altera <b>lo que M01 publica</b> de este producto.
/// </summary>
/// <remarks>
/// <para>
/// No es «se editó la fila del producto». Se emite también cuando cambian sus
/// presentaciones o sus categorías, porque desde fuera <b>eso es cambiar el
/// producto</b>: a quien consume el catálogo no le importa la estructura
/// interna, le importa que lo que enseña dejó de ser cierto.
/// </para>
/// <para>
/// La regla que lo gobierna, y que vale para cualquier consumidor que guarde
/// un snapshot: <b>un valor derivado cambia cuando cambia su entrada, no
/// cuando cambia la fila.</b>
/// </para>
/// <para>
/// <b>Puede llegar varias veces por una sola acción.</b> Guardar la tabla de
/// presentaciones desde el panel llama a varios endpoints seguidos —crea,
/// actualiza y desactiva—, así que una pulsación de «Guardar» emite uno por
/// cada uno. <b>Un handler tiene que ser idempotente</b>, y el número de
/// eventos no significa nada: no se cuentan, se aplican.
/// </para>
/// <para>
/// <b>Lo que no cubre:</b> desactivar una categoría no emite uno por cada
/// producto afectado. Para eso está <see cref="CategoriaDesactivada"/>, que es
/// un evento por acción en vez de una ráfaga proporcional al tamaño de la
/// categoría — y el conjunto afectado es prácticamente todos sus productos,
/// no unos pocos.
/// </para>
/// </remarks>
/// <param name="ProductId">Producto cuyo contenido publicado cambió.</param>
/// <param name="OccurredAt">Cuándo ocurrió.</param>
public sealed record ProductoActualizado(Guid ProductId, DateTimeOffset OccurredAt);

/// <summary>Se desactivó un producto. Baja lógica: sigue existiendo.</summary>
/// <param name="ProductId">Producto desactivado.</param>
/// <param name="OccurredAt">Cuándo ocurrió.</param>
public sealed record ProductoDesactivado(Guid ProductId, DateTimeOffset OccurredAt);

/// <summary>Se creó una variante de un producto, la segunda o siguiente.</summary>
/// <param name="ItemId">Variante creada.</param>
/// <param name="ProductId">Producto al que pertenece.</param>
/// <param name="OccurredAt">Cuándo ocurrió.</param>
public sealed record VarianteCreada(Guid ItemId, Guid ProductId, DateTimeOffset OccurredAt);

/// <summary>Se desactivó una variante. Baja lógica: sigue existiendo.</summary>
/// <param name="ItemId">Variante desactivada.</param>
/// <param name="ProductId">Producto al que pertenece.</param>
/// <param name="OccurredAt">Cuándo ocurrió.</param>
public sealed record VarianteDesactivada(Guid ItemId, Guid ProductId, DateTimeOffset OccurredAt);

/// <summary>
/// Se desactivó una categoría. No actúa en cascada: sus productos siguen
/// activos (SPEC regla 9).
/// </summary>
/// <param name="CategoryId">Categoría desactivada.</param>
/// <param name="OccurredAt">Cuándo ocurrió.</param>
public sealed record CategoriaDesactivada(Guid CategoryId, DateTimeOffset OccurredAt);
