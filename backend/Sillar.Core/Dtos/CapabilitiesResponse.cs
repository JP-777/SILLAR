namespace Sillar.Core.Dtos;

/// <summary>
/// Qué puede hacer esta instalación: producto, versión y módulos activos.
/// </summary>
/// <remarks>
/// Es el contrato con el frontend: el menú, las secciones de la home y los
/// enlaces del pie se construyen a partir de esta respuesta, nunca escritos a
/// mano (ADR-004).
/// </remarks>
/// <param name="Product">Nombre del producto. Siempre <c>SILLAR</c>.</param>
/// <param name="Version">Versión del producto instalada.</param>
/// <param name="Modules">Módulos activos.</param>
public sealed record CapabilitiesResponse(
    string Product,
    string Version,
    IReadOnlyList<ModuleCapability> Modules);

/// <summary>Un módulo activo, visto desde fuera.</summary>
/// <remarks>
/// Solo código y versión. Ni nombre de licencia, ni vigencia, ni límites: esta
/// respuesta es pública y no lleva información comercial.
/// </remarks>
/// <param name="Code">Código del módulo, por ejemplo <c>catalog</c>.</param>
/// <param name="Version">Versión del módulo.</param>
public sealed record ModuleCapability(string Code, string Version);
