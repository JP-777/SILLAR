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

public sealed class CreateServiceOrderPostgresTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 7, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public Task Manual_creation_freezes_two_lines_and_writes_initial_history_atomically()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var media = Guid.CreateVersion7();
            var showcase = new Showcase(
                new ServiceSnapshot(
                    11,
                    "Anillado",
                    "anillado",
                    "Tapa transparente",
                    "Anillado espiral por documento.",
                    12.50m,
                    "Por documento",
                    media,
                    $"/media/2026/10/{media}.png",
                    "Documento anillado"),
                new ServiceSnapshot(
                    12,
                    "Impresión A3",
                    "impresion-a3",
                    null,
                    "Impresión a color en papel A3.",
                    null,
                    "Por hoja",
                    null,
                    null,
                    null));

            await using var database = Context(connection);
            var service = Application(database, showcase);

            var result = await service.CreateAsync(
                new CreateServiceOrderRequest(
                    IdempotencyKey: Guid.CreateVersion7(),
                    CustomerId: null,
                    CustomerName: "Rosa Mamani",
                    CustomerPhone: "999 888 777",
                    CustomerEmail: null,
                    ReceivedNotes: "Coordinar por teléfono.",
                    ReceivedAt: null,
                    PromisedAt: Now.AddDays(2),
                    AssignToMe: true,
                    Items:
                    [
                        new CreateServiceOrderLineRequest(
                            11,
                            "Tapa azul y espiral negro.",
                            2.000m,
                            15.50m),

                        new CreateServiceOrderLineRequest(
                            12,
                            "Impresión a color.",
                            3.500m,
                            null)
                    ]),
                CancellationToken.None);

            Assert.Equal(ServiceOrderAdminOutcome.Ok, result.Outcome);

            var order = Assert.IsType<ServiceOrderAdminDetail>(result.Value);

            Assert.Equal("S-2026-0001", order.VisibleCode);
            Assert.Equal(ServiceOrderStatuses.Received, order.CurrentStatus);
            Assert.Equal("Rosa Mamani", order.CustomerName);
            Assert.Equal("999 888 777", order.CustomerPhone);
            Assert.Null(order.CustomerId);

            Assert.Equal(2, order.Items.Count);
            Assert.Equal(["Anillado", "Impresión A3"],
                order.Items.Select(item => item.ServiceName));
            Assert.Equal(["principal", "principal"],
                order.Items.Select(item => item.ServiceSourceNode));

            Assert.Equal(media, order.Items[0].MediaAssetId);
            Assert.Equal(
                $"/media/2026/10/{media}.png",
                order.Items[0].ImageUrl);
            Assert.Equal("Documento anillado", order.Items[0].ImageAltText);

            Assert.True(order.HasPendingPrice);
            Assert.Null(order.TotalAmount);

            Assert.Single(order.StatusHistory);
            Assert.Null(order.StatusHistory[0].FromStatus);
            Assert.Equal(
                ServiceOrderStatuses.Received,
                order.StatusHistory[0].ToStatus);

            Assert.NotNull(order.CurrentAssignee);
            Assert.Equal("Ana Operadora", order.CurrentAssignee.DisplayName);
            Assert.Equal(19, order.CurrentAssignee.AdminUserId);
            Assert.Equal("cuenta-lima", order.CurrentAssignee.HomeNode);

            Assert.Single(order.AssignmentHistory);
            Assert.Equal("assigned", order.AssignmentHistory[0].Action);

            Assert.Equal(1, showcase.Calls[11]);
            Assert.Equal(1, showcase.Calls[12]);

            Assert.Equal(
                1L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    "SELECT count(*) FROM service_orders.service_orders"));

            Assert.Equal(
                2L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    "SELECT count(*) FROM service_orders.service_order_items"));

            Assert.Equal(
                1L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    """
                    SELECT count(*)
                      FROM service_orders.service_order_status_history
                     WHERE from_status IS NULL
                       AND to_status = 'received'
                    """));

            Assert.Equal(
                1,
                await EphemeralDatabase.ScalarAsync<int>(
                    connection,
                    """
                    SELECT last_number
                      FROM service_orders.service_order_series
                     WHERE node_code = 'principal'
                       AND year = 2026
                    """));
        });

    [Fact]
    public Task Failure_after_number_reservation_rolls_back_document_and_number()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = new Showcase(
                new ServiceSnapshot(
                    21,
                    "Plastificado",
                    "plastificado",
                    "Protección de documento",
                    null,
                    8.00m,
                    "Por documento",
                    null,
                    null,
                    null));

            await EphemeralDatabase.ExecuteAsync(
                connection,
                """
                CREATE FUNCTION service_orders.reject_initial_history()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'sabotaje alta';
                END
                $$;

                CREATE TRIGGER reject_initial_history
                BEFORE INSERT ON service_orders.service_order_status_history
                FOR EACH ROW
                EXECUTE FUNCTION service_orders.reject_initial_history();
                """);

            await using (var database = Context(connection))
            {
                var service = Application(database, showcase);

                var error = await Assert.ThrowsAsync<DbUpdateException>(
                    () => service.CreateAsync(
                        Request(21),
                        CancellationToken.None));

                Assert.Contains(
                    "sabotaje alta",
                    error.InnerException?.Message ?? error.Message,
                    StringComparison.Ordinal);
            }

            Assert.Equal(
                0L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    "SELECT count(*) FROM service_orders.service_orders"));

            Assert.Equal(
                0L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    "SELECT count(*) FROM service_orders.service_order_items"));

            Assert.Equal(
                0L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    "SELECT count(*) FROM service_orders.service_order_status_history"));

            Assert.Equal(
                0L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    "SELECT count(*) FROM service_orders.service_order_series"));

            await EphemeralDatabase.ExecuteAsync(
                connection,
                """
                DROP TRIGGER reject_initial_history
                    ON service_orders.service_order_status_history;
                DROP FUNCTION service_orders.reject_initial_history();
                """);

            await using (var database = Context(connection))
            {
                var result = await Application(database, showcase).CreateAsync(
                    Request(21),
                    CancellationToken.None);

                Assert.Equal(ServiceOrderAdminOutcome.Ok, result.Outcome);
                Assert.Equal("S-2026-0001", result.Value!.VisibleCode);
            }

            Assert.Equal(
                1,
                await EphemeralDatabase.ScalarAsync<int>(
                    connection,
                    """
                    SELECT last_number
                      FROM service_orders.service_order_series
                     WHERE node_code = 'principal'
                       AND year = 2026
                    """));
        });

    [Fact]
    public Task Customer_id_without_active_crm_is_a_clean_conflict_and_does_not_reserve_number()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = new Showcase(
                new ServiceSnapshot(
                    31,
                    "Grabado",
                    "grabado",
                    "Grabado personalizado",
                    null,
                    20m,
                    "Por pieza",
                    null,
                    null,
                    null));

            await using var database = Context(connection);

            var result = await Application(
                    database,
                    showcase,
                    new OptionalServices())
                .CreateAsync(
                    new CreateServiceOrderRequest(
                        IdempotencyKey: Guid.CreateVersion7(),
                        CustomerId: Guid.CreateVersion7(),
                        CustomerName: null,
                        CustomerPhone: null,
                        CustomerEmail: null,
                        ReceivedNotes: null,
                        ReceivedAt: null,
                        PromisedAt: null,
                        AssignToMe: false,
                        Items:
                        [
                            new CreateServiceOrderLineRequest(
                                31,
                                "Iniciales R.M.",
                                1m,
                                20m)
                        ]),
                    CancellationToken.None);

            Assert.Equal(ServiceOrderAdminOutcome.Conflict, result.Outcome);
            Assert.Contains("CRM", result.Error!, StringComparison.Ordinal);

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

            Assert.Empty(showcase.Calls);
        });


    [Fact]
    public Task Same_idempotency_key_returns_same_order_without_second_snapshot_or_number()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = new Showcase(
                new ServiceSnapshot(
                    41,
                    "Encuadernado",
                    "encuadernado",
                    "Encuadernado simple",
                    null,
                    14m,
                    "Por documento",
                    null,
                    null,
                    null));

            var key = Guid.CreateVersion7();

            ServiceOrderAdminOperation<ServiceOrderAdminDetail> first;

            await using (var database = Context(connection))
            {
                first = await Application(database, showcase).CreateAsync(
                    Request(41, key),
                    CancellationToken.None);
            }

            ServiceOrderAdminOperation<ServiceOrderAdminDetail> replay;

            await using (var database = Context(connection))
            {
                replay = await Application(database, showcase).CreateAsync(
                    Request(41, key),
                    CancellationToken.None);
            }

            Assert.Equal(ServiceOrderAdminOutcome.Ok, first.Outcome);
            Assert.Equal(ServiceOrderAdminOutcome.Ok, replay.Outcome);

            Assert.False(first.IsReplay);
            Assert.True(replay.IsReplay);

            Assert.Equal(
                first.Value!.ServiceOrderId,
                replay.Value!.ServiceOrderId);

            Assert.Equal(
                first.Value.VisibleCode,
                replay.Value.VisibleCode);

            Assert.Equal(1, showcase.Calls[41]);

            Assert.Equal(
                1L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    "SELECT count(*) FROM service_orders.service_orders"));

            Assert.Equal(
                1L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    "SELECT count(*) FROM service_orders.service_order_items"));

            Assert.Equal(
                1L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    """
                    SELECT count(*)
                      FROM service_orders.service_order_status_history
                    """));

            Assert.Equal(
                1,
                await EphemeralDatabase.ScalarAsync<int>(
                    connection,
                    """
                    SELECT last_number
                      FROM service_orders.service_order_series
                     WHERE node_code = 'principal'
                       AND year = 2026
                    """));
        });

    private static CreateServiceOrderRequest Request(
        int serviceId,
        Guid? idempotencyKey = null)
        => new(
            IdempotencyKey: idempotencyKey ?? Guid.CreateVersion7(),
            CustomerId: null,
            CustomerName: "Cliente de prueba",
            CustomerPhone: "999111222",
            CustomerEmail: null,
            ReceivedNotes: null,
            ReceivedAt: null,
            PromisedAt: null,
            AssignToMe: false,
            Items:
            [
                new CreateServiceOrderLineRequest(
                    serviceId,
                    "Trabajo solicitado.",
                    1m,
                    10m)
            ]);

    private static ServiceOrderApplicationService Application(
        ServiceOrdersDbContext database,
        IServiceShowcaseSnapshots showcase,
        IServiceProvider? optionalServices = null)
        => new(
            database,
            showcase,
            optionalServices ?? new OptionalServices(),
            new Admin(),
            new ServiceOrderCodeAllocator(
                database,
                new Settings(),
                new NodeIdentity("principal"),
                new FixedTimeProvider(Now)),
            new NodeIdentity("principal"),
            new FixedTimeProvider(Now));

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
            return Task.FromResult(
                _snapshots.GetValueOrDefault(serviceId));
        }
    }

    private sealed class OptionalServices(
        ICustomerSnapshotReader? customers = null) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(ICustomerSnapshotReader)
                ? customers
                : null;
    }

    private sealed class Admin : ICurrentAdmin
    {
        public int? AdminUserId => 19;
        public string? Email => "ana@example.test";
        public string? Role => AdminRole.Editor;
        public string? DisplayName => "Ana Operadora";
        public string? HomeNode => "cuenta-lima";

        public bool IsInRole(string role)
            => role == AdminRole.Editor;
    }

    private sealed class Settings : ISettingsReader
    {
        public string? Get(string key)
            => key == ServiceOrderSettingsKeys.SeriesLabel
                ? "S"
                : null;

        public T? Get<T>(string key) => default;

        public IReadOnlyDictionary<string, string> GetPublic()
            => new Dictionary<string, string>();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
