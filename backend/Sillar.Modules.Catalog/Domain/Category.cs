namespace Sillar.Modules.Catalog.Domain;

/// <summary>Categoría del catálogo. Puede tener padre y forma un árbol.</summary>
public class Category : CatalogEntity
{
    /// <summary>Categoría padre. Nulo en las de primer nivel.</summary>
    /// <remarks>
    /// Una categoría no puede ser su propia ancestra (regla 10). El
    /// <c>CHECK</c> solo impide el caso directo —ser padre de sí misma—; el
    /// ciclo largo lo comprueba la aplicación, porque una restricción que
    /// recorre el árbol exige un disparador recursivo.
    /// </remarks>
    public Guid? ParentId { get; set; }

    /// <summary>Nombre visible. Colación <c>es_search</c>: se busca por él.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Para la URL pública. Colación <c>es_ci</c>: la unicidad ignora mayúsculas
    /// y respeta tildes.
    /// </summary>
    public required string Slug { get; set; }

    /// <summary>Texto de cabecera de la categoría.</summary>
    public string? Description { get; set; }

    /// <summary>Portada, en <c>core.media_assets</c>. El archivo lo gestiona CORE.</summary>
    public Guid? ImageId { get; set; }

    /// <summary>Orden de presentación.</summary>
    public int SortOrder { get; set; }

    /// <summary>Baja lógica.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Categoría padre.</summary>
    public Category? Parent { get; set; }

    /// <summary>Categorías hijas.</summary>
    public ICollection<Category> Children { get; set; } = [];

    /// <summary>Productos asociados.</summary>
    public ICollection<ProductCategory> Products { get; set; } = [];
}
