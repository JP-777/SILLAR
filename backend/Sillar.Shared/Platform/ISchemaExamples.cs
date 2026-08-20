namespace Sillar.Shared.Platform;

/// <summary>
/// Los cuerpos de ejemplo que un módulo aporta a la documentación del API.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un módulo declara datos, no conoce la herramienta.</b> Aquí solo hay
/// tipos y cadenas JSON: quién los convierte en un documento OpenAPI es
/// asunto de <c>Sillar.Api</c>, y si mañana se cambia de generador no hay que
/// tocar ningún módulo.
/// </para>
/// <para>
/// Hace falta porque el generador <b>no lee <c>&lt;example&gt;</c> de un
/// <c>record</c> posicional</b> —comprobado contra el documento real— y todos
/// los DTO de SILLAR lo son. Sin esto, cada cuerpo se documenta con
/// <c>"string"</c> en cada campo, que no le sirve a nadie que llegue de
/// fuera.
/// </para>
/// </remarks>
public interface ISchemaExamples
{
    /// <summary>
    /// El JSON de ejemplo de cada tipo de petición, indexado por el tipo.
    /// </summary>
    /// <remarks>
    /// El criterio para escribir uno es siempre el mismo: <b>¿podría alguien
    /// que no conoce SILLAR copiarlo y que le funcione?</b>
    /// </remarks>
    IReadOnlyDictionary<Type, string> Examples { get; }
}
