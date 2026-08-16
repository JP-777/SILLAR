using Sillar.Shared.Replication;

namespace Sillar.Modules.Catalog.Domain;

/// <summary>
/// Un producto en una categoría.
/// </summary>
/// <remarks>
/// N:M desde el principio porque hay segundo y tercer caso: los conos son
/// deporte y también juguete, una calculadora es tecnología y también material
/// del curso de matemáticas. En una librería esto no es la excepción, es la
/// mitad del catálogo (SPEC §4.1).
///
/// <b>Clave compuesta y sin <c>id</c> propio.</b> Dos <c>uuid</c> ya son
/// globalmente únicos, así que un tercero no aportaría nada. Sí lleva las cuatro
/// columnas de replicación: asociar un producto a una categoría es una fila que
/// puede nacer en cualquier nodo, y sin marca de versión M16 no podría
/// distinguir una asociación borrada de una que nunca llegó.
/// </remarks>
public class ProductCategory : IReplicatedEntity
{
    /// <summary>Producto.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Categoría.</summary>
    public Guid CategoryId { get; set; }

    /// <inheritdoc />
    public string OriginNode { get; set; } = string.Empty;

    /// <inheritdoc />
    public long RowVersion { get; set; } = 1;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Producto.</summary>
    public Product? Product { get; set; }

    /// <summary>Categoría.</summary>
    public Category? Category { get; set; }
}
