namespace Sillar.Core.Domain;

/// <summary>
/// Sesión administrativa respaldada en base de datos (ADR-010).
/// </summary>
/// <remarks>
/// Guardar la sesión aquí es lo que permite revocarla de verdad: cerrar sesión
/// marca la fila, no basta con borrar la cookie del navegador.
/// </remarks>
public class AdminSession
{
    /// <summary>Identificador, generado por la aplicación.</summary>
    public Guid AdminSessionId { get; set; }

    /// <summary>Usuario dueño de la sesión.</summary>
    public int AdminUserId { get; set; }

    /// <summary>
    /// Hash del token de sesión. <b>Nunca el token.</b> Quien lea la base de
    /// datos no puede suplantar a nadie.
    /// </summary>
    public required string TokenHash { get; set; }

    /// <summary>Hash del token CSRF asociado a esta sesión.</summary>
    public required string CsrfTokenHash { get; set; }

    /// <summary>Momento de emisión.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>Última actividad. Sostiene la renovación deslizante.</summary>
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Vencimiento.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Momento de revocación: cierre de sesión o revocación manual.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Dirección de origen. Admite IPv6.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Navegador declarado.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Usuario dueño de la sesión.</summary>
    public AdminUser? AdminUser { get; set; }
}
