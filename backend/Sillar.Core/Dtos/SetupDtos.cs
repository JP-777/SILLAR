namespace Sillar.Core.Dtos;

/// <summary>Si la instalación está pendiente.</summary>
/// <remarks>
/// <c>MigrationsPending</c> se añadió después y es opcional a propósito: quien
/// solo lee <c>SetupRequired</c> —el arranque de la interfaz, <c>App.tsx</c>—
/// sigue funcionando igual. Distingue dos situaciones que antes se veían iguales
/// desde fuera y no lo son: «falta instalar», que se arregla desde el asistente,
/// y «la tabla de instalación no está en esta base», que <b>no</b> — eso lo
/// arregla quien despliega, desde una terminal, y el asistente no puede hacer
/// nada al respecto. La bandera se llama <c>MigrationsPending</c> porque ésa es
/// la causa más frecuente, pero <b>lo que se sabe es lo primero</b>: la tabla no
/// está donde se buscó, y eso también ocurre con una conexión equivocada. Quien
/// tenga que actuar lo lee entero en la respuesta del <c>POST</c>.
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
