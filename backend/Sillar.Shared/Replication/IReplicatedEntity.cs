namespace Sillar.Shared.Replication;

/// <summary>
/// Una fila que puede nacer en un nodo y tener que existir en otro.
/// </summary>
/// <remarks>
/// Es la pregunta de la ADR-016 convertida en tipo: si la respuesta es sí, la
/// entidad implementa esto y su clave primaria es <c>uuid</c> v7 generado por la
/// aplicación.
///
/// Las cuatro propiedades las rellena el <c>DbContext</c> al guardar, no quien
/// escribe la entidad. Dejarlo a cada llamada garantiza que alguna se olvide.
/// </remarks>
public interface IReplicatedEntity
{
    /// <summary>Nodo donde nació la fila.</summary>
    string OriginNode { get; set; }

    /// <summary>
    /// Marca de versión, que sube con cada modificación.
    /// </summary>
    /// <remarks>
    /// M16 la usará para ordenar cambios que llegan de sitios distintos. Sin
    /// ella, dos ediciones de la misma fila son indistinguibles.
    /// </remarks>
    long RowVersion { get; set; }

    /// <summary>Fecha de alta.</summary>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>Fecha de la última modificación. La escribe un trigger.</summary>
    DateTimeOffset UpdatedAt { get; set; }
}
