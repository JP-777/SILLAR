namespace Sillar.Shared.Modularity;

/// <summary>Operación administrativa declarada por su módulo; CORE conserva y evalúa sus permisos.</summary>
/// <param name="Code">Código estable y local al módulo.</param>
/// <param name="DisplayName">Nombre en español para configurar permisos.</param>
/// <param name="MinimumRole">Rol mínimo; un permiso explícito no permite saltárselo.</param>
public sealed record ModuleOperation(string Code, string DisplayName, string MinimumRole);
