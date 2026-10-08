using Npgsql;
using Sillar.Core;
using Sillar.Modules.Services;
using Sillar.Shared.Configuration;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Modules.ServiceOrders.Tests;

internal static class EphemeralDatabase
{
    public static async Task RunAsync(Func<string, Task> body)
    {
        DotEnv.Load();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connection))
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay PostgreSQL real para M05b.");
        }

        var name = $"sillar_m05b_{Guid.NewGuid():N}"[..40];
        var admin = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres", Pooling = false }.ConnectionString;
        var own = new NpgsqlConnectionStringBuilder(connection) { Database = name, Pooling = false }.ConnectionString;
        await ExecuteAsync(admin, $"CREATE DATABASE \"{name}\"");
        try
        {
            await body(own);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    public static async Task InstallAsync(string connection, params IModuleMigrations[] modules)
    {
        foreach (var module in modules)
        {
            await module.ApplyMigrationsAsync(connection, CancellationToken.None);
        }
    }

    public static Task InstallM05bAsync(string connection)
        => InstallAsync(connection, new CoreModule(), new ServicesModule(), new ServiceOrdersModule());

    public static async Task ExecuteAsync(string connection, string sql)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<T> ScalarAsync<T>(string connection, string sql)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public static string Script(string file)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "database", "modules", "service_orders", file);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        throw new FileNotFoundException($"No se encontró database/modules/service_orders/{file}.");
    }

    public static string InsertOrder(Guid id, string code = "S-2026-0001", string promisedAt = "NULL")
        => $"""
            INSERT INTO service_orders.service_orders
                (service_order_id, visible_code, status, create_idempotency_key,
                 customer_name_snapshot, customer_phone_snapshot, received_at, promised_at,
                 created_by_admin_name, created_by_admin_user_id, created_by_admin_user_home_node,
                 last_status_changed_at, origin_node, row_version)
            VALUES ('{id}', '{code}', 'received', '{id}', 'Cliente', '999111222',
                    '2026-10-07T10:00:00Z', {promisedAt},
                    'Ana Pérez', 7, 'principal', '2026-10-07T10:00:00Z', 'principal', 1)
            """;
}
