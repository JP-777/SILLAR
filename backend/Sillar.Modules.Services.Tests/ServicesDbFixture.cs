using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Modules.Services.Data;
using Sillar.Shared.Configuration;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Modules.Services.Tests;

/// <summary>
/// Base PostgreSQL real para las pruebas de M05a. Misma regla que CRM y CMS:
/// son destructivas, así que solo corren contra la base efímera de
/// <c>scripts/verificar.mjs</c>, cuyo nombre llega en <c>SILLAR_VERIFY_DATABASE</c>.
/// </summary>
/// <remarks>
/// Asume CORE ya migrado —M05a depende de él: colaciones y <c>core.media_assets</c>—,
/// como en una instalación real. El schema <c>services</c> lo crea la propia
/// migración de M05a.
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

    /// <summary>Migra M05a (idempotente) y deja la tabla vacía.</summary>
    public async Task PrepararAsync(CancellationToken ct)
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync(ct);
        await db.Database.ExecuteSqlRawAsync("TRUNCATE services.service_entries RESTART IDENTITY;", ct);
    }

    public async Task<string> EscalarAsync(string sql, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(ConnectionString);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexion);
        return Convert.ToString(await comando.ExecuteScalarAsync(ct)) ?? "";
    }

    public async Task EjecutarAsync(string sql, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(ConnectionString);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Una ficha de medio activa en CORE, por SQL: M05a no escribe en core.</summary>
    public async Task<Guid> CrearMedioAsync(CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        await EjecutarAsync(
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
