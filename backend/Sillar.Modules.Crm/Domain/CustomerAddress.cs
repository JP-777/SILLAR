using Sillar.Shared.Replication;

namespace Sillar.Modules.Crm.Domain;

/// <summary>
/// Dirección de entrega o contacto de un cliente.
/// </summary>
/// <remarks>
/// Se replica junto con <see cref="Customer"/>: una dirección creada en el
/// ERP tiene que existir en la web cuando la ficha viaje.
/// </remarks>
public class CustomerAddress : IReplicatedEntity
{
    /// <summary>Identificador de la dirección.</summary>
    public Guid CustomerAddressId { get; set; } = Guid.CreateVersion7();

    /// <summary>Ficha a la que pertenece.</summary>
    public required Guid CustomerId { get; set; }

    /// <summary>Etiqueta opcional: «casa», «oficina».</summary>
    public string? Label { get; set; }

    /// <summary>Línea principal de la dirección.</summary>
    public required string AddressLine { get; set; }

    /// <summary>Distrito.</summary>
    public string? District { get; set; }

    /// <summary>Provincia.</summary>
    public string? Province { get; set; }

    /// <summary>Departamento.</summary>
    public string? Department { get; set; }

    /// <summary>Referencia para el repartidor.</summary>
    public string? Reference { get; set; }

    /// <summary>
    /// Dirección preferida del cliente. Solo puede haber una activa y
    /// preferida por cliente.
    /// </summary>
    public bool IsPreferred { get; set; }

    /// <summary>Dirección activa.</summary>
    public bool IsActive { get; set; } = true;

    /// <inheritdoc />
    public string OriginNode { get; set; } = string.Empty;

    /// <inheritdoc />
    public long RowVersion { get; set; } = 1;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
