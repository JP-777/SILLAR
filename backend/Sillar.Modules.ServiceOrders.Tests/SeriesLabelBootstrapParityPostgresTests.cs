namespace Sillar.Modules.ServiceOrders.Tests;

/// <summary>Regresión aislada de la diferencia encontrada en §35.</summary>
public sealed class SeriesLabelBootstrapParityPostgresTests
{
    [Fact]
    public Task Migrations_alone_register_required_private_pending_label()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var row = await EphemeralDatabase.ScalarAsync<string>(connection,
                """
                SELECT setting_value || '|' || value_type || '|' ||
                       is_public::text || '|' || is_active::text
                  FROM core.site_settings
                 WHERE setting_key = 'service_orders.series_label'
                """);

            Assert.Equal("PENDIENTE_DEFINIR|text|false|true", row);
            Assert.Equal(3L, await EphemeralDatabase.ScalarAsync<long>(connection,
                "SELECT count(*) FROM service_orders.__migrations"));
        });

    [Fact]
    public Task Subsequent_seed_and_migration_do_not_override_business_label()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);
            await EphemeralDatabase.ExecuteAsync(connection,
                """
                UPDATE core.site_settings
                   SET setting_value = 'OS', is_public = false
                 WHERE setting_key = 'service_orders.series_label'
                """);

            await EphemeralDatabase.ExecuteAsync(connection,
                EphemeralDatabase.Script("02_seed.sql"));
            await EphemeralDatabase.ExecuteAsync(connection,
                EphemeralDatabase.Script("02_seed.sql"));
            await new ServiceOrdersModule().ApplyMigrationsAsync(
                connection, CancellationToken.None);

            Assert.Equal("OS", await EphemeralDatabase.ScalarAsync<string>(connection,
                """
                SELECT setting_value FROM core.site_settings
                 WHERE setting_key = 'service_orders.series_label'
                """));
            Assert.Equal(1L, await EphemeralDatabase.ScalarAsync<long>(connection,
                """
                SELECT count(*) FROM core.site_settings
                 WHERE setting_key = 'service_orders.series_label'
                """));
        });
}
