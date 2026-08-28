using System.Text;
using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Crm.Data;
using Sillar.Modules.Crm.Domain;

namespace Sillar.Modules.Crm.Authentication;

internal sealed record CustomerIssuedToken(
    Guid CustomerId,
    string Recipient,
    string FullName,
    string Token,
    string Purpose);

internal enum CustomerPasswordTokenOutcome
{
    Success,
    InvalidToken,
    InvalidPassword
}

internal sealed record CustomerPasswordTokenResult(
    CustomerPasswordTokenOutcome Outcome,
    string? Error = null);

/// <summary>
/// Tokens de un solo uso para verificar, recuperar e invitar.
/// </summary>
internal sealed class CustomerAccountTokenService(
    CrmDbContext database,
    CustomerPasswordHasher passwords,
    TimeProvider clock)
{
    public async Task<CustomerIssuedToken?> IssueEmailVerificationAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);

        var target = await (
            from customer in database.Customers.AsNoTracking()
            join account in database.CustomerAccounts.AsNoTracking()
                on customer.CustomerId equals account.CustomerId
            where customer.Email == normalized
                && customer.IsActive
                && account.EmailVerifiedAt == null
            select new
            {
                customer.CustomerId,
                customer.Email,
                customer.FullName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            return null;
        }

        return await IssueAsync(
            target.CustomerId,
            target.Email,
            target.FullName,
            CustomerTokenPurpose.EmailVerification,
            CustomerTokenPolicy.EmailVerificationLifetime,
            cancellationToken);
    }

    public async Task<CustomerIssuedToken?> IssuePasswordResetAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);

        var target = await (
            from customer in database.Customers.AsNoTracking()
            join account in database.CustomerAccounts.AsNoTracking()
                on customer.CustomerId equals account.CustomerId
            where customer.Email == normalized
                && customer.IsActive
            select new
            {
                customer.CustomerId,
                customer.Email,
                customer.FullName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            // Se paga igualmente generación + hash de un secreto para que el
            // camino inexistente no sea una simple consulta y retorno.
            _ = SessionTokens.Hash(SessionTokens.CreateSessionToken());
            return null;
        }

        return await IssueAsync(
            target.CustomerId,
            target.Email,
            target.FullName,
            CustomerTokenPurpose.PasswordReset,
            CustomerTokenPolicy.PasswordResetLifetime,
            cancellationToken);
    }

    public async Task<CustomerIssuedToken?> IssueInvitationAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var target = await database.Customers
            .AsNoTracking()
            .Where(customer =>
                customer.CustomerId == customerId
                && customer.IsActive)
            .Select(customer => new
            {
                customer.CustomerId,
                customer.Email,
                customer.FullName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            return null;
        }

        var alreadyHasAccount = await database.CustomerAccounts
            .AsNoTracking()
            .AnyAsync(
                account => account.CustomerId == customerId,
                cancellationToken);

        if (alreadyHasAccount)
        {
            return null;
        }

        return await IssueAsync(
            target.CustomerId,
            target.Email,
            target.FullName,
            CustomerTokenPurpose.Invitation,
            CustomerTokenPolicy.InvitationLifetime,
            cancellationToken);
    }

    public async Task<bool> VerifyEmailAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var hash = SessionTokens.Hash(token);

        var candidate = await (
            from stored in database.CustomerTokens.AsNoTracking()
            join account in database.CustomerAccounts.AsNoTracking()
                on stored.CustomerId equals account.CustomerId
            where stored.TokenHash == hash
                && stored.Purpose == CustomerTokenPurpose.EmailVerification
                && stored.UsedAt == null
                && stored.ExpiresAt > now
            select new
            {
                stored.CustomerTokenId,
                account.CustomerAccountId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return false;
        }

        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        var consumed = await ConsumeAsync(
            candidate.CustomerTokenId,
            CustomerTokenPurpose.EmailVerification,
            now,
            cancellationToken);

        if (!consumed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await database.CustomerAccounts
            .Where(account =>
                account.CustomerAccountId == candidate.CustomerAccountId
                && account.EmailVerifiedAt == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(
                    account => account.EmailVerifiedAt,
                    now),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<CustomerPasswordTokenResult> ResetPasswordAsync(
        string token,
        string? newPassword,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var hash = SessionTokens.Hash(token);

        var candidate = await (
            from stored in database.CustomerTokens.AsNoTracking()
            join customer in database.Customers.AsNoTracking()
                on stored.CustomerId equals customer.CustomerId
            join account in database.CustomerAccounts
                on customer.CustomerId equals account.CustomerId
            where stored.TokenHash == hash
                && stored.Purpose == CustomerTokenPurpose.PasswordReset
                && stored.UsedAt == null
                && stored.ExpiresAt > now
                && customer.IsActive
            select new
            {
                stored.CustomerTokenId,
                account.CustomerAccountId,
                customer.Email,
                customer.FullName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return new CustomerPasswordTokenResult(
                CustomerPasswordTokenOutcome.InvalidToken);
        }

        var check = CustomerPasswordPolicy.Check(
            newPassword,
            candidate.Email,
            candidate.FullName);

        if (!check.IsValid)
        {
            return new CustomerPasswordTokenResult(
                CustomerPasswordTokenOutcome.InvalidPassword,
                check.Error);
        }

        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        var consumed = await ConsumeAsync(
            candidate.CustomerTokenId,
            CustomerTokenPurpose.PasswordReset,
            now,
            cancellationToken);

        if (!consumed)
        {
            await transaction.RollbackAsync(cancellationToken);

            return new CustomerPasswordTokenResult(
                CustomerPasswordTokenOutcome.InvalidToken);
        }

        var passwordHash = passwords.Hash(newPassword!);

        var accountUpdated = await database.CustomerAccounts
            .Where(account =>
                account.CustomerAccountId == candidate.CustomerAccountId)
            .ExecuteUpdateAsync(
                update => update.SetProperty(
                    account => account.PasswordHash,
                    passwordHash),
                cancellationToken);

        if (accountUpdated != 1)
        {
            await transaction.RollbackAsync(cancellationToken);

            return new CustomerPasswordTokenResult(
                CustomerPasswordTokenOutcome.InvalidToken);
        }

        await database.CustomerSessions
            .Where(session =>
                session.CustomerAccountId == candidate.CustomerAccountId
                && session.RevokedAt == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(
                    session => session.RevokedAt,
                    now),
                cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CustomerPasswordTokenResult(
            CustomerPasswordTokenOutcome.Success);
    }

    public async Task<CustomerPasswordTokenResult> AcceptInvitationAsync(
        string token,
        string? password,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var hash = SessionTokens.Hash(token);

        var candidate = await (
            from stored in database.CustomerTokens.AsNoTracking()
            join customer in database.Customers.AsNoTracking()
                on stored.CustomerId equals customer.CustomerId
            where stored.TokenHash == hash
                && stored.Purpose == CustomerTokenPurpose.Invitation
                && stored.UsedAt == null
                && stored.ExpiresAt > now
                && customer.IsActive
            select new
            {
                stored.CustomerTokenId,
                customer.CustomerId,
                customer.Email,
                customer.FullName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return new CustomerPasswordTokenResult(
                CustomerPasswordTokenOutcome.InvalidToken);
        }

        var alreadyHasAccount = await database.CustomerAccounts
            .AsNoTracking()
            .AnyAsync(
                account => account.CustomerId == candidate.CustomerId,
                cancellationToken);

        if (alreadyHasAccount)
        {
            return new CustomerPasswordTokenResult(
                CustomerPasswordTokenOutcome.InvalidToken);
        }

        var check = CustomerPasswordPolicy.Check(
            password,
            candidate.Email,
            candidate.FullName);

        if (!check.IsValid)
        {
            return new CustomerPasswordTokenResult(
                CustomerPasswordTokenOutcome.InvalidPassword,
                check.Error);
        }

        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        var consumed = await ConsumeAsync(
            candidate.CustomerTokenId,
            CustomerTokenPurpose.Invitation,
            now,
            cancellationToken);

        if (!consumed)
        {
            await transaction.RollbackAsync(cancellationToken);

            return new CustomerPasswordTokenResult(
                CustomerPasswordTokenOutcome.InvalidToken);
        }

        database.CustomerAccounts.Add(
            new CustomerAccount
            {
                CustomerId = candidate.CustomerId,
                PasswordHash = passwords.Hash(password!),
                // El enlace llegó al correo de esa ficha; aceptarlo prueba
                // control del buzón.
                EmailVerifiedAt = now
            });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CustomerPasswordTokenResult(
                CustomerPasswordTokenOutcome.Success);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);

            return new CustomerPasswordTokenResult(
                CustomerPasswordTokenOutcome.InvalidToken);
        }
    }

    private async Task<CustomerIssuedToken> IssueAsync(
        Guid customerId,
        string recipient,
        string fullName,
        string purpose,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        // Solo el último enlace de cada propósito queda vigente.
        await database.CustomerTokens
            .Where(stored =>
                stored.CustomerId == customerId
                && stored.Purpose == purpose
                && stored.UsedAt == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(
                    stored => stored.UsedAt,
                    now),
                cancellationToken);

        var token = SessionTokens.CreateSessionToken();

        database.CustomerTokens.Add(
            new CustomerToken
            {
                CustomerId = customerId,
                Purpose = purpose,
                TokenHash = SessionTokens.Hash(token),
                CreatedAt = now,
                ExpiresAt = now + lifetime
            });

        await database.SaveChangesAsync(cancellationToken);

        return new CustomerIssuedToken(
            customerId,
            recipient,
            fullName,
            token,
            purpose);
    }

    private async Task<bool> ConsumeAsync(
        int tokenId,
        string purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var affected = await database.CustomerTokens
            .Where(stored =>
                stored.CustomerTokenId == tokenId
                && stored.Purpose == purpose
                && stored.UsedAt == null
                && stored.ExpiresAt > now)
            .ExecuteUpdateAsync(
                update => update.SetProperty(
                    stored => stored.UsedAt,
                    now),
                cancellationToken);

        return affected == 1;
    }

    private static string NormalizeEmail(string email)
        => email.Trim().Normalize(NormalizationForm.FormC);
}
