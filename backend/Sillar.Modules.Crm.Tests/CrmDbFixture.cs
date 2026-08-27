using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Modules.Crm.Data;
using Sillar.Shared.Configuration;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Crm.Tests;

/// <summary>
/// Provee la cadena de conexión y herramientas para crear contextos frescos.
/// </summary>
/// <remarks>
/// Carga .env para obtener la cadena de conexión. Asume que el schema crm ya
/// está migrado. Cada prueba crea su propio <see cref="CrmDbContext"/> y limpia
/// las tablas al inicio.
///
/// Las pruebas de persistencia CRM son destructivas (TRUNCATE, DROP SCHEMA):
/// solo pueden ejecutarse contra la base efímera creada por la puerta
/// <c>scripts/verificar.mjs</c>, cuyo nombre exacto llega en
/// <c>SILLAR_VERIFY_DATABASE</c>. El constructor comprueba que
/// <c>Database</c> en la cadena de conexión coincida con ese valor (regla
/// única, compartida con CMS) y falla antes de tocar nada.
/// </remarks>
public sealed class CrmDbFixture
{
    public string ConnectionString { get; }
    public NodeIdentity Node { get; } = new("principal");

    public CrmDbFixture()
    {
        DotEnv.Load();
        ConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings__Default en .env");

        // La única autoridad para el nombre de la base es la puerta
        // scripts/verificar.mjs, que pasa SILLAR_VERIFY_DATABASE al proceso
        // backend. Las pruebas de persistencia CRM son destructivas
        // (TRUNCATE crm.*, DROP SCHEMA crm CASCADE) y solo pueden ejecutarse
        // contra esa base efímera. Se comprueba coincidencia exacta entre
        // SILLAR_VERIFY_DATABASE y Database de la cadena —no un prefijo—
        // para que la regla se defina una sola vez, aquí y en CMS.
        var verifyDb = Environment.GetEnvironmentVariable("SILLAR_VERIFY_DATABASE");
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);

        if (string.IsNullOrWhiteSpace(verifyDb))
        {
            throw new InvalidOperationException(
                "Las pruebas de persistencia CRM son destructivas y solo pueden ejecutarse " +
                "contra la base efímera creada por scripts/verificar.mjs. " +
                "Falta SILLAR_VERIFY_DATABASE en el entorno.");
        }

        if (!string.Equals(builder.Database, verifyDb, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Las pruebas de persistencia CRM son destructivas y solo pueden ejecutarse " +
                "contra la base efímera creada por scripts/verificar.mjs. " +
                $"SILLAR_VERIFY_DATABASE='{verifyDb}' pero la conexión apunta a '{builder.Database}'.");
        }
    }

    public CrmDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable(
                CrmDbContext.MigrationsHistoryTable,
                CrmDbContext.Schema))
            .Options;

        return new CrmDbContext(options, Node, TimeProvider.System);
    }

    public async Task CleanAllTablesAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            "TRUNCATE crm.contact_messages, crm.customer_tokens, crm.customer_sessions, crm.customer_accounts, crm.customer_addresses, crm.customers RESTART IDENTITY CASCADE;");
    }

    public async Task EnsureMigratedAsync()
    {
        // Reproduce la precondición real de instalación: nada precrea el
        // schema crm. MigrateAsync aplica CrmInitial, cuyo primer paso es
        // EnsureSchema("crm"). Precrearlo aquí ocultaría una regresión si
        // CrmInitial dejara de ejecutar EnsureSchema.
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
    }
}
