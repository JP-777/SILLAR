using Sillar.Core.Contracts;
using Sillar.Shared.Modularity;

namespace Sillar.Core.Modularity;

/// <summary>
/// Los módulos que el host descubrió en el despliegue, activos o no.
/// </summary>
/// <remarks>
/// El descubrimiento es cosa del host, pero el endpoint de activación necesita
/// el catálogo completo para razonar sobre el grafo: qué depende de qué, qué
/// falta para activar algo y quién bloquea una desactivación. El host lo deja
/// aquí al arrancar, igual que la foto de activaciones.
/// </remarks>
/// <param name="Modules">Todos los módulos declarados en el código.</param>
public sealed record DeclaredModules(IReadOnlyList<IModule> Modules);

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
