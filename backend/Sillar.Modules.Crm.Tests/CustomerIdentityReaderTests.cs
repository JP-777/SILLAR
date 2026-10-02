using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Crm.Domain;
using Sillar.Modules.Crm.Profiles;

namespace Sillar.Modules.Crm.Tests;

/// <summary>
/// La identidad mínima del cliente que leen otros módulos —la bandeja B2B de
/// M07— para enseñar a una persona en vez de un identificador.
/// </summary>
[Collection("CrmDb")]
public sealed class CustomerIdentityReaderTests(
    CrmDbFixture fixture) : IClassFixture<CrmDbFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // --- Lectura individual ---------------------------------------------------

    [Fact]
    public async Task Un_cliente_activo_devuelve_nombre_correo_y_telefono()
    {
        await fixture.CleanAllTablesAsync();
        var id = await SeedAsync("ana@ejemplo.pe", "Ana Quispe", "+51 900 000 010", withAccount: true);

        await using var db = fixture.CreateContext();
        var identidad = await new CustomerIdentityReader(db).GetAsync(id, Ct);

        Assert.NotNull(identidad);
        Assert.Equal(new CustomerIdentity(id, "Ana Quispe", "ana@ejemplo.pe", "+51 900 000 010"), identidad);
    }

    [Fact]
    public async Task Una_ficha_sin_cuenta_tambien_tiene_identidad()
    {
        await fixture.CleanAllTablesAsync();
        var id = await SeedAsync("mostrador@ejemplo.pe", "Cliente Mostrador", phone: null, withAccount: false);

        await using var db = fixture.CreateContext();
        var identidad = await new CustomerIdentityReader(db).GetAsync(id, Ct);

        Assert.NotNull(identidad);
        Assert.Null(identidad.Phone);
    }

    [Fact]
    public async Task Un_cliente_inexistente_devuelve_null()
    {
        await fixture.CleanAllTablesAsync();

        await using var db = fixture.CreateContext();
        Assert.Null(await new CustomerIdentityReader(db).GetAsync(Guid.CreateVersion7(), Ct));
    }

    [Theory]
    [InlineData("baja")]
    [InlineData("bloqueo")]
    public async Task Un_cliente_de_baja_o_bloqueado_devuelve_null(string estado)
    {
        await fixture.CleanAllTablesAsync();
        var id = await SeedAsync($"{estado}@ejemplo.pe", "Cliente Inactivo", phone: null, withAccount: true);
        await DesactivarAsync(id, estado);

        await using var db = fixture.CreateContext();
        Assert.Null(await new CustomerIdentityReader(db).GetAsync(id, Ct));
    }

    [Fact]
    public async Task No_expone_notas_internas()
    {
        await fixture.CleanAllTablesAsync();
        var id = await SeedAsync(
            "notas@ejemplo.pe", "Cliente Notas", phone: null, withAccount: true, internalNotes: "NO VIAJA A M07");

        await using var db = fixture.CreateContext();
        var reader = new CustomerIdentityReader(db);
        var json = JsonSerializer.Serialize(await reader.GetAsync(id, Ct))
            + JsonSerializer.Serialize(await reader.GetManyAsync([id], Ct));

        Assert.DoesNotContain("NO VIAJA A M07", json, StringComparison.Ordinal);
        Assert.DoesNotContain("internalNotes", json, StringComparison.OrdinalIgnoreCase);
    }

    // --- Lectura por lotes ----------------------------------------------------

    [Fact]
    public async Task Por_lotes_devuelve_solo_los_activos_existentes_una_vez_cada_uno()
    {
        await fixture.CleanAllTablesAsync();
        var ana = await SeedAsync("ana@ejemplo.pe", "Ana Quispe", phone: null, withAccount: true);
        var luis = await SeedAsync("luis@ejemplo.pe", "Luis Mamani", phone: null, withAccount: false);
        var baja = await SeedAsync("baja@ejemplo.pe", "Cliente Baja", phone: null, withAccount: true);
        await DesactivarAsync(baja, "baja");
        var inexistente = Guid.CreateVersion7();

        await using var db = fixture.CreateContext();
        var identidades = await new CustomerIdentityReader(db).GetManyAsync([ana, luis, ana, baja, inexistente], Ct);

        Assert.Equal(2, identidades.Count);
        Assert.Equal("Ana Quispe", identidades[ana].FullName);
        Assert.Equal("Luis Mamani", identidades[luis].FullName);
        Assert.False(identidades.ContainsKey(baja));
        Assert.False(identidades.ContainsKey(inexistente));
    }

    [Fact]
    public async Task Por_lotes_una_coleccion_vacia_devuelve_un_diccionario_vacio()
    {
        await using var db = fixture.CreateContext();
        Assert.Empty(await new CustomerIdentityReader(db).GetManyAsync([], Ct));
    }

    // --- Forma del contrato ---------------------------------------------------

    [Fact]
    public void El_contrato_no_pide_direccion()
    {
        var parametros = typeof(ICustomerIdentityReader)
            .GetMethods()
            .SelectMany(m => m.GetParameters())
            .Select(p => p.Name ?? string.Empty);

        Assert.DoesNotContain(parametros, n => n.Contains("address", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void La_identidad_solo_lleva_id_nombre_correo_y_telefono()
    {
        var propiedades = typeof(CustomerIdentity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Order()
            .ToArray();

        Assert.Equal(["CustomerId", "Email", "FullName", "Phone"], propiedades);
    }

    [Fact]
    public void El_contrato_es_solo_lectura()
    {
        var nombres = typeof(ICustomerIdentityReader).GetMethods().Select(m => m.Name).Order().ToArray();
        Assert.Equal(["GetAsync", "GetManyAsync"], nombres);
    }

    [Fact]
    public void Los_contratos_no_arrastran_la_implementacion_de_CRM_ni_EF()
    {
        // Quien consume ICustomerIdentityReader referencia Sillar.Modules.Crm.Contracts.
        // Si ese ensamblado dependiera de Sillar.Modules.Crm o de EF Core, el
        // consumidor podría llegar a las tablas de CRM sin pasar por el contrato.
        var referencias = typeof(ICustomerIdentityReader).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("Sillar.Modules.Crm", referencias);
        Assert.DoesNotContain(referencias, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referencias, n => n.StartsWith("Npgsql", StringComparison.Ordinal));
    }

    // --- Siembra --------------------------------------------------------------

    private async Task<Guid> SeedAsync(
        string email,
        string fullName,
        string? phone,
        bool withAccount,
        string? internalNotes = null)
    {
        await using var db = fixture.CreateContext();

        var customer = new Customer
        {
            FullName = fullName,
            Email = email,
            Phone = phone,
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
                EmailVerifiedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(Ct);
        }

        return customer.CustomerId;
    }

    private async Task DesactivarAsync(Guid customerId, string estado)
    {
        await using var db = fixture.CreateContext();
        // Los dos estados que ck_customers_lifecycle_state admite además de ACTIVA.
        var customer = await db.Customers.SingleAsync(c => c.CustomerId == customerId, Ct);
        customer.IsActive = false;
        if (estado == "baja") customer.DeactivatedAt = DateTimeOffset.UtcNow;
        else customer.BlockedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(Ct);
    }
}
