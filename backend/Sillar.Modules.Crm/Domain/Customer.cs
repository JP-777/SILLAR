using Sillar.Shared.Replication;

namespace Sillar.Modules.Crm.Domain;

/// <summary>
/// Ficha de cliente: la persona que el negocio conoce.
/// </summary>
/// <remarks>
/// Es la única entidad replicada del módulo junto con
/// <see cref="CustomerAddress"/>: ambas pueden nacer en un nodo ERP y viajar
/// a la web, o al revés. El resto de tablas de M04 son locales del nodo.
///
/// La clave se genera en la aplicación con <c>Guid.CreateVersion7()</c>
/// (ADR-016, regla 1): un nodo sin conexión tiene que poder crear la fila
/// entera antes de hablar con nadie.
/// </remarks>
public class Customer : IReplicatedEntity
{
    /// <summary>Identificador de la ficha.</summary>
    public Guid CustomerId { get; set; } = Guid.CreateVersion7();

    /// <summary>Nombre completo del cliente.</summary>
    public required string FullName { get; set; }

    /// <summary>
    /// Correo electrónico. Colación <c>core.es_ci</c>: ignora mayúsculas,
    /// respeta tildes.
    /// </summary>
    /// <remarks>
    /// Se normaliza en <c>CrmDbContext</c> antes de persistir: <c>Trim()</c> +
    /// <c>Normalize(NormalizationForm.FormC)</c>. No se convierte a minúsculas:
    /// la equivalencia la resuelve <c>core.es_ci</c>.
    /// </remarks>
    public required string Email { get; set; }

    /// <summary>Teléfono, si se conoce.</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Tipo de documento: <c>dni</c> o <c>ruc</c>, o <c>null</c> si no se
    /// declaró. Va siempre acompañado de <see cref="DocumentNumber"/>.
    /// </summary>
    public string? DocumentType { get; set; }

    /// <summary>Número de documento, o <c>null</c> si no se declaró.</summary>
    public string? DocumentNumber { get; set; }

    /// <summary>Notas internas del negocio sobre el cliente.</summary>
    public string? InternalNotes { get; set; }

    /// <summary>
    /// Ficha activa. Junto con <see cref="DeactivatedAt"/> y
    /// <see cref="BlockedAt"/> forma el estado físico (ACTIVA, DE BAJA,
    /// BLOQUEADA). No existe cuarto estado.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Cuándo se dio de baja. Solo cuando <see cref="IsActive"/> es false y no hay bloqueo.</summary>
    public DateTimeOffset? DeactivatedAt { get; set; }

    /// <summary>Cuándo se bloqueó. Solo cuando <see cref="IsActive"/> es false y no hay baja.</summary>
    public DateTimeOffset? BlockedAt { get; set; }

    /// <summary>Cuándo se solicitó reactivación de una ficha bloqueada.</summary>
    public DateTimeOffset? ReactivationRequestedAt { get; set; }

    /// <summary>Cuándo administración resolvió la solicitud de reactivación.</summary>
    public DateTimeOffset? ReactivationResolvedAt { get; set; }

    /// <inheritdoc />
    public string OriginNode { get; set; } = string.Empty;

    /// <inheritdoc />
    public long RowVersion { get; set; } = 1;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
