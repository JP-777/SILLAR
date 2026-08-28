using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Crm.Data;

namespace Sillar.Modules.Crm.Profiles;

/// <summary>Implementación del contrato de instantánea que consumirá M03.</summary>
internal sealed class CustomerSnapshotReader(
    CrmDbContext database) : ICustomerSnapshotReader
{
    public async Task<CustomerOrderSnapshot?> GetForOrderAsync(
        Guid customerId,
        Guid customerAddressId,
        CancellationToken cancellationToken)
    {
        // InternalNotes no se selecciona ni cruza la frontera del contrato.
        var data = await (
            from customer in database.Customers.AsNoTracking()
            join account in database.CustomerAccounts.AsNoTracking()
                on customer.CustomerId equals account.CustomerId
            join address in database.CustomerAddresses.AsNoTracking()
                on customer.CustomerId equals address.CustomerId
            where customer.CustomerId == customerId
                  && customer.IsActive
                  && address.CustomerAddressId == customerAddressId
                  && address.IsActive
            select new
            {
                customer.CustomerId,
                customer.FullName,
                customer.Email,
                customer.Phone,
                customer.DocumentType,
                customer.DocumentNumber,
                EmailVerified = account.EmailVerifiedAt != null,
                address.CustomerAddressId,
                address.Label,
                address.AddressLine,
                address.District,
                address.Province,
                address.Department,
                address.Reference
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (data is null)
        {
            return null;
        }

        return new CustomerOrderSnapshot(
            data.CustomerId,
            data.FullName,
            data.Email,
            data.Phone,
            data.DocumentType,
            data.DocumentNumber,
            data.EmailVerified,
            new CustomerOrderAddressSnapshot(
                data.CustomerAddressId,
                data.Label,
                data.AddressLine,
                data.District,
                data.Province,
                data.Department,
                data.Reference));
    }
}
