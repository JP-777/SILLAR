using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Crm.Authentication;
using Sillar.Modules.Crm.Domain;

namespace Sillar.Modules.Crm.Tests;

/// <summary>
/// Pruebas del ciclo real de customer_sessions contra PostgreSQL.
/// </summary>
[Collection("CrmDb")]
public sealed class CustomerSessionServiceTests(
    CrmDbFixture fixture) : IClassFixture<CrmDbFixture>
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 27, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Abrir_sesion_guarda_solo_hashes_de_los_secretos()
    {
        var accountId = await SeedAccountAsync();

        await using var db = fixture.CreateContext();

        var service = new CustomerSessionService(
            db,
            new FixedTimeProvider(Now));

        var grant = await service.OpenAsync(
            accountId,
            Now.AddHours(2),
            "203.0.113.10",
            "Navegador de prueba",
            CancellationToken.None);

        Assert.NotNull(grant);
        Assert.True(grant.SessionId > 0);

        db.ChangeTracker.Clear();

        var stored = await db.CustomerSessions
            .AsNoTracking()
            .SingleAsync();

        Assert.NotEqual(grant.SessionToken, stored.TokenHash);
        Assert.NotEqual(grant.CsrfToken, stored.CsrfTokenHash);

        Assert.True(
            SessionTokens.Matches(
                grant.SessionToken,
                stored.TokenHash));

        Assert.True(
            SessionTokens.Matches(
                grant.CsrfToken,
                stored.CsrfTokenHash));

        Assert.Equal(Now, stored.IssuedAt);
        Assert.Equal(Now, stored.LastSeenAt);
        Assert.Equal(Now.AddHours(2), stored.ExpiresAt);
    }

    [Fact]
    public async Task Rotar_csrf_invalida_el_token_anterior()
    {
        var accountId = await SeedAccountAsync();

        await using var db = fixture.CreateContext();

        var service = new CustomerSessionService(
            db,
            new FixedTimeProvider(Now));

        var grant = await service.OpenAsync(
            accountId,
            Now.AddHours(2),
            null,
            null,
            CancellationToken.None);

        Assert.NotNull(grant);

        var rotated = await service.RotateCsrfAsync(
            grant.SessionId,
            accountId,
            CancellationToken.None);

        Assert.NotNull(rotated);
        Assert.NotEqual(grant.CsrfToken, rotated);

        db.ChangeTracker.Clear();

        var stored = await db.CustomerSessions
            .AsNoTracking()
            .SingleAsync();

        Assert.False(
            SessionTokens.Matches(
                grant.CsrfToken,
                stored.CsrfTokenHash));

        Assert.True(
            SessionTokens.Matches(
                rotated,
                stored.CsrfTokenHash));
    }

    [Fact]
    public async Task Logout_revoca_la_fila_y_no_se_puede_rotar_csrf()
    {
        var accountId = await SeedAccountAsync();

        await using var db = fixture.CreateContext();

        var service = new CustomerSessionService(
            db,
            new FixedTimeProvider(Now));

        var grant = await service.OpenAsync(
            accountId,
            Now.AddHours(2),
            null,
            null,
            CancellationToken.None);

        Assert.NotNull(grant);

        var revoked = await service.LogoutAsync(
            grant.SessionId,
            accountId,
            CancellationToken.None);

        Assert.True(revoked);

        var rotated = await service.RotateCsrfAsync(
            grant.SessionId,
            accountId,
            CancellationToken.None);

        Assert.Null(rotated);

        db.ChangeTracker.Clear();

        var stored = await db.CustomerSessions
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(Now, stored.RevokedAt);
    }

    private async Task<int> SeedAccountAsync()
    {
        await fixture.CleanAllTablesAsync();

        await using var db = fixture.CreateContext();

        var customer = new Customer
        {
            FullName = "Cliente Sesión",
            Email = "sesion@ejemplo.pe",
            IsActive = true
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var account = new CustomerAccount
        {
            CustomerId = customer.CustomerId,
            PasswordHash = "hash-de-prueba"
        };

        db.CustomerAccounts.Add(account);
        await db.SaveChangesAsync();

        return account.CustomerAccountId;
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
