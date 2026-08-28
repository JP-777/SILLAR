using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Crm.Authentication;
using Sillar.Modules.Crm.Domain;

namespace Sillar.Modules.Crm.Tests;

[Collection("CrmDb")]
public sealed class CustomerAccountTokenServiceTests(
    CrmDbFixture fixture) : IClassFixture<CrmDbFixture>
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 28, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Verificacion_guarda_hash_y_solo_el_ultimo_token_es_valido()
    {
        var data = await SeedAccountAsync(
            verified: false,
            password: "clave-original-muy-larga");

        await using var db = fixture.CreateContext();
        var service = Service(db);

        var first = await service.IssueEmailVerificationAsync(
            data.Email,
            TestContext.Current.CancellationToken);

        var second = await service.IssueEmailVerificationAsync(
            data.Email,
            TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.Token, second.Token);

        db.ChangeTracker.Clear();

        var stored = await db.CustomerTokens
            .AsNoTracking()
            .OrderBy(token => token.CustomerTokenId)
            .ToListAsync();

        Assert.Equal(2, stored.Count);
        Assert.NotNull(stored[0].UsedAt);
        Assert.Null(stored[1].UsedAt);

        Assert.DoesNotContain(
            stored,
            token => token.TokenHash == first.Token);

        Assert.True(
            SessionTokens.Matches(
                second.Token,
                stored[1].TokenHash));

        Assert.False(
            await service.VerifyEmailAsync(
                first.Token,
                TestContext.Current.CancellationToken));

        Assert.True(
            await service.VerifyEmailAsync(
                second.Token,
                TestContext.Current.CancellationToken));

        Assert.False(
            await service.VerifyEmailAsync(
                second.Token,
                TestContext.Current.CancellationToken));

        db.ChangeTracker.Clear();

        var account = await db.CustomerAccounts
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(Now, account.EmailVerifiedAt);
    }

    [Fact]
    public async Task Recuperar_password_revoca_todas_las_sesiones()
    {
        var data = await SeedAccountAsync(
            verified: true,
            password: "clave-original-muy-larga");

        await using var db = fixture.CreateContext();
        var time = new FixedTimeProvider(Now);
        var sessionService = new CustomerSessionService(db, time);

        var firstSession = await sessionService.OpenAsync(
            data.AccountId,
            Now.AddHours(2),
            null,
            null,
            TestContext.Current.CancellationToken);

        var secondSession = await sessionService.OpenAsync(
            data.AccountId,
            Now.AddHours(2),
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.NotNull(firstSession);
        Assert.NotNull(secondSession);

        var service = Service(db);

        var issued = await service.IssuePasswordResetAsync(
            data.Email,
            TestContext.Current.CancellationToken);

        Assert.NotNull(issued);

        var reset = await service.ResetPasswordAsync(
            issued.Token,
            "farol-montana-rio-829",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            CustomerPasswordTokenOutcome.Success,
            reset.Outcome);

        db.ChangeTracker.Clear();

        var account = await db.CustomerAccounts
            .AsNoTracking()
            .SingleAsync();

        var passwords = new CustomerPasswordHasher();

        Assert.True(
            passwords.Verify(
                "farol-montana-rio-829",
                account.PasswordHash));

        Assert.False(
            passwords.Verify(
                "clave-original-muy-larga",
                account.PasswordHash));

        var sessions = await db.CustomerSessions
            .AsNoTracking()
            .ToListAsync();

        Assert.Equal(2, sessions.Count);
        Assert.All(
            sessions,
            session => Assert.Equal(Now, session.RevokedAt));

        var secondAttempt = await service.ResetPasswordAsync(
            issued.Token,
            "otra-clave-segura-987",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            CustomerPasswordTokenOutcome.InvalidToken,
            secondAttempt.Outcome);
    }

    [Fact]
    public async Task Invitacion_crea_cuenta_y_verifica_correo()
    {
        await fixture.CleanAllTablesAsync();

        Guid customerId;

        await using (var seed = fixture.CreateContext())
        {
            var customer = new Customer
            {
                FullName = "Cliente Invitado",
                Email = "invitado@ejemplo.pe",
                IsActive = true
            };

            seed.Customers.Add(customer);
            await seed.SaveChangesAsync();
            customerId = customer.CustomerId;
        }

        await using var db = fixture.CreateContext();
        var service = Service(db);

        var issued = await service.IssueInvitationAsync(
            customerId,
            TestContext.Current.CancellationToken);

        Assert.NotNull(issued);

        var accepted = await service.AcceptInvitationAsync(
            issued.Token,
            "farol-montana-rio-829",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            CustomerPasswordTokenOutcome.Success,
            accepted.Outcome);

        db.ChangeTracker.Clear();

        var account = await db.CustomerAccounts
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(customerId, account.CustomerId);
        Assert.Equal(Now, account.EmailVerifiedAt);

        var passwords = new CustomerPasswordHasher();

        Assert.True(
            passwords.Verify(
                "farol-montana-rio-829",
                account.PasswordHash));
    }

    private CustomerAccountTokenService Service(
        Sillar.Modules.Crm.Data.CrmDbContext db)
        => new(
            db,
            new CustomerPasswordHasher(),
            new FixedTimeProvider(Now));

    private async Task<SeededAccount> SeedAccountAsync(
        bool verified,
        string password)
    {
        await fixture.CleanAllTablesAsync();

        await using var db = fixture.CreateContext();

        var customer = new Customer
        {
            FullName = "Cliente Token",
            Email = "token@ejemplo.pe",
            IsActive = true
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var account = new CustomerAccount
        {
            CustomerId = customer.CustomerId,
            PasswordHash = new CustomerPasswordHasher().Hash(password),
            EmailVerifiedAt = verified ? Now : null
        };

        db.CustomerAccounts.Add(account);
        await db.SaveChangesAsync();

        return new SeededAccount(
            customer.CustomerId,
            account.CustomerAccountId,
            customer.Email);
    }

    private sealed record SeededAccount(
        Guid CustomerId,
        int AccountId,
        string Email);

    private sealed class FixedTimeProvider(
        DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
