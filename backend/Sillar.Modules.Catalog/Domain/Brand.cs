namespace Sillar.Modules.Catalog.Domain;

/// <summary>
/// Marca o fabricante.
/// </summary>
/// <remarks>
/// Existe para <b>filtrar</b>. El nombre del producto ya lleva la marca por la
/// convención de nomenclatura, así que esta tabla no la repite: la hace
/// seleccionable.
///
/// Un restaurante no usa marcas y la tabla se queda vacía. Es correcto: una
/// tabla vacía no estorba, un campo obligatorio sí.
/// </remarks>
public class Brand : CatalogEntity
{
    /// <summary>
    /// Nombre. Colación <c>es_ci</c>: <c>Artesco</c> y <c>ARTESCO</c> son la
    /// misma marca; <c>Peña</c> y <c>Pena</c> no.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>Para la URL pública.</summary>
    public required string Slug { get; set; }

    /// <summary>Logotipo, en <c>core.media_assets</c>. El archivo lo gestiona CORE.</summary>
    public Guid? LogoId { get; set; }

    /// <summary>Baja lógica.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Productos de esta marca.</summary>
    public ICollection<Product> Products { get; set; } = [];
}
