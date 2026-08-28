using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Crm.Domain;
using Sillar.Modules.Crm.Dtos;
using Sillar.Modules.Crm.Profiles;

namespace Sillar.Modules.Crm.Tests;

[Collection("CrmDb")]
public sealed class CustomerProfileServiceTests(
    CrmDbFixture fixture) : IClassFixture<CrmDbFixture>
{
    [Fact]
    public async Task Perfil_no_expone_notas_internas()
    {
        var seed = await SeedCustomerAsync(
            "perfil@ejemplo.pe",
            internalNotes: "NOTA PRIVADA QUE NO DEBE SALIR",
            verified: true);

        await using var db = fixture.CreateContext();
        var service = new CustomerProfileService(
            db,
            TimeProvider.System);

        var profile = await service.GetAsync(
            seed.CustomerId,
            TestContext.Current.CancellationToken);

        Assert.NotNull(profile);
        Assert.Equal("Cliente Perfil", profile.FullName);
        Assert.True(profile.EmailVerified);

        var json = JsonSerializer.Serialize(profile);

        Assert.DoesNotContain(
            "internalNotes",
            json,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "NOTA PRIVADA QUE NO DEBE SALIR",
            json,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cambiar_correo_invalida_verificacion()
    {
        var seed = await SeedCustomerAsync(
            "verificado@ejemplo.pe",
            internalNotes: null,
            verified: true);

        await using var db = fixture.CreateContext();
        var service = new CustomerProfileService(
            db,
            TimeProvider.System);

        var result = await service.UpdateAsync(
            seed.CustomerId,
            new UpdateCustomerProfileRequest(
                "Cliente Perfil",
                "nuevo-correo@ejemplo.pe",
                "+51 900 000 000",
                "dni",
                "12345678"),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            CustomerProfileUpdateOutcome.Updated,
            result.Outcome);

        db.ChangeTracker.Clear();

        var account = await db.CustomerAccounts
            .AsNoTracking()
            .SingleAsync(
                account => account.CustomerId == seed.CustomerId,
                TestContext.Current.CancellationToken);

        Assert.Null(account.EmailVerifiedAt);

        var profile = await service.GetAsync(
            seed.CustomerId,
            TestContext.Current.CancellationToken);

        Assert.NotNull(profile);
        Assert.False(profile.EmailVerified);
        Assert.Equal("nuevo-correo@ejemplo.pe", profile.Email);
    }

    [Fact]
    public async Task Agrega_dos_direcciones_y_cambia_la_preferida()
    {
        var seed = await SeedCustomerAsync(
            "direcciones@ejemplo.pe",
            internalNotes: null,
            verified: false);

        await using var db = fixture.CreateContext();
        var service = new CustomerProfileService(
            db,
            TimeProvider.System);

        var first = await service.CreateAddressAsync(
            seed.CustomerId,
            new SaveCustomerAddressRequest(
                "Casa",
                "Av. Ejército 100",
                "Yanahuara",
                "Arequipa",
                "Arequipa",
                null,
                true),
            TestContext.Current.CancellationToken);

        var second = await service.CreateAddressAsync(
            seed.CustomerId,
            new SaveCustomerAddressRequest(
                "Oficina",
                "Calle Mercaderes 200",
                "Arequipa",
                "Arequipa",
                "Arequipa",
                "Piso 2",
                false),
            TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True(first.IsPreferred);
        Assert.False(second.IsPreferred);

        var preferred = await service.SetPreferredAsync(
            seed.CustomerId,
            second.CustomerAddressId,
            TestContext.Current.CancellationToken);

        Assert.NotNull(preferred);
        Assert.True(preferred.IsPreferred);

        db.ChangeTracker.Clear();

        var stored = await db.CustomerAddresses
            .AsNoTracking()
            .Where(address => address.CustomerId == seed.CustomerId)
            .OrderBy(address => address.CreatedAt)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, stored.Count);
        Assert.Single(stored, address => address.IsPreferred);
        Assert.False(stored.Single(address =>
            address.CustomerAddressId == first.CustomerAddressId).IsPreferred);
        Assert.True(stored.Single(address =>
            address.CustomerAddressId == second.CustomerAddressId).IsPreferred);
    }

    [Fact]
    public async Task No_modifica_una_direccion_de_otro_cliente()
    {
        var owner = await SeedCustomerAsync(
            "owner@ejemplo.pe",
            internalNotes: null,
            verified: false);

        await using (var firstDb = fixture.CreateContext())
        {
            var firstService = new CustomerProfileService(
                firstDb,
                TimeProvider.System);

            await firstService.CreateAddressAsync(
                owner.CustomerId,
                new SaveCustomerAddressRequest(
                    "Casa",
                    "Dirección del dueño",
                    null,
                    null,
                    null,
                    null,
                    true),
                TestContext.Current.CancellationToken);
        }

        Guid strangerId;

        await using (var seedDb = fixture.CreateContext())
        {
            var stranger = new Customer
            {
                FullName = "Cliente Ajeno",
                Email = "ajeno@ejemplo.pe",
                IsActive = true
            };

            seedDb.Customers.Add(stranger);
            await seedDb.SaveChangesAsync(
                TestContext.Current.CancellationToken);

            strangerId = stranger.CustomerId;
        }

        await using var db = fixture.CreateContext();
        var addressId = await db.CustomerAddresses
            .AsNoTracking()
            .Where(address => address.CustomerId == owner.CustomerId)
            .Select(address => address.CustomerAddressId)
            .SingleAsync(TestContext.Current.CancellationToken);

        var service = new CustomerProfileService(
            db,
            TimeProvider.System);

        var result = await service.UpdateAddressAsync(
            strangerId,
            addressId,
            new SaveCustomerAddressRequest(
                "Intruso",
                "No debe cambiar",
                null,
                null,
                null,
                null,
                true),
            TestContext.Current.CancellationToken);

        Assert.Null(result);

        db.ChangeTracker.Clear();

        var stored = await db.CustomerAddresses
            .AsNoTracking()
            .SingleAsync(
                address => address.CustomerAddressId == addressId,
                TestContext.Current.CancellationToken);

        Assert.Equal("Casa", stored.Label);
        Assert.Equal("Dirección del dueño", stored.AddressLine);
    }

    [Fact]
    public async Task Snapshot_para_M03_usa_solo_datos_publicables_del_cliente()
    {
        var seed = await SeedCustomerAsync(
            "pedido@ejemplo.pe",
            internalNotes: "NO VIAJA A M03",
            verified: true);

        Guid addressId;

        await using (var firstDb = fixture.CreateContext())
        {
            var service = new CustomerProfileService(
                firstDb,
                TimeProvider.System);

            var address = await service.CreateAddressAsync(
                seed.CustomerId,
                new SaveCustomerAddressRequest(
                    "Entrega",
                    "Av. Independencia 500",
                    "Cercado",
                    "Arequipa",
                    "Arequipa",
                    "Puerta azul",
                    true),
                TestContext.Current.CancellationToken);

            Assert.NotNull(address);
            addressId = address.CustomerAddressId;
        }

        await using var db = fixture.CreateContext();
        var reader = new CustomerSnapshotReader(db);

        var snapshot = await reader.GetForOrderAsync(
            seed.CustomerId,
            addressId,
            TestContext.Current.CancellationToken);

        Assert.NotNull(snapshot);
        Assert.Equal("Cliente Perfil", snapshot.FullName);
        Assert.Equal("12345678", snapshot.DocumentNumber);
        Assert.True(snapshot.EmailVerified);
        Assert.Equal("Av. Independencia 500", snapshot.Address.AddressLine);

        var json = JsonSerializer.Serialize(snapshot);

        Assert.DoesNotContain(
            "NO VIAJA A M03",
            json,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "internalNotes",
            json,
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<SeedResult> SeedCustomerAsync(
        string email,
        string? internalNotes,
        bool verified)
    {
        await fixture.CleanAllTablesAsync();

        await using var db = fixture.CreateContext();

        var customer = new Customer
        {
            FullName = "Cliente Perfil",
            Email = email,
            Phone = "+51 900 000 000",
            DocumentType = "dni",
            DocumentNumber = "12345678",
            InternalNotes = internalNotes,
            IsActive = true
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(
            TestContext.Current.CancellationToken);

        var account = new CustomerAccount
        {
            CustomerId = customer.CustomerId,
            PasswordHash = "hash-de-prueba-no-publicable",
            EmailVerifiedAt = verified
                ? DateTimeOffset.UtcNow
                : null
        };

        db.CustomerAccounts.Add(account);
        await db.SaveChangesAsync(
            TestContext.Current.CancellationToken);

        return new SeedResult(
            customer.CustomerId,
            account.CustomerAccountId);
    }

    private sealed record SeedResult(
        Guid CustomerId,
        int AccountId);
}
