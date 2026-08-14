namespace Sillar.Core.Contracts.Events;

/// <summary>Un módulo pasó a estar activo en esta instalación.</summary>
/// <remarks>
/// Se publica tras confirmar la transacción. Quien lo reciba debe tener presente
/// que el host se detiene a continuación para relanzarse: el enrutamiento se
/// construye al arrancar y no cambia en caliente.
/// </remarks>
/// <param name="Code">Código del módulo.</param>
/// <param name="OccurredAt">Cuándo ocurrió.</param>
public sealed record ModuleActivated(string Code, DateTimeOffset OccurredAt);

/// <summary>Un módulo dejó de estar activo en esta instalación.</summary>
/// <param name="Code">Código del módulo.</param>
/// <param name="OccurredAt">Cuándo ocurrió.</param>
public sealed record ModuleDeactivated(string Code, DateTimeOffset OccurredAt);

/// <summary>Cambió una configuración del sitio.</summary>
/// <remarks>
/// <b>No lleva el valor</b>, ni el nuevo ni el anterior. La tabla está pensada
/// para alojar también credenciales de correo saliente, y un evento que los
/// transporte los reparte por los registros de todo el que escuche.
/// </remarks>
/// <param name="Key">Clave que cambió.</param>
/// <param name="IsPublic">Si la clave queda expuesta en el endpoint público.</param>
/// <param name="OccurredAt">Cuándo ocurrió.</param>
public sealed record SettingChanged(string Key, bool IsPublic, DateTimeOffset OccurredAt);
