using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Core;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders;
using Sillar.Modules.Services.Data;
using Sillar.Modules.Tracking;
using Sillar.Shared.Configuration;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Modules.Services.Tests;

/// <summary>
/// Base PostgreSQL real para las pruebas de M05a. Misma regla que CRM y CMS:
/// son destructivas, así que solo corren contra la base efímera de
/// <c>scripts/verificar.mjs</c>, cuyo nombre llega en <c>SILLAR_VERIFY_DATABASE</c>.
/// </summary>
/// <remarks>
/// Las pruebas ordinarias asumen CORE **y M05a** ya migrados por la etapa 4 de
/// la puerta, como CRM y Sales. No migran por su cuenta: si lo hicieran, quitar
/// M05a de la lista de migraciones de la puerta no rompería nada y la costura
/// no se podría comprobar. El ciclo destructivo es la única excepción: instala
/// y reinstala dentro de una base propia, y nunca desmonta schemas de la base
/// compartida de la puerta.
/// </remarks>
public sealed class ServicesDbFixture
{
    public string ConnectionString { get; }

    public ServicesDbFixture()
    {
        DotEnv.Load();
        ConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings__Default en .env");

        var verifyDb = Environment.GetEnvironmentVariable("SILLAR_VERIFY_DATABASE");
        var database = new NpgsqlConnectionStringBuilder(ConnectionString).Database;

        if (string.IsNullOrWhiteSpace(verifyDb) || !string.Equals(database, verifyDb, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Las pruebas de persistencia de M05a son destructivas y solo pueden ejecutarse contra la base " +
                $"efímera de scripts/verificar.mjs. SILLAR_VERIFY_DATABASE='{verifyDb}', conexión a '{database}'.");
        }
    }

    public ServicesDbContext CreateContext()
        => new(PersistenciaDeModulo.Opciones<ServicesDbContext>(
            ConnectionString, ServicesDbContext.Schema, ServicesDbContext.MigrationsHistoryTable));

    /// <summary>Exige el schema que dejó la puerta y vacía la tabla.</summary>
    public async Task PrepararAsync(CancellationToken ct)
    {
        var database = new NpgsqlConnectionStringBuilder(ConnectionString).Database;

        if (await EscalarAsync("SELECT count(*) FROM pg_namespace WHERE nspname = 'services'", ct) != "1")
        {
            throw new InvalidOperationException(
                $"El schema 'services' no está migrado en '{database}', así que las pruebas de persistencia de M05a " +
                "no pueden correr. La puerta lo migra en la etapa 4 solo si Sillar.Modules.Services está en la lista " +
                "de módulos de scripts/verificar.mjs (costura de M05a, ESCALADAS.md E3).");
        }

        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE services.service_entries RESTART IDENTITY;", ct);
    }

    /// <summary>
    /// Crea una base vacía propia para una prueba de ciclo y la elimina al
    /// terminar, incluso si la aserción falla.
    /// </summary>
    /// <remarks>
    /// La base verificada de la puerta solo aporta el servidor y las
    /// credenciales. Ningún schema ni dato suyo se desmonta para preparar el
    /// caso positivo de M05a.
    /// </remarks>
    public async Task ConBaseDeCicloAisladaAsync(Func<string, Task> cuerpo, CancellationToken ct)
    {
        var nombre = $"sillar_services_ciclo_{Guid.NewGuid():N}"[..48];
        var admin = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = "postgres",
            Pooling = false,
        }.ConnectionString;
        var aislada = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = nombre,
            Pooling = false,
        }.ConnectionString;

        try
        {
            // CREATE también queda bajo el finally: si el servidor alcanza a
            // crear la base pero el cliente recibe una cancelación, el nombre
            // generado sigue retirándose de forma segura.
            await EjecutarEnAsync(admin, $"CREATE DATABASE \"{nombre}\"", ct);
            await cuerpo(aislada);
        }
        finally
        {
            // La cancelación de la prueba no debe impedir retirar su base propia.
            await EjecutarEnAsync(
                admin,
                $"DROP DATABASE IF EXISTS \"{nombre}\" WITH (FORCE)",
                CancellationToken.None);
        }
    }

    /// <summary>Instala la cadena real CORE → M05a → M05b → M06.</summary>
    public static async Task InstalarCadenaDeServiciosAsync(string connectionString, CancellationToken ct)
    {
        await new CoreModule().ApplyMigrationsAsync(connectionString, ct);
        await new ServicesModule().ApplyMigrationsAsync(connectionString, ct);
        await new ServiceOrdersModule().ApplyMigrationsAsync(connectionString, ct);
        await new TrackingModule().ApplyMigrationsAsync(connectionString, ct);
    }

    /// <summary>Reinstala únicamente M05a después de retirar sus dependientes.</summary>
    public static Task InstalarServicesAsync(string connectionString, CancellationToken ct)
        => new ServicesModule().ApplyMigrationsAsync(connectionString, ct);

    public async Task<string> EscalarAsync(string sql, CancellationToken ct)
        => await EscalarEnAsync(ConnectionString, sql, ct);

    public static async Task<string> EscalarEnAsync(string connectionString, string sql, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(connectionString);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexion);
        return Convert.ToString(await comando.ExecuteScalarAsync(ct)) ?? "";
    }

    public async Task EjecutarAsync(string sql, CancellationToken ct)
        => await EjecutarEnAsync(ConnectionString, sql, ct);

    public static async Task EjecutarEnAsync(string connectionString, string sql, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(connectionString);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Una ficha de medio activa en CORE, por SQL: M05a no escribe en core.</summary>
    public async Task<Guid> CrearMedioAsync(CancellationToken ct)
        => await CrearMedioEnAsync(ConnectionString, ct);

    public static async Task<Guid> CrearMedioEnAsync(string connectionString, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        await EjecutarEnAsync(connectionString,
            "INSERT INTO core.media_assets (media_asset_id, origin_node, stored_name, relative_path, mime_type, size_bytes) "
            + $"VALUES ('{id}', 'principal', '{id}.png', '2026/10/{id}.png', 'image/png', 10)", ct);
        return id;
    }
}

/// <summary>Doble de IMediaStorage: la URL pública que daría CORE, sin disco.</summary>
internal sealed class MediosDePrueba : IMediaStorage
{
    public HashSet<Guid> Activos { get; } = [];

    public Task<MediaAsset> SaveAsync(Stream content, string originalName, string ownerModuleCode, CancellationToken ct)
        => throw new NotSupportedException("M05a no sube archivos.");

    public Task<bool> DeleteAsync(Guid mediaAssetId, CancellationToken ct)
        => throw new NotSupportedException("M05a no da de baja archivos.");

    public string? GetPublicUrl(Guid mediaAssetId)
        => Activos.Contains(mediaAssetId) ? $"/media/2026/10/{mediaAssetId}.png" : null;
}

[CollectionDefinition("ServicesDb", DisableParallelization = true)]
public sealed class ServicesDbCollection : ICollectionFixture<ServicesDbFixture>;
