namespace Sillar.Modules.Catalog.Services;

/// <summary>
/// Operaciones sobre el árbol de categorías que no necesitan la base: se les
/// da el mapa de padres ya leído y razonan en memoria.
/// </summary>
public static class CategoryTree
{
    /// <summary>
    /// Si asignar <paramref name="candidateParentId"/> como padre de
    /// <paramref name="categoryId"/> formaría un ciclo (SPEC regla 10).
    /// </summary>
    /// <remarks>
    /// El <c>CHECK</c> de la base solo impide el caso directo —ser padre de sí
    /// misma— porque una restricción no puede recorrer la tabla. El ciclo
    /// largo —A es padre de B, B es padre de A— lo comprueba la aplicación,
    /// subiendo desde el candidato hasta la raíz o hasta encontrar
    /// <paramref name="categoryId"/> en el camino.
    /// </remarks>
    /// <param name="parentById">Padre de cada categoría, tal como está hoy en la base.</param>
    /// <param name="categoryId">Categoría que se está editando.</param>
    /// <param name="candidateParentId">Padre que se le quiere asignar.</param>
    public static bool CreatesCycle(
        IReadOnlyDictionary<Guid, Guid?> parentById,
        Guid categoryId,
        Guid? candidateParentId)
    {
        var current = candidateParentId;
        var visited = 0;

        // El tope de visitas no es una regla de negocio: es la defensa contra
        // un mapa ya inconsistente (una fila editada a mano) que formara un
        // bucle antes de llegar nunca a categoryId. Sin él, ese caso cuelga en
        // vez de responder "sí, es un ciclo".
        while (current is { } id && visited <= parentById.Count)
        {
            if (id == categoryId)
            {
                return true;
            }

            parentById.TryGetValue(id, out current);
            visited++;
        }

        return false;
    }
}
