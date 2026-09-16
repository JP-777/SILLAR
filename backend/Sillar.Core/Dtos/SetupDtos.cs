namespace Sillar.Core.Dtos;

/// <summary>Si la instalación está pendiente.</summary>
/// <remarks>
/// <c>MigrationsPending</c> distingue dos situaciones que no tienen la misma
/// solución: «falta instalar», que se resuelve desde el asistente, y «la tabla de
/// instalación no está en esta base», que el asistente no debe intentar arreglar.
/// El arranque de la interfaz consume ambas banderas para no ofrecer instalación
/// cuando primero corresponde comprobar la conexión o preparar las migraciones.
/// El nombre refleja la causa más frecuente, aunque lo comprobado es la ausencia
/// de la tabla y una conexión equivocada produce el mismo estado.
/// </remarks>
/// <param name="SetupRequired">Verdadero mientras no se haya completado.</param>
/// <param name="MigrationsPending">
/// Verdadero si la tabla de instalación no está en la base a la que se conectó. Implica
/// <c>SetupRequired</c>: sin tablas no hay instalación posible.
/// </param>
public sealed record SetupStatusResponse(bool SetupRequired, bool MigrationsPending = false);

/// <summary>Datos para completar la instalación.</summary>
/// <param name="BusinessName">Nombre comercial del negocio.</param>
/// <param name="LicenseType">Tipo de licencia: <c>trial</c>, <c>subscription</c> o <c>perpetual</c>.</param>
/// <param name="Admin">Primer administrador, que será <c>super_admin</c>.</param>
public sealed record SetupRequest(string? BusinessName, string? LicenseType, SetupAdminRequest? Admin);

/// <summary>Primer administrador de la instalación.</summary>
/// <param name="FullName">Nombre completo.</param>
/// <param name="Email">Correo, que será su identificador de acceso.</param>
/// <param name="Password">Contraseña elegida por la persona.</param>
public sealed record SetupAdminRequest(string? FullName, string? Email, string? Password);

/// <summary>Instalación completada.</summary>
/// <param name="BusinessName">Nombre del negocio instalado.</param>
/// <param name="AdminUserId">Identificador del administrador creado.</param>
/// <param name="Email">Correo con el que iniciar sesión.</param>
public sealed record SetupResponse(string BusinessName, int AdminUserId, string Email);
