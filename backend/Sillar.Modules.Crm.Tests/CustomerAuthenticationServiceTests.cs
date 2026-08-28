using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Crm.Authentication;
using Sillar.Modules.Crm.Domain;

namespace Sillar.Modules.Crm.Tests;

[Collection("CrmDb")]
public sealed class CustomerAuthenticationServiceTests(
    CrmDbFixture fixture) : IClassFixture<CrmDbFixture>
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 28, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Login_correcto_crea_customer_session()
    {
        var password = "ClaveSegura-123!";
        await SeedAsync(password, active: true);

        await using var db = fixture.CreateContext();
        var authentication = CreateService(db);

        var attempt = await authentication.LoginAsync(
            " CLIENTE@ejemplo.pe ",
            password,
            "203.0.113.8",
            "Prueba",
            CancellationToken.None);

        Assert.Equal(CustomerLoginOutcome.Granted, attempt.Outcome);
        Assert.NotNull(attempt.Session);
        Assert.NotNull(attempt.Customer);

        Assert.Equal(1, await db.CustomerSessions.CountAsync());
    }

    [Fact]
    public async Task Correo_inexistente_y_password_incorrecto_tienen_mismo_resultado()
    {
        await SeedAsync("ClaveSegura-123!", active: true);

        await using var db = fixture.CreateContext();
        var authentication = CreateService(db);

        var unknown = await authentication.LoginAsync(
            "nadie@ejemplo.pe",
            "incorrecta",
            "203.0.113.9",
            null,
            CancellationToken.None);

        var wrong = await authentication.LoginAsync(
            "cliente@ejemplo.pe",
            "incorrecta",
            "203.0.113.9",
            null,
            CancellationToken.None);

        Assert.Equal(CustomerLoginOutcome.Denied, unknown.Outcome);
        Assert.Equal(CustomerLoginOutcome.Denied, wrong.Outcome);
        Assert.Equal(0, await db.CustomerSessions.CountAsync());
    }

    [Fact]
    public async Task Cliente_inactivo_no_puede_abrir_sesion()
    {
        var password = "ClaveSegura-123!";
        await SeedAsync(password, active: false);

        await using var db = fixture.CreateContext();
        var authentication = CreateService(db);

        var attempt = await authentication.LoginAsync(
            "cliente@ejemplo.pe",
            password,
            "203.0.113.10",
            null,
            CancellationToken.None);

        Assert.Equal(CustomerLoginOutcome.Denied, attempt.Outcome);
        Assert.Equal(0, await db.CustomerSessions.CountAsync());
    }

    private CustomerAuthenticationService CreateService(
        Sillar.Modules.Crm.Data.CrmDbContext db)
    {
        var time = new FixedTimeProvider(Now);
        var passwords = new CustomerPasswordHasher();
        var throttle = new CustomerLoginThrottle(time);
        var sessions = new CustomerSessionService(db, time);

        return new CustomerAuthenticationService(
            db,
            passwords,
            throttle,
            sessions,
            time);
    }

    private async Task SeedAsync(string password, bool active)
    {
        await fixture.CleanAllTablesAsync();

        await using var db = fixture.CreateContext();

        var passwords = new CustomerPasswordHasher();

        var customer = new Customer
        {
            FullName = "Cliente Login",
            Email = "cliente@ejemplo.pe",
            IsActive = active,
            DeactivatedAt = active ? null : Now
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.CustomerAccounts.Add(new CustomerAccount
        {
            CustomerId = customer.CustomerId,
            PasswordHash = passwords.Hash(password)
        });

        await db.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
