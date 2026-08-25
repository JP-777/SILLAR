namespace Sillar.Modules.Crm.Domain;

/// <summary>
/// Sesión activa de un cliente.
/// </summary>
/// <remarks>
/// No se replica. Usa <c>integer GENERATED ALWAYS AS IDENTITY</c> como PK, la
/// convención vigente para tablas no replicadas — no copia la PK Guid
/// histórica de <c>AdminSession</c>.
///
/// <see cref="LastSeenAt"/> sostiene la renovación deslizante.
/// </remarks>
public class CustomerSession
{
    /// <summary>Identificador generado por la base de datos.</summary>
    public int CustomerSessionId { get; set; }

    /// <summary>Cuenta a la que pertenece la sesión.</summary>
    public required int CustomerAccountId { get; set; }

    /// <summary>Hash del token de sesión. Único.</summary>
    public required string TokenHash { get; set; }

    /// <summary>Hash del token CSRF.</summary>
    public required string CsrfTokenHash { get; set; }

    /// <summary>Cuándo se emitió la sesión.</summary>
    public required DateTimeOffset IssuedAt { get; set; }

    /// <summary>
    /// Última vez que se vio activa. Sostiene la renovación deslizante.
    /// </summary>
    public required DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Cuándo caduca la sesión.</summary>
    public required DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Cuándo se revocó, si se revocó.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Dirección IP desde la que se emitió.</summary>
    public string? IpAddress { get; set; }

    /// <summary>User-Agent del navegador.</summary>
    public string? UserAgent { get; set; }
}
