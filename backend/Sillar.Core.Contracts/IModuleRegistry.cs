namespace Sillar.Core.Contracts;

/// <summary>
/// Qué módulos están activos en esta instalación.
/// </summary>
/// <remarks>
/// Es la forma correcta de resolver una dependencia blanda: se pregunta por el
/// módulo y, si no está, se degrada el comportamiento sin fallar. Nunca un
/// <c>if</c> suelto ni una excepción por una dependencia blanda ausente.
///
/// La respuesta se calcula una vez en el arranque: las activaciones no cambian
/// mientras el host vive, porque activar o desactivar un módulo altera qué
/// servicios y qué rutas existen.
/// </remarks>
public interface IModuleRegistry
{
    /// <summary>Indica si el módulo indicado está activo.</summary>
    /// <param name="moduleCode">Código del módulo, por ejemplo <c>catalog</c>.</param>
    bool IsActive(string moduleCode);

    /// <summary>Devuelve los módulos activos, en orden de presentación.</summary>
    IReadOnlyList<ActiveModule> GetActive();
}
