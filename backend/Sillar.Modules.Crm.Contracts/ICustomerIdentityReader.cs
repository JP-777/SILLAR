namespace Sillar.Modules.Crm.Contracts;

/// <summary>
/// Quién es un cliente, en lo mínimo que una persona necesita para
/// reconocerlo: nombre, correo y teléfono. Solo lectura.
/// </summary>
/// <remarks>
/// Para paneles de otros módulos que guardan <c>customer_id</c> y tienen que
/// enseñar a una persona, no un identificador —por ejemplo, la bandeja B2B de
/// M07—. No es la instantánea de pedido (<see cref="ICustomerSnapshotReader"/>):
/// no pide dirección, no exige cuenta y no se congela; devuelve el dato vigente
/// en cada lectura.
///
/// Devuelve solo fichas **activas**. Una ficha inexistente, de baja o bloqueada
/// no aparece: <c>null</c> en <see cref="GetAsync"/> y ausente del diccionario en
/// <see cref="GetManyAsync"/>, sin distinguir el motivo.
///
/// No expone notas internas, documento, dirección, estado de la cuenta ni nada
/// de la autenticación.
/// </remarks>
public interface ICustomerIdentityReader
{
    /// <summary>La identidad de un cliente activo, o <c>null</c>.</summary>
    Task<CustomerIdentity?> GetAsync(
        Guid customerId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Las identidades de varios clientes en una sola consulta, para listados.
    /// </summary>
    /// <remarks>
    /// La clave es el <c>customerId</c>. Los que no existen o no están activos
    /// no aparecen. Los repetidos se leen una vez. Una colección vacía devuelve
    /// un diccionario vacío.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, CustomerIdentity>> GetManyAsync(
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken cancellationToken);
}

/// <summary>Identidad mínima y legible de un cliente.</summary>
public sealed record CustomerIdentity(
    Guid CustomerId,
    string FullName,
    string Email,
    string? Phone);
