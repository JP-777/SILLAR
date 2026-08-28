using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Modules.Crm.Data;
using Sillar.Modules.Crm.Domain;

namespace Sillar.Modules.Crm.Authentication;

internal enum CustomerRegistrationOutcome
{
    Created,
    Linked,
    AlreadyRegistered
}

/// <summary>
/// Crea la ficha+cuenta o enlaza una cuenta a una ficha ya conocida.
/// </summary>
/// <remarks>
/// Hacia HTTP los tres resultados se vuelven la misma respuesta para que el
/// registro no permita averiguar qué correos ya existen.
/// </remarks>
internal sealed class CustomerRegistrationService(
    CrmDbContext database,
    CustomerPasswordHasher passwords)
{
    public async Task<CustomerRegistrationOutcome> RegisterAsync(
        string fullName,
        string email,
        string password,
        string? phone,
        CancellationToken cancellationToken)
    {
        var normalizedName = fullName
            .Trim()
            .Normalize(NormalizationForm.FormC);

        var normalizedEmail = email
            .Trim()
            .Normalize(NormalizationForm.FormC);

        var normalizedPhone = string.IsNullOrWhiteSpace(phone)
            ? null
            : phone.Trim().Normalize(NormalizationForm.FormC);

        // Se calcula antes de consultar existencia. Incluso si ya había cuenta,
        // el camino paga BCrypt y el tiempo no se convierte en un enumerador
        // trivial de correos.
        var passwordHash = passwords.Hash(password);

        var existing = await database.Customers
            .SingleOrDefaultAsync(
                customer => customer.Email == normalizedEmail,
                cancellationToken);

        try
        {
            if (existing is null)
            {
                var customer = new Customer
                {
                    FullName = normalizedName,
                    Email = normalizedEmail,
                    Phone = normalizedPhone,
                    IsActive = true
                };

                database.Customers.Add(customer);
                database.CustomerAccounts.Add(new CustomerAccount
                {
                    CustomerId = customer.CustomerId,
                    PasswordHash = passwordHash
                });

                await database.SaveChangesAsync(cancellationToken);
                return CustomerRegistrationOutcome.Created;
            }

            var alreadyHasAccount = await database.CustomerAccounts
                .AnyAsync(
                    account => account.CustomerId == existing.CustomerId,
                    cancellationToken);

            if (alreadyHasAccount)
            {
                return CustomerRegistrationOutcome.AlreadyRegistered;
            }

            // Se conserva la ficha que ya tenía el negocio: nombre, teléfono,
            // documento y especialmente InternalNotes no se pisan desde una
            // petición pública. Solo nace la cuenta que faltaba.
            database.CustomerAccounts.Add(new CustomerAccount
            {
                CustomerId = existing.CustomerId,
                PasswordHash = passwordHash
            });

            await database.SaveChangesAsync(cancellationToken);
            return CustomerRegistrationOutcome.Linked;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Dos registros concurrentes pueden leer "no existe" a la vez.
            // Los UNIQUE de email y customer_id son la autoridad final. Hacia
            // fuera sigue siendo exactamente la misma respuesta.
            database.ChangeTracker.Clear();
            return CustomerRegistrationOutcome.AlreadyRegistered;
        }
    }
}
