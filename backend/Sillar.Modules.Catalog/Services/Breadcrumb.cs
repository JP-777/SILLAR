namespace Sillar.Modules.Catalog.Services;

/// <summary>
/// Elige y arma la miga de pan de un producto. No está en el SPEC: es el caso
/// que no cubría — una categoría principal desactivada no puede seguir dando
/// la ruta pública, porque nada enlaza a algo invisible.
/// </summary>
public static class Breadcrumb
{
    /// <summary>Una categoría, con lo que hace falta para decidir la miga de pan.</summary>
    /// <param name="Id">Identificador.</param>
    /// <param name="Slug">Para el enlace.</param>
    /// <param name="Name">Para el texto.</param>
    /// <param name="ParentId">Categoría padre, si tiene.</param>
    /// <param name="IsActive">Si sigue visible en el catálogo público.</param>
    public sealed record CategoryNode(Guid Id, string Slug, string Name, Guid? ParentId, bool IsActive);

    /// <summary>
    /// Qué categoría del producto da la miga de pan.
    /// </summary>
    /// <remarks>
    /// La principal, si está activa. Si no, la primera activa entre las demás
    /// categorías del producto, por nombre — un criterio estable, no el orden
    /// de llegada. Si ninguna lo está, <c>null</c>: sin miga, nunca un enlace a
    /// algo invisible.
    /// </remarks>
    /// <param name="primary">Categoría principal del producto, o <c>null</c> si no tiene.</param>
    /// <param name="others">Las demás categorías del producto.</param>
    public static CategoryNode? ChooseTarget(CategoryNode? primary, IReadOnlyList<CategoryNode> others)
    {
        if (primary is { IsActive: true })
        {
            return primary;
        }

        return others
            .Where(category => category.IsActive)
            .OrderBy(category => category.Name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// Construye la ruta desde la raíz hasta <paramref name="target"/>.
    /// </summary>
    /// <remarks>
    /// Corta en el primer antecesor inactivo, sin incluirlo: el mismo
    /// principio de <see cref="ChooseTarget"/> aplicado un nivel más arriba. Si
    /// el propio <paramref name="target"/> ya llega inactivo, la ruta sale
    /// vacía — no debería ocurrir, porque <see cref="ChooseTarget"/> ya filtró,
    /// pero esta función no confía en eso por sí sola.
    /// </remarks>
    /// <param name="target">Categoría elegida por <see cref="ChooseTarget"/>.</param>
    /// <param name="byId">Todas las categorías relevantes, indexadas por id.</param>
    public static IReadOnlyList<CategoryNode> BuildTrail(
        CategoryNode target,
        IReadOnlyDictionary<Guid, CategoryNode> byId)
    {
        var trail = new List<CategoryNode>();
        var current = target;

        while (current.IsActive)
        {
            trail.Insert(0, current);

            if (current.ParentId is not { } parentId || !byId.TryGetValue(parentId, out var parent))
            {
                break;
            }

            current = parent;
        }

        return trail;
    }
}
