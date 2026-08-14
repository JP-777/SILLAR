using Sillar.Core.Contracts;

namespace Sillar.Core.Modularity;

/// <summary>
/// Foto de las activaciones tomada durante el arranque.
/// </summary>
/// <remarks>
/// Se lee una sola vez y no cambia mientras el host vive. No es una optimización:
/// activar o desactivar un módulo altera qué servicios existen en el contenedor y
/// qué rutas están montadas, así que un cambio en caliente dejaría el proceso
/// contradiciéndose consigo mismo. Los cambios de activación se aplican al
/// reiniciar.
/// </remarks>
/// <param name="ActiveModules">Módulos activos, en orden de presentación.</param>
public sealed record ModuleActivationSnapshot(IReadOnlyList<ActiveModule> ActiveModules);
