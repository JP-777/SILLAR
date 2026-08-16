using Sillar.Shared.Replication;

namespace Sillar.Modules.Catalog.Domain;

/// <summary>
/// Base de las entidades del catálogo.
/// </summary>
/// <remarks>
/// Todas las tablas de <c>catalog</c> se replican (ADR-017: el catálogo es dato
/// compartido entre el ERP y la web), así que todas llevan clave <c>uuid</c> v7
/// generada por la aplicación y las cuatro columnas de la ADR-016 regla 4.
///
/// <b>La clave se genera en el constructor</b>, no en la base de datos. Es la
/// regla 1 de la ADR-016: un nodo sin conexión tiene que poder crear la fila
/// entera antes de hablar con nadie.
///
/// v7 y no v4 porque lleva la marca de tiempo delante, así que se inserta al
/// final del índice como un entero incremental en vez de caer en una página al
/// azar.
/// </remarks>
public abstract class CatalogEntity : IReplicatedEntity
{
    /// <summary>
    /// Identificador. <b>Nunca se muestra al usuario</b> (ADR-016, regla 2).
    /// </summary>
    /// <remarks>
    /// Fuera se usa el slug; dentro, el código del negocio. Nadie lee un
    /// <c>uuid</c> en voz alta por teléfono.
    /// </remarks>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public string OriginNode { get; set; } = string.Empty;

    /// <inheritdoc />
    public long RowVersion { get; set; } = 1;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
