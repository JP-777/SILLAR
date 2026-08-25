using Microsoft.EntityFrameworkCore;
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
