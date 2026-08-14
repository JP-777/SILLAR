namespace Sillar.Core.Dtos;

/// <summary>Administrador, tal como lo ve el panel. Nunca incluye el hash.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="FullName">Nombre completo.</param>
/// <param name="Email">Correo, que es su identificador de acceso.</param>
/// <param name="Role">Rol.</param>
/// <param name="Phone">Teléfono de contacto.</param>
/// <param name="IsActive">Si puede entrar.</param>
/// <param name="LastLoginAt">Último acceso correcto.</param>
/// <param name="LockedUntil">Bloqueo temporal vigente, si lo hay.</param>
public sealed record AdminUserResponse(
    int Id,
    string FullName,
    string Email,
    string Role,
    string? Phone,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? LockedUntil);

/// <summary>Alta de un administrador.</summary>
/// <param name="FullName">Nombre completo.</param>
/// <param name="Email">Correo.</param>
/// <param name="Password">
/// Contraseña inicial, fijada por el <c>super_admin</c>. No se generan ni se
/// envían por correo mientras el sistema no sepa enviar correo.
/// </param>
/// <param name="Role">Rol.</param>
/// <param name="Phone">Teléfono de contacto.</param>
public sealed record CreateAdminUserRequest(
    string? FullName,
    string? Email,
    string? Password,
    string? Role,
    string? Phone);

/// <summary>Modificación de un administrador.</summary>
/// <param name="FullName">Nombre completo.</param>
/// <param name="Role">Rol.</param>
/// <param name="Phone">Teléfono de contacto.</param>
/// <param name="IsActive">Si puede entrar.</param>
public sealed record UpdateAdminUserRequest(string? FullName, string? Role, string? Phone, bool IsActive);

/// <summary>Sesión abierta, tal como se lista en el panel.</summary>
/// <param name="Id">Identificador de la sesión.</param>
/// <param name="AdminUserId">Dueño de la sesión.</param>
/// <param name="Email">Correo del dueño.</param>
/// <param name="IssuedAt">Cuándo se abrió.</param>
/// <param name="LastSeenAt">Última actividad.</param>
/// <param name="ExpiresAt">Cuándo caduca.</param>
/// <param name="RevokedAt">Cuándo se revocó, si se revocó.</param>
/// <param name="IpAddress">Dirección desde la que se abrió.</param>
/// <param name="UserAgent">Navegador declarado.</param>
public sealed record AdminSessionResponse(
    Guid Id,
    int AdminUserId,
    string Email,
    DateTimeOffset IssuedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    string? IpAddress,
    string? UserAgent);
