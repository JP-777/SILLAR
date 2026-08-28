using System.Text;
using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Crm.Data;

namespace Sillar.Modules.Crm.Authentication;

internal enum CustomerLoginOutcome
{
    Granted,
    Denied
}

internal sealed record CustomerLoginIdentity(
    Guid CustomerId,
    int CustomerAccountId,
    string FullName,
    string Email,
    bool EmailVerified);

internal sealed record CustomerLoginAttempt(
    CustomerLoginOutcome Outcome,
    CustomerLoginIdentity? Customer = null,
    CustomerSessionGrant? Session = null);

/// <summary>Inicio de sesión público de M04.</summary>
internal sealed class CustomerAuthenticationService(
    CrmDbContext database,
    CustomerPasswordHasher passwords,
    CustomerLoginThrottle throttle,
    CustomerSessionService sessions,
    TimeProvider clock)
{
    public async Task<CustomerLoginAttempt> LoginAsync(
        string email,
        string password,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email
            .Trim()
            .Normalize(NormalizationForm.FormC);

        var candidate = await (
            from customer in database.Customers.AsNoTracking()
            join account in database.CustomerAccounts.AsNoTracking()
                on customer.CustomerId equals account.CustomerId
            where customer.Email == normalizedEmail
            select new
            {
                customer.CustomerId,
                customer.FullName,
                customer.Email,
                customer.IsActive,
                account.CustomerAccountId,
                account.PasswordHash,
                account.EmailVerifiedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            // Igualdad temporal: correo inexistente también paga BCrypt.
            passwords.VerifyDecoy(password);

            await throttle.RegisterFailureAsync(
                normalizedEmail,
                ipAddress,
                cancellationToken);

            return new CustomerLoginAttempt(CustomerLoginOutcome.Denied);
        }

        var passwordMatches = passwords.Verify(
            password,
            candidate.PasswordHash);

        // Cuenta existente, contraseña incorrecta y ficha no activa se vuelven
        // indistinguibles hacia fuera.
        if (!passwordMatches || !candidate.IsActive)
        {
            await throttle.RegisterFailureAsync(
                normalizedEmail,
                ipAddress,
                cancellationToken);

            return new CustomerLoginAttempt(CustomerLoginOutcome.Denied);
        }

        var grant = await sessions.OpenAsync(
            candidate.CustomerAccountId,
            clock.GetUtcNow() + CustomerSessionPolicy.Lifetime,
            ipAddress,
            userAgent,
            cancellationToken);

        // Cubre la carrera en la que la ficha se desactiva entre la consulta y
        // la creación de la sesión.
        if (grant is null)
        {
            await throttle.RegisterFailureAsync(
                normalizedEmail,
                ipAddress,
                cancellationToken);

            return new CustomerLoginAttempt(CustomerLoginOutcome.Denied);
        }

        throttle.RegisterSuccess(normalizedEmail);

        return new CustomerLoginAttempt(
            CustomerLoginOutcome.Granted,
            new CustomerLoginIdentity(
                candidate.CustomerId,
                candidate.CustomerAccountId,
                candidate.FullName,
                candidate.Email,
                candidate.EmailVerifiedAt is not null),
            grant);
    }
}
