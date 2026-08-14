namespace Sillar.Core.Domain;

/// <summary>Usuario administrador del negocio.</summary>
public class AdminUser
{
    /// <summary>Identificador.</summary>
    public int AdminUserId { get; set; }

    /// <summary>Nombre completo.</summary>
    public required string FullName { get; set; }

    /// <summary>
    /// Correo. Sirve de identificador de acceso, así que se compara sin
    /// distinguir mayúsculas: la columna usa la colación <c>core.es_ci</c>.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Hash BCrypt de la contraseña, factor de trabajo 12 o más.
    /// </summary>
    /// <remarks>
    /// Nunca sale del backend: ninguna respuesta del API lo incluye y nunca
    /// aparece en un registro de log.
    /// </remarks>
    public required string PasswordHash { get; set; }

    /// <summary>Rol. Ver <see cref="Values.AdminRole"/>.</summary>
    public required string Role { get; set; }

    /// <summary>Teléfono de contacto.</summary>
    public string? Phone { get; set; }

    /// <summary>Eliminación lógica: un usuario nunca se borra físicamente.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Último acceso correcto.</summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Intentos fallidos consecutivos. Se reinicia con un acceso correcto.
    /// </summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Bloqueo temporal tras varios intentos fallidos.</summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>Fecha de alta.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Fecha de la última modificación. La escribe un trigger.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Sesiones abiertas por este usuario.</summary>
    public ICollection<AdminSession> Sessions { get; set; } = [];
}
