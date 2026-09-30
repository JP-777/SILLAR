using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Crm.Data;

namespace Sillar.Modules.Crm.Profiles;

/// <summary>Implementación del contrato de identidad mínima del cliente.</summary>
internal sealed class CustomerIdentityReader(
    CrmDbContext database) : ICustomerIdentityReader
{
    public async Task<CustomerIdentity?> GetAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        // InternalNotes, documento y direcciones no se seleccionan.
        return await database.Customers
            .AsNoTracking()
            .Where(customer => customer.CustomerId == customerId && customer.IsActive)
            .Select(customer => new CustomerIdentity(
                customer.CustomerId,
                customer.FullName,
                customer.Email,
                customer.Phone))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, CustomerIdentity>> GetManyAsync(
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken cancellationToken)
    {
        if (customerIds.Count == 0)
        {
            return new Dictionary<Guid, CustomerIdentity>();
        }

        var ids = customerIds.Distinct().ToArray();

        return await database.Customers
            .AsNoTracking()
            .Where(customer => ids.Contains(customer.CustomerId) && customer.IsActive)
            .Select(customer => new CustomerIdentity(
                customer.CustomerId,
                customer.FullName,
                customer.Email,
                customer.Phone))
            .ToDictionaryAsync(identity => identity.CustomerId, cancellationToken);
    }
}
