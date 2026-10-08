using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.ServiceOrders.Application;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Data;
using Sillar.Modules.ServiceOrders.Numbering;
using Sillar.Modules.Services.Contracts;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Tests;

public sealed class SeriesConfigurationPostgresTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 8, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public Task Seed_registers_private_pending_series_setting_idempotently()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var seed = EphemeralDatabase.Script("02_seed.sql");
            await EphemeralDatabase.ExecuteAsync(connection, seed);
            await EphemeralDatabase.ExecuteAsync(connection, seed);

            Assert.Equal(
                1L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    """
                    SELECT count(*)
                      FROM core.site_settings
                     WHERE setting_key = 'service_orders.series_label'
                    """));

            Assert.Equal(
                "PENDIENTE_DEFINIR",
                await EphemeralDatabase.ScalarAsync<string>(
                    connection,
                    """
                    SELECT setting_value
                      FROM core.site_settings
                     WHERE setting_key = 'service_orders.series_label'
                    """));

            Assert.False(
                await EphemeralDatabase.ScalarAsync<bool>(
                    connection,
                    """
                    SELECT is_public
                      FROM core.site_settings
                     WHERE setting_key = 'service_orders.series_label'
                    """));
        });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PENDIENTE_DEFINIR")]
    public Task Missing_or_pending_series_label_is_clean_invalid_and_reserves_nothing(
        string? seriesLabel)
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = new Showcase(
                new ServiceSnapshot(
                    501,
                    "Servicio de prueba",
                    "servicio-prueba",
                    "Resumen",
                    null,
                    10m,
                    "unidad",
                    null,
                    null,
                    null));

            await using var database = Context(connection);
            var service = new ServiceOrderApplicationService(
                database,
                showcase,
                new OptionalServices(),
                new Admin(),
                new ServiceOrderCodeAllocator(
                    database,
                    new Settings(seriesLabel),
                    new NodeIdentity("principal"),
                    new FixedTimeProvider(Now)),
                new NodeIdentity("principal"),
                new FixedTimeProvider(Now));

            var result = await service.CreateAsync(
                new CreateServiceOrderRequest(
                    IdempotencyKey: Guid.CreateVersion7(),
                    CustomerId: null,
                    CustomerName: "Cliente configuración",
                    CustomerPhone: "999111222",
                    CustomerEmail: null,
                    ReceivedNotes: null,
                    ReceivedAt: null,
                    PromisedAt: null,
                    AssignToMe: false,
                    Items:
                    [
                        new CreateServiceOrderLineRequest(
                            501,
                            "Trabajo de prueba.",
                            1m,
                            10m)
                    ]),
                CancellationToken.None);

            Assert.Equal(ServiceOrderAdminOutcome.Invalid, result.Outcome);
            Assert.Contains(
                ServiceOrderSettingsKeys.SeriesLabel,
                result.Error!,
                StringComparison.Ordinal);
            Assert.Empty(showcase.Calls);

            Assert.Equal(
                0L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    "SELECT count(*) FROM service_orders.service_orders"));

            Assert.Equal(
                0L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    "SELECT count(*) FROM service_orders.service_order_series"));
        });

    private static ServiceOrdersDbContext Context(string connection)
        => new(
            new DbContextOptionsBuilder<ServiceOrdersDbContext>()
                .UseNpgsql(
                    connection,
                    options => options.MigrationsHistoryTable(
                        ServiceOrdersDbContext.MigrationsHistoryTable,
                        ServiceOrdersDbContext.Schema))
                .Options,
            new NodeIdentity("principal"),
            new FixedTimeProvider(Now));

    private sealed class Showcase(params ServiceSnapshot[] snapshots)
        : IServiceShowcaseSnapshots
    {
        private readonly IReadOnlyDictionary<int, ServiceSnapshot> _snapshots =
            snapshots.ToDictionary(snapshot => snapshot.ServiceId);

        public Dictionary<int, int> Calls { get; } = [];

        public Task<ServiceSnapshot?> GetPublishedSnapshotAsync(
            int serviceId,
            CancellationToken cancellationToken)
        {
            Calls[serviceId] = Calls.GetValueOrDefault(serviceId) + 1;
            return Task.FromResult(_snapshots.GetValueOrDefault(serviceId));
        }
    }

    private sealed class OptionalServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class Admin : ICurrentAdmin
    {
        public int? AdminUserId => 19;
        public string? Email => "ana@example.test";
        public string? Role => AdminRole.Editor;
        public string? DisplayName => "Ana Operadora";
        public string? HomeNode => "principal";
        public bool IsInRole(string role) => role == AdminRole.Editor;
    }

    private sealed class Settings(string? seriesLabel) : ISettingsReader
    {
        public string? Get(string key)
            => key == ServiceOrderSettingsKeys.SeriesLabel
                ? seriesLabel
                : null;

        public T? Get<T>(string key) => default;

        public IReadOnlyDictionary<string, string> GetPublic()
            => new Dictionary<string, string>();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
