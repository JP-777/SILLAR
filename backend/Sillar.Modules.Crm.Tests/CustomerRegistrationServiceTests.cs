using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Crm.Authentication;
using Sillar.Modules.Crm.Domain;

namespace Sillar.Modules.Crm.Tests;

[Collection("CrmDb")]
public sealed class CustomerRegistrationServiceTests(
    CrmDbFixture fixture) : IClassFixture<CrmDbFixture>
{
    [Fact]
    public async Task Registro_nuevo_crea_ficha_y_cuenta_no_verificada()
    {
        await fixture.CleanAllTablesAsync();

        await using var db = fixture.CreateContext();
        var passwords = new CustomerPasswordHasher();
        var service = new CustomerRegistrationService(db, passwords);

        var outcome = await service.RegisterAsync(
            "  Ana Quispe  ",
            " ANA@ejemplo.pe ",
            "farol-montana-rio-829",
            " 999111222 ",
            CancellationToken.None);

        Assert.Equal(CustomerRegistrationOutcome.Created, outcome);

        db.ChangeTracker.Clear();

        var customer = await db.Customers.AsNoTracking().SingleAsync();
        var account = await db.CustomerAccounts.AsNoTracking().SingleAsync();

        Assert.Equal("Ana Quispe", customer.FullName);
        Assert.Equal("ANA@ejemplo.pe", customer.Email);
        Assert.Equal("999111222", customer.Phone);
        Assert.True(customer.IsActive);

        Assert.Equal(customer.CustomerId, account.CustomerId);
        Assert.Null(account.EmailVerifiedAt);
        Assert.True(
            passwords.Verify(
                "farol-montana-rio-829",
                account.PasswordHash));
    }

    [Fact]
    public async Task Ficha_existente_sin_cuenta_se_enlaza_sin_duplicarla_ni_pisar_notas()
    {
        await fixture.CleanAllTablesAsync();

        Guid originalId;

        await using (var seed = fixture.CreateContext())
        {
            var customer = new Customer
            {
                FullName = "Nombre del negocio",
                Email = "cliente@ejemplo.pe",
                Phone = "111",
                InternalNotes = "NO DEBE SALIR NI SER PISADA",
                IsActive = true
            };

            seed.Customers.Add(customer);
            await seed.SaveChangesAsync();
            originalId = customer.CustomerId;
        }

        await using var db = fixture.CreateContext();
        var passwords = new CustomerPasswordHasher();
        var service = new CustomerRegistrationService(db, passwords);

        var outcome = await service.RegisterAsync(
            "Nombre escrito en la web",
            " CLIENTE@ejemplo.pe ",
            "farol-montana-rio-829",
            "999",
            CancellationToken.None);

        Assert.Equal(CustomerRegistrationOutcome.Linked, outcome);

        db.ChangeTracker.Clear();

        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(1, await db.CustomerAccounts.CountAsync());

        var customerAfter = await db.Customers.AsNoTracking().SingleAsync();
        var account = await db.CustomerAccounts.AsNoTracking().SingleAsync();

        Assert.Equal(originalId, customerAfter.CustomerId);
        Assert.Equal("Nombre del negocio", customerAfter.FullName);
        Assert.Equal("111", customerAfter.Phone);
        Assert.Equal(
            "NO DEBE SALIR NI SER PISADA",
            customerAfter.InternalNotes);

        Assert.Equal(originalId, account.CustomerId);
        Assert.True(
            passwords.Verify(
                "farol-montana-rio-829",
                account.PasswordHash));
    }

    [Fact]
    public async Task Ficha_con_cuenta_no_reemplaza_la_password_existente()
    {
        await fixture.CleanAllTablesAsync();

        var passwords = new CustomerPasswordHasher();
        string originalHash;

        await using (var seed = fixture.CreateContext())
        {
            var customer = new Customer
            {
                FullName = "Cliente existente",
                Email = "existente@ejemplo.pe",
                IsActive = true
            };

            originalHash = passwords.Hash("clave-original-muy-larga");

            seed.Customers.Add(customer);
            seed.CustomerAccounts.Add(new CustomerAccount
            {
                CustomerId = customer.CustomerId,
                PasswordHash = originalHash
            });

            await seed.SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var service = new CustomerRegistrationService(db, passwords);

        var outcome = await service.RegisterAsync(
            "Intento",
            "existente@ejemplo.pe",
            "farol-montana-rio-829",
            null,
            CancellationToken.None);

        Assert.Equal(
            CustomerRegistrationOutcome.AlreadyRegistered,
            outcome);

        db.ChangeTracker.Clear();

        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(1, await db.CustomerAccounts.CountAsync());

        var account = await db.CustomerAccounts
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(originalHash, account.PasswordHash);
        Assert.True(
            passwords.Verify(
                "clave-original-muy-larga",
                account.PasswordHash));

        Assert.False(
            passwords.Verify(
                "farol-montana-rio-829",
                account.PasswordHash));
    }
}
