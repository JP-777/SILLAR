namespace Sillar.Modules.Crm.Contracts;

/// <summary>
/// Lectura mínima de CRM para que otros módulos congelen datos del cliente.
/// </summary>
/// <remarks>
/// No expone entidades de CRM. M03 debe guardar su propia instantánea y no
/// depender de que la ficha o la dirección sigan iguales después.
///
/// Las dos variantes aplican las mismas guardas sobre el cliente: la ficha
/// existe, está activa y tiene cuenta. Si alguna falla devuelven <c>null</c>,
/// sin distinguir el motivo. La cuenta es obligatoria porque comprar en línea
/// la exige; una ficha de mostrador sin cuenta no es un cliente de pedido.
///
/// Versión 1.1.0 del contrato: se añade la variante sin dirección. La de
/// 1.0.0 no cambia de firma ni de comportamiento.
/// </remarks>
public interface ICustomerSnapshotReader
{
    /// <summary>
    /// Instantánea para un pedido con entrega: datos del cliente y la dirección
    /// elegida.
    /// </summary>
    /// <remarks>
    /// La dirección es obligatoria. Devuelve <c>null</c> si no existe, está de
    /// baja o pertenece a otro cliente: nunca cae en la variante sin dirección.
    /// </remarks>
    Task<CustomerOrderSnapshot?> GetForOrderAsync(
        Guid customerId,
        Guid customerAddressId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Instantánea para un pedido sin entrega —recojo en tienda—: solo los
    /// datos del cliente, sin ninguna dirección.
    /// </summary>
    /// <remarks>
    /// Desde 1.1.0. Quien necesite domicilio usa la otra variante: esta no
    /// devuelve dirección ni la inventa.
    /// </remarks>
    Task<CustomerOrderContactSnapshot?> GetForOrderAsync(
        Guid customerId,
        CancellationToken cancellationToken);
}

/// <summary>Datos del cliente que un pedido sin entrega puede congelar.</summary>
/// <remarks>Desde 1.1.0. No lleva dirección.</remarks>
public sealed record CustomerOrderContactSnapshot(
    Guid CustomerId,
    string FullName,
    string Email,
    string? Phone,
    string? DocumentType,
    string? DocumentNumber,
    bool EmailVerified);

/// <summary>Datos del cliente que un pedido con entrega puede congelar.</summary>
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
