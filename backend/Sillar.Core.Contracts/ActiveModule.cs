namespace Sillar.Core.Contracts;

/// <summary>Módulo activo en esta instalación.</summary>
/// <remarks>
/// Deliberadamente no lleva nada de licencia: ni vigencia, ni límites, ni tipo
/// de contrato. Eso es información comercial y no sale de CORE.
/// </remarks>
/// <param name="Code">Código del módulo. También el nombre de su schema.</param>
/// <param name="DisplayName">Nombre visible, en español.</param>
/// <param name="Version">Versión del módulo instalada.</param>
public sealed record ActiveModule(string Code, string DisplayName, string Version);
