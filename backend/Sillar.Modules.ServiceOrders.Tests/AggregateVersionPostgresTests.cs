using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders.Application;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Data;
using Sillar.Modules.ServiceOrders.Numbering;
using Sillar.Modules.Services.Contracts;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Tests;

public sealed class AggregateVersionPostgresTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 7, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public Task Child_only_edit_advances_parent_updated_at_and_row_version()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = ShowcaseWith(Snapshot(201));
            var created = await CreateOrderAsync(connection, showcase, 201);

            var oldRowVersion = await ParentRowVersionAsync(
                connection,
                created.ServiceOrderId);

            var item = created.Items.Single();

            ServiceOrderAdminDetail updated;

            await using (var database = Context(connection))
            {
                var result = await Application(database, showcase)
                    .UpdateAsync(
                        created.ServiceOrderId,
                        UpdateRequest(
                            created,
                            [
                                ExistingLine(
                                    item,
                                    requestedDetails:
                                        "Detalle modificado solamente en la línea.")
                            ]),
                        CancellationToken.None);

                Assert.Equal(ServiceOrderAdminOutcome.Ok, result.Outcome);
                updated = result.Value!;
            }

            var newRowVersion = await ParentRowVersionAsync(
                connection,
                created.ServiceOrderId);

            Assert.True(
                updated.UpdatedAt > created.UpdatedAt,
                $"Expected parent UpdatedAt to advance: old={created.UpdatedAt:o}, new={updated.UpdatedAt:o}");

            Assert.Equal(oldRowVersion + 1, newRowVersion);
        });

    [Fact]
    public Task Pure_reorder_advances_parent_updated_at()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = ShowcaseWith(
                Snapshot(211),
                Snapshot(212));

            var created = await CreateOrderAsync(
                connection,
                showcase,
                211,
                212);

            var first = created.Items.Single(
                item => item.ServiceSourceId == 211);

            var second = created.Items.Single(
                item => item.ServiceSourceId == 212);

            ServiceOrderAdminDetail updated;

            await using (var database = Context(connection))
            {
                var result = await Application(database, showcase)
                    .UpdateAsync(
                        created.ServiceOrderId,
                        UpdateRequest(
                            created,
                            [
                                ExistingLine(second),
                                ExistingLine(first)
                            ]),
                        CancellationToken.None);

                Assert.Equal(ServiceOrderAdminOutcome.Ok, result.Outcome);
                updated = result.Value!;
            }

            Assert.True(
                updated.UpdatedAt > created.UpdatedAt,
                $"Expected parent UpdatedAt to advance after reorder: old={created.UpdatedAt:o}, new={updated.UpdatedAt:o}");
        });

    [Fact]
    public Task Stale_parent_token_is_rejected_after_child_only_edit()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = ShowcaseWith(Snapshot(221));
            var created = await CreateOrderAsync(connection, showcase, 221);
            var item = created.Items.Single();

            await using (var database = Context(connection))
            {
                var first = await Application(database, showcase)
                    .UpdateAsync(
                        created.ServiceOrderId,
                        UpdateRequest(
                            created,
                            [
                                ExistingLine(
                                    item,
                                    requestedDetails: "Primera edición.")
                            ]),
                        CancellationToken.None);

                Assert.Equal(ServiceOrderAdminOutcome.Ok, first.Outcome);
            }

            await using (var database = Context(connection))
            {
                var stale = await Application(database, showcase)
                    .UpdateAsync(
                        created.ServiceOrderId,
                        UpdateRequest(
                            created,
                            [
                                ExistingLine(
                                    item,
                                    requestedDetails:
                                        "Segunda edición con token obsoleto.")
                            ]),
                        CancellationToken.None);

                Assert.Equal(
                    ServiceOrderAdminOutcome.Conflict,
                    stale.Outcome);
            }

            await using (var database = Context(connection))
            {
                var current = await Application(database, showcase)
                    .GetAdminAsync(
                        created.ServiceOrderId,
                        CancellationToken.None);

                Assert.NotNull(current);
                Assert.Equal(
                    "Primera edición.",
                    current.Items.Single().RequestedDetails);
            }
        });

    [Fact]
    public Task Tracking_source_observes_new_updated_at_after_child_only_edit()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = ShowcaseWith(Snapshot(231));
            var created = await CreateOrderAsync(connection, showcase, 231);
            var item = created.Items.Single();

            ServiceOrderTrackingSnapshot before;

            await using (var database = Context(connection))
            {
                IServiceOrderTrackingSource source =
                    Application(database, showcase);

                before = Assert.IsType<ServiceOrderTrackingSnapshot>(
                    await source.GetAsync(
                        created.ServiceOrderId,
                        CancellationToken.None));
            }

            await using (var database = Context(connection))
            {
                var update = await Application(database, showcase)
                    .UpdateAsync(
                        created.ServiceOrderId,
                        UpdateRequest(
                            created,
                            [
                                ExistingLine(
                                    item,
                                    quantity: item.Quantity + 1m)
                            ]),
                        CancellationToken.None);

                Assert.Equal(ServiceOrderAdminOutcome.Ok, update.Outcome);
            }

            ServiceOrderTrackingSnapshot after;

            await using (var database = Context(connection))
            {
                IServiceOrderTrackingSource source =
                    Application(database, showcase);

                after = Assert.IsType<ServiceOrderTrackingSnapshot>(
                    await source.GetAsync(
                        created.ServiceOrderId,
                        CancellationToken.None));
            }

            Assert.True(
                after.UpdatedAt > before.UpdatedAt,
                $"Expected tracking UpdatedAt to advance: old={before.UpdatedAt:o}, new={after.UpdatedAt:o}");
        });

    private static async Task<ServiceOrderAdminDetail> CreateOrderAsync(
        string connection,
        Showcase showcase,
        params int[] serviceIds)
    {
        await using var database = Context(connection);

        var result = await Application(database, showcase)
            .CreateAsync(
                new CreateServiceOrderRequest(
                    IdempotencyKey: Guid.CreateVersion7(),
                    CustomerId: null,
                    CustomerName: "Cliente Manual",
                    CustomerPhone: "999111222",
                    CustomerEmail: null,
                    ReceivedNotes: null,
                    ReceivedAt: null,
                    PromisedAt: null,
                    AssignToMe: false,
                    Items: serviceIds
                        .Select(
                            (serviceId, index) =>
                                new CreateServiceOrderLineRequest(
                                    ServiceId: serviceId,
                                    RequestedDetails:
                                        $"Trabajo solicitado {serviceId}.",
                                    Quantity: 1m,
                                    AgreedUnitPrice: 10m + index))
                        .ToArray()),
                CancellationToken.None);

        Assert.Equal(ServiceOrderAdminOutcome.Ok, result.Outcome);

        return result.Value!;
    }

    private static UpdateServiceOrderRequest UpdateRequest(
        ServiceOrderAdminDetail order,
        IReadOnlyList<UpdateServiceOrderLineRequest> items)
        => new(
            ExpectedUpdatedAt: order.UpdatedAt,
            CustomerId: order.CustomerId,
            CustomerName: order.CustomerName,
            CustomerPhone: order.CustomerPhone,
            CustomerEmail: order.CustomerEmail,
            ReceivedNotes: order.ReceivedNotes,
            PromisedAt: order.PromisedAt,
            Items: items);

    private static UpdateServiceOrderLineRequest ExistingLine(
        ServiceOrderAdminItem item,
        string? requestedDetails = null,
        decimal? quantity = null)
        => new(
            ServiceOrderItemId: item.ServiceOrderItemId,
            ServiceId: null,
            RequestedDetails:
                requestedDetails ?? item.RequestedDetails,
            Quantity: quantity ?? item.Quantity,
            AgreedUnitPrice: item.AgreedUnitPrice);

    private static async Task<long> ParentRowVersionAsync(
        string connection,
        Guid serviceOrderId)
        => await EphemeralDatabase.ScalarAsync<long>(
            connection,
            $"""
            SELECT row_version
              FROM service_orders.service_orders
             WHERE service_order_id = '{serviceOrderId}'
            """);

    private static ServiceSnapshot Snapshot(int serviceId)
        => new(
            serviceId,
            $"Servicio {serviceId}",
            $"servicio-{serviceId}",
            $"Descripción corta {serviceId}",
            null,
            10m,
            "Unidad",
            null,
            null,
            null);

    private static Showcase ShowcaseWith(
        params ServiceSnapshot[] snapshots)
        => new(snapshots);

    private static ServiceOrderApplicationService Application(
        ServiceOrdersDbContext database,
        IServiceShowcaseSnapshots showcase)
        => new(
            database,
            showcase,
            new OptionalServices(),
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

    private sealed class Showcase(
        IEnumerable<ServiceSnapshot> snapshots)
        : IServiceShowcaseSnapshots
    {
        private readonly IReadOnlyDictionary<int, ServiceSnapshot> _snapshots =
            snapshots.ToDictionary(snapshot => snapshot.ServiceId);

        public Task<ServiceSnapshot?> GetPublishedSnapshotAsync(
            int serviceId,
            CancellationToken cancellationToken)
            => Task.FromResult(
                _snapshots.GetValueOrDefault(serviceId));
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
