namespace Sillar.Modules.Crm.Domain;

/// <summary>
/// Token de un solo uso para invitación, verificación de correo o
/// recuperación de contraseña.
/// </summary>
/// <remarks>
/// No se replica. El consumo es atómico:
///
/// <code>
/// UPDATE crm.customer_tokens
///    SET used_at = now()
///  WHERE customer_token_id = @id
///    AND used_at IS NULL
///    AND expires_at > now();
/// </code>
///
/// 1 fila afectada = éxito. 0 filas = usado / inválido / caducado. Dos
/// consumos concurrentes producen exactamente un ganador.
/// </remarks>
public class CustomerToken
{
    /// <summary>Identificador generado por la base de datos.</summary>
    public int CustomerTokenId { get; set; }

    /// <summary>Ficha del cliente.</summary>
    public required Guid CustomerId { get; set; }

    /// <summary>
    /// Propósito: <c>invitation</c>, <c>email_verification</c> o
    /// <c>password_reset</c>.
    /// </summary>
    public required string Purpose { get; set; }

    /// <summary>Hash del token. Único.</summary>
    public required string TokenHash { get; set; }

    /// <summary>Fecha de creación.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Cuándo caduca. Siempre posterior a <see cref="CreatedAt"/>.</summary>
    public required DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Cuándo se consumió, si se consumió.</summary>
    public DateTimeOffset? UsedAt { get; set; }
}
