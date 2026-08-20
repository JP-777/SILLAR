namespace Sillar.Modules.Catalog.Dtos;

/// <summary>Un eslabón de la miga de pan, de la raíz hacia la categoría mostrada.</summary>
/// <param name="Slug">Para el enlace.</param>
/// <param name="Name">Para el texto.</param>
public sealed record BreadcrumbItemResponse(string Slug, string Name);
