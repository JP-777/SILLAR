using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Modules.Sales.Data;
using Sillar.Shared.Configuration;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// Contextos frescos contra PostgreSQL real, para lo que no se puede acreditar sin él.
/// </summary>
/// <remarks>
/// <para>
/// <b>No EF InMemory.</b> Lo que estas pruebas comprueban es traducción y persistencia
/// —que el filtro por cliente va en la consulta, que el <c>CHECK</c> de atribución
/// completa acepta tres nulos, que el aislamiento entre clientes lo hace la base—, y
/// un proveedor en memoria daría verde sin haber tocado nada de eso.
/// </para>
/// <para>
/// <b>No son destructivas, y por eso no exigen la base efímera de la puerta.</b> Cada
/// prueba trabaja dentro de una transacción que se deshace al terminar: ni
/// <c>TRUNCATE</c>, ni <c>DROP</c>, ni filas que sobrevivan. Es lo que permite
/// correrlas contra la base de desarrollo sin estropearla, al contrario que las de
/// M04 y M02.
/// </para>
/// <para>
/// <b>Si el schema <c>sales</c> no está migrado, fallan diciendo exactamente por
/// qué.</b> No se saltan en silencio: una prueba omitida sin declararlo es peor que
/// una roja, porque la roja se ve.
/// </para>
/// </remarks>
public sealed class SalesDbFixture
{
    public string ConnectionString { get; }

    public SalesDbFixture()
    {
        DotEnv.Load();

        ConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings__Default. Copia .env.example como .env en la raíz.");

        using var conexion = new NpgsqlConnection(ConnectionString);
        conexion.Open();

        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables " +
            "WHERE table_schema = 'sales' AND table_name = 'orders');";

        if (comando.ExecuteScalar() is not true)
        {
            var baseDeDatos = new NpgsqlConnectionStringBuilder(ConnectionString).Database;

            throw new InvalidOperationException(
                $"El schema 'sales' no está migrado en '{baseDeDatos}', así que estas " +
                "pruebas de persistencia no pueden correr. En la puerta canónica esto " +
                "ocurrirá hasta que Integración añada Sillar.Modules.Sales a la lista " +
                "de módulos de scripts/verificar.mjs — es la costura §j de " +
                "docs/modules/sales/ESCALADAS-M03.md, pedida y no aplicada.");
        }
    }

    /// <summary>Un contexto nuevo, con nodo y reloj fijos.</summary>
    public SalesDbContext NuevoContexto(TimeProvider? reloj = null)
        => new(
            new DbContextOptionsBuilder<SalesDbContext>()
                .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable(
                    SalesDbContext.MigrationsHistoryTable, SalesDbContext.Schema))
                .Options,
            new NodeIdentity("principal"),
            reloj ?? TimeProvider.System);
}
