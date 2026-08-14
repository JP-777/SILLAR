namespace Sillar.Core.Contracts;

/// <summary>Lee la configuración general del sitio.</summary>
/// <remarks>
/// Es la única forma que tienen los demás módulos de consultarla: nadie toca
/// <c>core.site_settings</c> por su cuenta.
///
/// Los valores se cachean en memoria porque esto se consulta en cada petición
/// pública. La caché es por proceso, que basta con una instancia por instalación
/// (ADR-001), y se invalida al escribir.
/// </remarks>
public interface ISettingsReader
{
    /// <summary>Devuelve el valor de una clave activa, o <c>null</c> si no existe.</summary>
    string? Get(string key);

    /// <summary>
    /// Devuelve el valor convertido al tipo pedido, o <c>default</c> si la clave
    /// no existe o el valor no se puede convertir.
    /// </summary>
    T? Get<T>(string key);

    /// <summary>
    /// Devuelve las configuraciones marcadas como públicas.
    /// </summary>
    /// <remarks>
    /// Solo las que alguien decidió publicar de forma deliberada: el valor por
    /// defecto de <c>is_public</c> es falso.
    /// </remarks>
    IReadOnlyDictionary<string, string> GetPublic();
}
