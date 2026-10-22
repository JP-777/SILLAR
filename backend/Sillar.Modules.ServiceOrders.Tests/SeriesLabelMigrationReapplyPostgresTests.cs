using Sillar.Modules.ServiceOrders;

namespace Sillar.Modules.ServiceOrders.Tests;

public sealed class SeriesLabelMigrationReapplyPostgresTests
{
    [Fact]
    public Task Reapplying_real_migration_preserves_business_configured_series_label()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            Assert.Equal(3L, await EphemeralDatabase.ScalarAsync<long>(connection,
                "SELECT count(*) FROM service_orders.__migrations"));

            await EphemeralDatabase.ExecuteAsync(connection, """
                UPDATE core.site_settings
                   SET setting_value = 'OS'
                 WHERE setting_key = 'service_orders.series_label'
                """);

            // Solo se manipula la tabla EF de ESTA base efímera. No se toca ninguna DB existente.
            // Hace que EF ejecute físicamente la tercera migración contra un ajuste existente.
            await EphemeralDatabase.ExecuteAsync(connection, """
                DELETE FROM service_orders.__migrations
                 WHERE "MigrationId" = '20261021215500_EnsureServiceOrdersSeriesLabel'
                """);

            Assert.Equal(2L, await EphemeralDatabase.ScalarAsync<long>(connection,
                "SELECT count(*) FROM service_orders.__migrations"));

            await new ServiceOrdersModule().ApplyMigrationsAsync(
                connection, CancellationToken.None);

            Assert.Equal(3L, await EphemeralDatabase.ScalarAsync<long>(connection,
                "SELECT count(*) FROM service_orders.__migrations"));
            Assert.Equal("OS", await EphemeralDatabase.ScalarAsync<string>(connection, """
                SELECT setting_value FROM core.site_settings
                 WHERE setting_key = 'service_orders.series_label'
                """));
            Assert.Equal(1L, await EphemeralDatabase.ScalarAsync<long>(connection, """
                SELECT count(*) FROM core.site_settings
                 WHERE setting_key = 'service_orders.series_label'
                """));
        });
}
