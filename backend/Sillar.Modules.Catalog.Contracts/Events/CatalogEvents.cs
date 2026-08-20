namespace Sillar.Modules.Catalog.Contracts.Events;

/// <summary>Se creó un producto, con su variante única.</summary>
/// <param name="ProductId">Producto creado.</param>
/// <param name="OccurredAt">Cuándo ocurrió.</param>
public sealed record ProductoCreado(Guid ProductId, DateTimeOffset OccurredAt);

/// <summary>Se modificó un producto.</summary>
/// <param name="ProductId">Producto modificado.</param>
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
