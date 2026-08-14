namespace Sillar.Core.Dtos;

/// <summary>Credenciales de acceso.</summary>
/// <param name="Email">Correo. No distingue mayúsculas.</param>
/// <param name="Password">Contraseña.</param>
public sealed record LoginRequest(string? Email, string? Password);

/// <summary>Sesión abierta.</summary>
/// <param name="User">Quién ha entrado.</param>
/// <param name="CsrfToken">
/// Token que hay que enviar en la cabecera <c>X-CSRF-Token</c> en toda petición
/// que modifique datos. Viaja en el cuerpo, nunca en una cookie: si fuera una
/// cookie, el navegador la adjuntaría sola y no protegería de nada.
/// </param>
public sealed record LoginResponse(AuthenticatedUserResponse User, string CsrfToken);

/// <summary>Usuario en sesión. Nunca incluye el hash de la contraseña.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="FullName">Nombre completo.</param>
/// <param name="Email">Correo.</param>
/// <param name="Role">Rol: <c>super_admin</c>, <c>admin</c> o <c>editor</c>.</param>
public sealed record AuthenticatedUserResponse(int Id, string FullName, string Email, string Role);

/// <summary>Token CSRF de la sesión activa.</summary>
/// <param name="CsrfToken">Token nuevo. El anterior deja de valer.</param>
public sealed record CsrfTokenResponse(string CsrfToken);

/// <summary>Cambio de la contraseña propia.</summary>
/// <param name="CurrentPassword">Contraseña actual, exigida aunque haya sesión abierta.</param>
/// <param name="NewPassword">Contraseña nueva, que pasa la misma política.</param>
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
