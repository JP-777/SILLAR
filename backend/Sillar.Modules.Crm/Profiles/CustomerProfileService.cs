using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Modules.Crm.Data;
using Sillar.Modules.Crm.Domain;
using Sillar.Modules.Crm.Dtos;

namespace Sillar.Modules.Crm.Profiles;

internal enum CustomerProfileUpdateOutcome
{
    Updated,
    NotFound,
    EmailConflict,
    DocumentConflict
}

internal sealed record CustomerProfileUpdateResult(
    CustomerProfileUpdateOutcome Outcome,
    CustomerProfileResponse? Profile = null);

/// <summary>Lectura y edición del perfil propio de la clientela.</summary>
internal sealed class CustomerProfileService(
    CrmDbContext database,
    TimeProvider clock)
{
    public async Task<CustomerProfileResponse?> GetAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        // Selección explícita: InternalNotes nunca entra al objeto público.
        var header = await (
            from customer in database.Customers.AsNoTracking()
            join account in database.CustomerAccounts.AsNoTracking()
                on customer.CustomerId equals account.CustomerId
            where customer.CustomerId == customerId
                  && customer.IsActive
            select new
            {
                customer.CustomerId,
                customer.FullName,
                customer.Email,
                customer.Phone,
                customer.DocumentType,
                customer.DocumentNumber,
                EmailVerified = account.EmailVerifiedAt != null
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        var addresses = await database.CustomerAddresses
            .AsNoTracking()
            .Where(address =>
                address.CustomerId == customerId
                && address.IsActive)
            .OrderByDescending(address => address.IsPreferred)
            .ThenBy(address => address.CreatedAt)
            .Select(address => new CustomerAddressResponse(
                address.CustomerAddressId,
                address.Label,
                address.AddressLine,
                address.District,
                address.Province,
                address.Department,
                address.Reference,
                address.IsPreferred))
            .ToListAsync(cancellationToken);

        return new CustomerProfileResponse(
            header.CustomerId,
            header.FullName,
            header.Email,
            header.Phone,
            header.DocumentType,
            header.DocumentNumber,
            header.EmailVerified,
            addresses);
    }

    public async Task<CustomerProfileUpdateResult> UpdateAsync(
        Guid customerId,
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await database.Customers
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.CustomerId == customerId
                    && candidate.IsActive,
                cancellationToken);

        if (customer is null)
        {
            return new CustomerProfileUpdateResult(
                CustomerProfileUpdateOutcome.NotFound);
        }

        customer.FullName = request.FullName!.Trim();
        customer.Email = request.Email!.Trim();
        customer.Phone = Optional(request.Phone);
        customer.DocumentType =
            Optional(request.DocumentType)?.ToLowerInvariant();
        customer.DocumentNumber = Optional(request.DocumentNumber);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (HasConstraint(exception, "uq_customers_email"))
        {
            database.ChangeTracker.Clear();

            return new CustomerProfileUpdateResult(
                CustomerProfileUpdateOutcome.EmailConflict);
        }
        catch (DbUpdateException exception)
            when (HasConstraint(exception, "uq_customers_document"))
        {
            database.ChangeTracker.Clear();

            return new CustomerProfileUpdateResult(
                CustomerProfileUpdateOutcome.DocumentConflict);
        }

        return new CustomerProfileUpdateResult(
            CustomerProfileUpdateOutcome.Updated,
            await GetAsync(customerId, cancellationToken));
    }

    public async Task<CustomerAddressResponse?> CreateAddressAsync(
        Guid customerId,
        SaveCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        var customerExists = await database.Customers
            .AsNoTracking()
            .AnyAsync(
                customer =>
                    customer.CustomerId == customerId
                    && customer.IsActive,
                cancellationToken);

        if (!customerExists)
        {
            return null;
        }

        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        if (request.IsPreferred)
        {
            await ClearPreferredAsync(
                customerId,
                exceptAddressId: null,
                cancellationToken);
        }

        var address = new CustomerAddress
        {
            CustomerId = customerId,
            Label = Optional(request.Label),
            AddressLine = request.AddressLine!.Trim(),
            District = Optional(request.District),
            Province = Optional(request.Province),
            Department = Optional(request.Department),
            Reference = Optional(request.Reference),
            IsPreferred = request.IsPreferred,
            IsActive = true
        };

        database.CustomerAddresses.Add(address);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(address);
    }

    public async Task<CustomerAddressResponse?> UpdateAddressAsync(
        Guid customerId,
        Guid customerAddressId,
        SaveCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        var address = await database.CustomerAddresses
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.CustomerAddressId == customerAddressId
                    && candidate.CustomerId == customerId
                    && candidate.IsActive,
                cancellationToken);

        if (address is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (request.IsPreferred && !address.IsPreferred)
        {
            await ClearPreferredAsync(
                customerId,
                customerAddressId,
                cancellationToken);
        }

        address.Label = Optional(request.Label);
        address.AddressLine = request.AddressLine!.Trim();
        address.District = Optional(request.District);
        address.Province = Optional(request.Province);
        address.Department = Optional(request.Department);
        address.Reference = Optional(request.Reference);
        address.IsPreferred = request.IsPreferred;

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(address);
    }

    public async Task<CustomerAddressResponse?> SetPreferredAsync(
        Guid customerId,
        Guid customerAddressId,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        var address = await database.CustomerAddresses
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.CustomerAddressId == customerAddressId
                    && candidate.CustomerId == customerId
                    && candidate.IsActive,
                cancellationToken);

        if (address is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (!address.IsPreferred)
        {
            await ClearPreferredAsync(
                customerId,
                customerAddressId,
                cancellationToken);

            address.IsPreferred = true;
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ToResponse(address);
    }

    public async Task<bool> DeleteAddressAsync(
        Guid customerId,
        Guid customerAddressId,
        CancellationToken cancellationToken)
    {
        var address = await database.CustomerAddresses
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.CustomerAddressId == customerAddressId
                    && candidate.CustomerId == customerId
                    && candidate.IsActive,
                cancellationToken);

        if (address is null)
        {
            return false;
        }

        address.IsPreferred = false;
        address.IsActive = false;

        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ClearPreferredAsync(
        Guid customerId,
        Guid? exceptAddressId,
        CancellationToken cancellationToken)
    {
        var query = database.CustomerAddresses
            .Where(address =>
                address.CustomerId == customerId
                && address.IsActive
                && address.IsPreferred);

        if (exceptAddressId is { } addressId)
        {
            query = query.Where(
                address => address.CustomerAddressId != addressId);
        }

        var now = clock.GetUtcNow();

        // ExecuteUpdate evita el choque momentáneo del índice único parcial,
        // pero conserva manualmente las marcas que SaveChanges pondría en una
        // entidad replicada.
        await query.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(address => address.IsPreferred, false)
                .SetProperty(
                    address => address.RowVersion,
                    address => address.RowVersion + 1)
                .SetProperty(address => address.UpdatedAt, now),
            cancellationToken);
    }

    private static CustomerAddressResponse ToResponse(
        CustomerAddress address)
        => new(
            address.CustomerAddressId,
            address.Label,
            address.AddressLine,
            address.District,
            address.Province,
            address.Department,
            address.Reference,
            address.IsPreferred);

    private static string? Optional(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static bool HasConstraint(
        DbUpdateException exception,
        string constraintName)
        => exception.InnerException is PostgresException postgres
           && string.Equals(
               postgres.ConstraintName,
               constraintName,
               StringComparison.Ordinal);
}
