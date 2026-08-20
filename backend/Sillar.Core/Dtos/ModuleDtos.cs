namespace Sillar.Core.Dtos;

/// <summary>Un módulo del catálogo, con su estado en esta instalación.</summary>
/// <param name="Code">Código del módulo. También el nombre de su schema.</param>
/// <param name="DisplayName">Nombre visible.</param>
/// <param name="Description">Qué hace, en lenguaje de negocio.</param>
/// <param name="Version">Versión instalada.</param>
/// <param name="IsCore">Verdadero solo para CORE, que no se puede desactivar.</param>
/// <param name="IsActive">Si está activo ahora.</param>
/// <param name="ActivatedAt">Última activación.</param>
/// <param name="DeactivatedAt">Última desactivación.</param>
/// <param name="ExpiresAt">
/// Vencimiento de la licencia del módulo, si lo tiene. Se devuelve para poder
/// mostrarlo, pero <b>no se evalúa</b>: el control de vencimientos es de la fase
/// de comercialización (ADR-004), así que hoy un módulo vencido sigue activo.
/// </param>
/// <param name="DisplayOrder">Orden de presentación en el panel.</param>
/// <param name="HardDependencies">Módulos sin los cuales no funciona.</param>
/// <param name="SoftDependencies">Módulos que lo enriquecen, pero que pueden faltar.</param>
/// <param name="CanActivate">Si se puede activar ahora mismo.</param>
/// <param name="CanDeactivate">Si se puede desactivar ahora mismo.</param>
/// <param name="BlockedBy">
/// Qué lo impide: las dependencias duras inactivas si no se puede activar, o los
/// módulos activos que dependen de él si no se puede desactivar. Se calcula en el
/// servidor a propósito — si el frontend rehiciera el análisis del grafo,
/// tendríamos dos implementaciones de la misma regla y una se quedaría atrás.
/// </param>
/// <param name="RestartsAutomatically">
/// Si esta instalación reinicia el host sola tras un cambio de activación
/// (<c>Modules:RestartAfterActivation</c>). Igual para las seis tarjetas: es un
/// dato del despliegue, no del módulo. Existe para que el diálogo de
/// confirmación —que se muestra <b>antes</b> de activar o desactivar, cuando
/// todavía no hay respuesta de esa operación que consultar— sepa qué frase es
/// cierta en <i>esta</i> instalación, en vez de suponer que siempre se reinicia
/// sola.
/// </param>
public sealed record ModuleResponse(
    string Code,
    string DisplayName,
    string Description,
    string Version,
    bool IsCore,
    bool IsActive,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? DeactivatedAt,
    DateTimeOffset? ExpiresAt,
    int DisplayOrder,
    IReadOnlyList<string> HardDependencies,
    IReadOnlyList<string> SoftDependencies,
    bool CanActivate,
    bool CanDeactivate,
    IReadOnlyList<string> BlockedBy,
    bool RestartsAutomatically);

/// <summary>Resultado de activar o desactivar un módulo.</summary>
/// <param name="Code">Código del módulo.</param>
/// <param name="IsActive">Estado en el que queda.</param>
/// <param name="Restart">
/// Qué pasa con el proceso: <c>scheduled</c> si va a detenerse solo para
/// relanzarse, <c>required</c> si hay que relanzarlo a mano, y <c>none</c> si la
/// operación no cambió nada.
/// </param>
/// <param name="Message">Explicación para mostrar en el panel.</param>
public sealed record ModuleActivationResponse(
    string Code,
    bool IsActive,
    string Restart,
    string Message);

/// <summary>Valores posibles de <see cref="ModuleActivationResponse.Restart"/>.</summary>
public static class RestartOutcome
{
    /// <summary>El host se detendrá y el orquestador lo relanzará.</summary>
    public const string Scheduled = "scheduled";

    /// <summary>Hace falta relanzar el host a mano para que el cambio surta efecto.</summary>
    public const string Required = "required";

    /// <summary>Nada que reiniciar: la operación no cambió el estado.</summary>
    public const string None = "none";
}
