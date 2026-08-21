namespace Sillar.Core.Contracts;

/// <summary>
/// Qué módulos están <b>activados</b> en esta instalación.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esto informa; no decide si algo se puede usar.</b> La diferencia importa
/// porque los dos nombres suenan igual de bien desde fuera, y se eligen mal
/// justo cuando más da igual — hasta que no da igual.
/// </para>
/// <list type="table">
///   <item>
///     <term>Preguntar aquí</term>
///     <description>
///       Responde según <b>la foto de las activaciones</b> del arranque. Sirve
///       para <b>contar</b> lo que hay: pintar el menú, responder
///       <c>/api/capabilities</c>, decir qué está contratado.
///     </description>
///   </item>
///   <item>
///     <term>Preguntar al contenedor</term>
///     <description>
///       Pedir el contrato del otro módulo —<c>GetService&lt;IOtroServicio&gt;()</c>—
///       y comprobar si vino. Responde según <b>lo que de verdad está cargado y
///       se puede llamar</b>. <b>Es lo que hay que usar para una dependencia
///       blanda</b>, y es lo que dice <c>CLAUDE.md</c>.
///     </description>
///   </item>
/// </list>
/// <para>
/// <b>Por qué no da lo mismo.</b> Una fila de <c>core.modules</c> es una
/// declaración; el contenedor es el efecto. El 21 de agosto de 2026 apareció
/// en esa tabla un módulo que <b>no existía en el binario</b> — arrancó bien y
/// avisó, pero un <c>IsActive("cms")</c> habría contestado que sí. Preguntar al
/// registro es preguntarle a lo que alguien escribió; preguntar al contenedor
/// es mirar lo que hay.
/// </para>
/// <para>
/// Y la regla que no cambia: si el otro módulo no está, <b>se degrada sin
/// fallar</b>. Nunca una excepción porque falte una dependencia blanda.
/// </para>
/// </remarks>
public interface IModuleRegistry
{
    /// <summary>Indica si el módulo indicado está activo.</summary>
    /// <param name="moduleCode">Código del módulo, por ejemplo <c>catalog</c>.</param>
    bool IsActive(string moduleCode);

    /// <summary>Devuelve los módulos activos, en orden de presentación.</summary>
    IReadOnlyList<ActiveModule> GetActive();
}
