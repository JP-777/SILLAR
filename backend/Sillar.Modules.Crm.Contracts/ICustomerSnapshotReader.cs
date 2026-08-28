namespace Sillar.Modules.Crm.Contracts;

/// <summary>
/// Lectura mínima de CRM para que otros módulos congelen datos del cliente.
/// </summary>
/// <remarks>
/// No expone entidades de CRM. M03 debe guardar su propia instantánea y no
/// depender de que la ficha o la dirección sigan iguales después.
/// </remarks>
public interface ICustomerSnapshotReader
{
    Task<CustomerOrderSnapshot?> GetForOrderAsync(
        Guid customerId,
        Guid customerAddressId,
        CancellationToken cancellationToken);
}

/// <summary>Datos del cliente que un pedido puede congelar.</summary>
public sealed record CustomerOrderSnapshot(
    Guid CustomerId,
    string FullName,
    string Email,
    string? Phone,
    string? DocumentType,
    string? DocumentNumber,
    bool EmailVerified,
    CustomerOrderAddressSnapshot Address);

/// <summary>Dirección elegida que un pedido puede congelar.</summary>
public sealed record CustomerOrderAddressSnapshot(
    Guid CustomerAddressId,
    string? Label,
    string AddressLine,
    string? District,
    string? Province,
    string? Department,
    string? Reference);
