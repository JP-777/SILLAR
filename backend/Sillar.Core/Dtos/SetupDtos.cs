namespace Sillar.Core.Dtos;

/// <summary>Si la instalación está pendiente.</summary>
/// <param name="SetupRequired">Verdadero mientras no se haya completado.</param>
public sealed record SetupStatusResponse(bool SetupRequired);

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
