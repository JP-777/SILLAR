using Npgsql;
using Sillar.Core;
using Sillar.Modules.ServiceOrders;
using Sillar.Modules.Services;
using Sillar.Shared.Configuration;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Modules.Tracking.Tests;

internal static class EphemeralDatabase
{
    public static async Task RunAsync(Func<string, Task> body)
    {
        DotEnv.Load();

        var connection =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(connection))
        {
            Assert.Skip(
                "Sin ConnectionStrings__Default: no hay PostgreSQL real para M06.");
        }

        var name = $"sillar_m06_{Guid.NewGuid():N}"[..40];

        var admin = new NpgsqlConnectionStringBuilder(connection)
        {
            Database = "postgres",
            Pooling = false
        }.ConnectionString;

        var own = new NpgsqlConnectionStringBuilder(connection)
        {
            Database = name,
            Pooling = false
        }.ConnectionString;

        await ExecuteAsync(admin, $"CREATE DATABASE \"{name}\"");

        try
        {
            await body(own);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();

            await ExecuteAsync(
                admin,
                $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    public static async Task InstallDependenciesAsync(string connection)
    {
        IModuleMigrations[] modules =
        [
            new CoreModule(),
            new ServicesModule(),
            new ServiceOrdersModule()
        ];

        foreach (var module in modules)
        {
            await module.ApplyMigrationsAsync(
                connection,
                CancellationToken.None);
        }
    }

    public static async Task InstallM06Async(string connection)
    {
        await InstallDependenciesAsync(connection);

        await new TrackingModule().ApplyMigrationsAsync(
            connection,
            CancellationToken.None);
    }

    public static async Task ExecuteAsync(string connection, string sql)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();

        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<T> ScalarAsync<T>(
        string connection,
        string sql)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();

        await using var command = new NpgsqlCommand(sql, db);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public static string InsertOrder(
        Guid id,
        string code = "S-2026-9001",
        string receivedAt = "2026-10-07T10:00:00Z")
        => $"""
            INSERT INTO service_orders.service_orders
                (
                    service_order_id,
                    create_idempotency_key,
                    visible_code,
                    status,
                    customer_name_snapshot,
                    customer_phone_snapshot,
                    received_at,
                    created_by_admin_name,
                    created_by_admin_user_id,
                    created_by_admin_user_home_node,
                    last_status_changed_at,
                    origin_node,
                    row_version
                )
            VALUES
                (
                    '{id}',
                    '{id}',
                    '{code}',
                    'received',
                    'Cliente prueba',
                    '999111222',
                    '{receivedAt}',
                    'Ana Pérez',
                    7,
                    'principal',
                    '{receivedAt}',
                    'principal',
                    1
                );
            """;

    public static string InsertTracking(
        Guid trackingId,
        Guid orderId,
        string priority = "NULL")
        => $"""
            INSERT INTO tracking.order_tracking
                (
                    order_tracking_id,
                    service_order_id,
                    board_priority,
                    pinned,
                    is_active,
                    origin_node,
                    row_version
                )
            VALUES
                (
                    '{trackingId}',
                    '{orderId}',
                    {priority},
                    false,
                    true,
                    'principal',
                    1
                );
            """;
}
