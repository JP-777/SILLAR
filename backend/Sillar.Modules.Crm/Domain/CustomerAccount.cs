namespace Sillar.Modules.Crm.Domain;

/// <summary>
/// Cuenta de acceso de un cliente.
/// </summary>
/// <remarks>
/// No se replica: la cuenta vive en el nodo donde el cliente se autentica.
/// La relación ficha → cuenta es 0..1, garantizada por el <c>UNIQUE</c> sobre
/// <see cref="CustomerId"/> y por la FK hacia <c>crm.customers</c>.
///
/// La FK va de la tabla local (esta) hacia la tabla replicada
/// (<c>customers</c>): es la dirección permitida.
/// </remarks>
public class CustomerAccount
{
    /// <summary>Identificador generado por la base de datos.</summary>
    public int CustomerAccountId { get; set; }

    /// <summary>Ficha del cliente. Única: una ficha tiene 0..1 cuenta.</summary>
    public required Guid CustomerId { get; set; }

    /// <summary>Hash de la contraseña. No es BCrypt todavía: solo persistencia.</summary>
    public required string PasswordHash { get; set; }

    /// <summary>
    /// Cuándo se verificó el correo. Se anula mediante trigger si cambia
    /// <c>customers.email</c>.
    /// </summary>
    public DateTimeOffset? EmailVerifiedAt { get; set; }

    /// <summary>Fecha de alta.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Fecha de la última modificación. La escribe un trigger.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
