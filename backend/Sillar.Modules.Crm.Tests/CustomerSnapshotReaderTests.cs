using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Crm.Domain;
using Sillar.Modules.Crm.Dtos;
using Sillar.Modules.Crm.Profiles;

namespace Sillar.Modules.Crm.Tests;

/// <summary>
/// El contrato de instantánea que consume M03, en sus dos variantes: con
/// dirección (pedido con entrega) y sin dirección (recojo en tienda).
/// </summary>
[Collection("CrmDb")]
public sealed class CustomerSnapshotReaderTests(
    CrmDbFixture fixture) : IClassFixture<CrmDbFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // --- Sin dirección ------------------------------------------------------

    [Fact]
    public async Task Sin_direccion_devuelve_los_datos_del_cliente_aunque_no_tenga_ninguna_direccion()
    {
        await fixture.CleanAllTablesAsync();
        var customerId = await SeedCustomerAsync("recojo@ejemplo.pe", withAccount: true, verified: true);

        await using var db = fixture.CreateContext();
        var snapshot = await new CustomerSnapshotReader(db).GetForOrderAsync(customerId, Ct);

        Assert.NotNull(snapshot);
        Assert.Equal(customerId, snapshot.CustomerId);
        Assert.Equal("Cliente Recojo", snapshot.FullName);
        Assert.Equal("recojo@ejemplo.pe", snapshot.Email);
        Assert.Equal("+51 900 000 001", snapshot.Phone);
        Assert.Equal("dni", snapshot.DocumentType);
        Assert.Matches("^[0-9]{8}$", snapshot.DocumentNumber!);
        Assert.True(snapshot.EmailVerified);
    }

    [Fact]
    public async Task Sin_direccion_refleja_correo_sin_verificar()
    {
        await fixture.CleanAllTablesAsync();
        var customerId = await SeedCustomerAsync("sinverificar@ejemplo.pe", withAccount: true, verified: false);

        await using var db = fixture.CreateContext();
        var snapshot = await new CustomerSnapshotReader(db).GetForOrderAsync(customerId, Ct);

        Assert.NotNull(snapshot);
        Assert.False(snapshot.EmailVerified);
    }

    [Fact]
    public void Sin_direccion_no_expone_ninguna_propiedad_de_direccion()
    {
        var nombres = typeof(CustomerOrderContactSnapshot)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain(nombres, n => n.Contains("Address", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nombres, n => n.Contains("InternalNotes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Sin_direccion_no_filtra_notas_internas()
    {
        await fixture.CleanAllTablesAsync();
        var customerId = await SeedCustomerAsync(
            "notas@ejemplo.pe", withAccount: true, verified: true, internalNotes: "NO VIAJA A M03");

        await using var db = fixture.CreateContext();
        var snapshot = await new CustomerSnapshotReader(db).GetForOrderAsync(customerId, Ct);
        var json = JsonSerializer.Serialize(snapshot);

        Assert.DoesNotContain("NO VIAJA A M03", json, StringComparison.Ordinal);
    }

    // --- Con dirección: compatibilidad con 1.0.0 ------------------------------

    [Fact]
    public async Task Con_direccion_valida_devuelve_cliente_y_direccion_como_en_1_0_0()
    {
        await fixture.CleanAllTablesAsync();
        var customerId = await SeedCustomerAsync("entrega@ejemplo.pe", withAccount: true, verified: true);
        var addressId = await AddAddressAsync(customerId, "Av. Independencia 500");

        await using var db = fixture.CreateContext();
        var snapshot = await new CustomerSnapshotReader(db).GetForOrderAsync(customerId, addressId, Ct);

        Assert.NotNull(snapshot);
        Assert.Equal(customerId, snapshot.CustomerId);
        Assert.Equal(addressId, snapshot.Address.CustomerAddressId);
        Assert.Equal("Av. Independencia 500", snapshot.Address.AddressLine);
    }

    [Fact]
    public async Task Con_direccion_inexistente_devuelve_null_y_no_cae_en_la_variante_sin_direccion()
    {
        await fixture.CleanAllTablesAsync();
        var customerId = await SeedCustomerAsync("sindir@ejemplo.pe", withAccount: true, verified: true);

        await using var db = fixture.CreateContext();
        var reader = new CustomerSnapshotReader(db);

        Assert.Null(await reader.GetForOrderAsync(customerId, Guid.CreateVersion7(), Ct));
        // El mismo cliente sí es válido sin dirección: la nulidad de arriba es
        // por la dirección, no por el cliente.
        Assert.NotNull(await reader.GetForOrderAsync(customerId, Ct));
    }

    [Fact]
    public async Task Con_direccion_ajena_devuelve_null()
    {
        await fixture.CleanAllTablesAsync();
        var propietario = await SeedCustomerAsync("propietario@ejemplo.pe", withAccount: true, verified: true);
        var otro = await SeedCustomerAsync("otro@ejemplo.pe", withAccount: true, verified: true);
        var direccionDelPropietario = await AddAddressAsync(propietario, "Calle Mercaderes 200");

        await using var db = fixture.CreateContext();
        var reader = new CustomerSnapshotReader(db);

        Assert.Null(await reader.GetForOrderAsync(otro, direccionDelPropietario, Ct));
        Assert.NotNull(await reader.GetForOrderAsync(propietario, direccionDelPropietario, Ct));
    }

    [Fact]
    public async Task Con_direccion_de_baja_devuelve_null()
    {
        await fixture.CleanAllTablesAsync();
        var customerId = await SeedCustomerAsync("baja-dir@ejemplo.pe", withAccount: true, verified: true);
        var addressId = await AddAddressAsync(customerId, "Av. Goyeneche 300");

        await using (var db = fixture.CreateContext())
        {
            Assert.True(await new CustomerProfileService(db, TimeProvider.System)
                .DeleteAddressAsync(customerId, addressId, Ct));
        }

        await using var lectura = fixture.CreateContext();
        Assert.Null(await new CustomerSnapshotReader(lectura).GetForOrderAsync(customerId, addressId, Ct));
    }

    // --- Guardas comunes del cliente -----------------------------------------

    [Fact]
    public async Task Cliente_inexistente_devuelve_null_en_las_dos_variantes()
    {
        await fixture.CleanAllTablesAsync();

        await using var db = fixture.CreateContext();
        var reader = new CustomerSnapshotReader(db);
        var inexistente = Guid.CreateVersion7();

        Assert.Null(await reader.GetForOrderAsync(inexistente, Ct));
        Assert.Null(await reader.GetForOrderAsync(inexistente, Guid.CreateVersion7(), Ct));
    }

    [Theory]
    [InlineData("baja")]
    [InlineData("bloqueo")]
    public async Task Cliente_de_baja_o_bloqueado_devuelve_null_en_las_dos_variantes(string estado)
    {
        await fixture.CleanAllTablesAsync();
        var customerId = await SeedCustomerAsync($"{estado}@ejemplo.pe", withAccount: true, verified: true);
        var addressId = await AddAddressAsync(customerId, "Av. Ejército 100");

        await using (var db = fixture.CreateContext())
        {
            // Los dos estados que ck_customers_lifecycle_state admite además de ACTIVA.
            var customer = await db.Customers.SingleAsync(c => c.CustomerId == customerId, Ct);
            customer.IsActive = false;
            if (estado == "baja") customer.DeactivatedAt = DateTimeOffset.UtcNow;
            else customer.BlockedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(Ct);
        }

        await using var lectura = fixture.CreateContext();
        var reader = new CustomerSnapshotReader(lectura);

        Assert.Null(await reader.GetForOrderAsync(customerId, Ct));
        Assert.Null(await reader.GetForOrderAsync(customerId, addressId, Ct));
    }

    [Fact]
    public async Task Ficha_sin_cuenta_devuelve_null_en_las_dos_variantes()
    {
        await fixture.CleanAllTablesAsync();
        var customerId = await SeedCustomerAsync("mostrador@ejemplo.pe", withAccount: false, verified: false);
        var addressId = await AddAddressAsync(customerId, "Av. Parra 400");

        await using var db = fixture.CreateContext();
        var reader = new CustomerSnapshotReader(db);

        Assert.Null(await reader.GetForOrderAsync(customerId, Ct));
        Assert.Null(await reader.GetForOrderAsync(customerId, addressId, Ct));
    }

    // --- Cambios posteriores e inmutabilidad ----------------------------------

    [Fact]
    public async Task Un_cambio_posterior_del_perfil_no_altera_la_instantanea_ya_tomada()
    {
        await fixture.CleanAllTablesAsync();
        var customerId = await SeedCustomerAsync("cambio@ejemplo.pe", withAccount: true, verified: true);
        var addressId = await AddAddressAsync(customerId, "Av. Independencia 500");

        CustomerOrderContactSnapshot? sinDireccion;
        CustomerOrderSnapshot? conDireccion;
        await using (var db = fixture.CreateContext())
        {
            var reader = new CustomerSnapshotReader(db);
            sinDireccion = await reader.GetForOrderAsync(customerId, Ct);
            conDireccion = await reader.GetForOrderAsync(customerId, addressId, Ct);
        }
        Assert.NotNull(sinDireccion);
        Assert.NotNull(conDireccion);

        await using (var db = fixture.CreateContext())
        {
            var service = new CustomerProfileService(db, TimeProvider.System);
            var perfil = await service.UpdateAsync(
                customerId,
                new UpdateCustomerProfileRequest(
                    "Cliente Renombrado", "cambiado@ejemplo.pe", "+51 911 111 111", "dni", "11112222"),
                Ct);
            Assert.Equal(CustomerProfileUpdateOutcome.Updated, perfil.Outcome);

            var direccion = await service.UpdateAddressAsync(
                customerId,
                addressId,
                new SaveCustomerAddressRequest("Nueva", "Calle Nueva 999", null, null, null, null, true),
                Ct);
            Assert.NotNull(direccion);
        }

        // Lo ya tomado conserva los datos del momento.
        Assert.Equal("Cliente Recojo", sinDireccion.FullName);
        Assert.Equal("cambio@ejemplo.pe", sinDireccion.Email);
        Assert.True(sinDireccion.EmailVerified);
        Assert.Equal("Cliente Recojo", conDireccion.FullName);
        Assert.Equal("Av. Independencia 500", conDireccion.Address.AddressLine);

        // Una lectura nueva ve el estado actual: cambiar el correo anula la
        // verificación, y así lo refleja.
        await using var lectura = fixture.CreateContext();
        var reader2 = new CustomerSnapshotReader(lectura);
        var nueva = await reader2.GetForOrderAsync(customerId, Ct);
        Assert.NotNull(nueva);
        Assert.Equal("Cliente Renombrado", nueva.FullName);
        Assert.Equal("cambiado@ejemplo.pe", nueva.Email);
        Assert.False(nueva.EmailVerified);
        var nuevaConDireccion = await reader2.GetForOrderAsync(customerId, addressId, Ct);
        Assert.NotNull(nuevaConDireccion);
        Assert.Equal("Calle Nueva 999", nuevaConDireccion.Address.AddressLine);
    }

    [Theory]
    [InlineData(typeof(CustomerOrderContactSnapshot))]
    [InlineData(typeof(CustomerOrderSnapshot))]
    [InlineData(typeof(CustomerOrderAddressSnapshot))]
    public void Las_instantaneas_no_se_pueden_modificar_despues_de_creadas(Type tipo)
    {
        foreach (var propiedad in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var setter = propiedad.SetMethod;
            if (setter is null)
            {
                continue;
            }

            var soloInit = setter.ReturnParameter
                .GetRequiredCustomModifiers()
                .Contains(typeof(IsExternalInit));
            Assert.True(soloInit, $"{tipo.Name}.{propiedad.Name} tiene un setter público mutable.");
        }
    }

    // --- Siembra -------------------------------------------------------------

    private async Task<Guid> SeedCustomerAsync(
        string email,
        bool withAccount,
        bool verified,
        string? internalNotes = null)
    {
        await using var db = fixture.CreateContext();

        var customer = new Customer
        {
            FullName = "Cliente Recojo",
            Email = email,
            Phone = "+51 900 000 001",
            DocumentType = "dni",
            // Un documento por ficha: uq_customers_document no admite repetidos.
            DocumentNumber = DocumentoEstable(email),
            InternalNotes = internalNotes,
            IsActive = true
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(Ct);

        if (withAccount)
        {
            db.CustomerAccounts.Add(new CustomerAccount
            {
                CustomerId = customer.CustomerId,
                PasswordHash = "hash-de-prueba-no-publicable",
                EmailVerifiedAt = verified ? DateTimeOffset.UtcNow : null
            });
            await db.SaveChangesAsync(Ct);
        }

        return customer.CustomerId;
    }

    /// <summary>Ocho dígitos derivados del correo: estable entre ejecuciones.</summary>
    private static string DocumentoEstable(string email)
    {
        var h = 17;
        foreach (var c in email) h = unchecked(h * 31 + c);
        return (10_000_000 + (int)((uint)h % 89_999_999u)).ToString();
    }

    private async Task<Guid> AddAddressAsync(Guid customerId, string addressLine)
    {
        await using var db = fixture.CreateContext();
        var address = await new CustomerProfileService(db, TimeProvider.System).CreateAddressAsync(
            customerId,
            new SaveCustomerAddressRequest("Entrega", addressLine, "Cercado", "Arequipa", "Arequipa", null, true),
            Ct);
        Assert.NotNull(address);
        return address.CustomerAddressId;
    }
}
