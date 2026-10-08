using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.ServiceOrders.Application;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Data;
using Sillar.Modules.ServiceOrders.Numbering;
using Sillar.Modules.ServiceOrders.Services;
using Sillar.Modules.Services.Contracts;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Tests;

public sealed class ApplicationBehaviorPostgresTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 7, 20, 30, 0, TimeSpan.Zero);

    [Fact]
    public Task Crm_snapshot_is_optional_and_frozen_when_used()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var customerId = Guid.CreateVersion7();

            var crm = new CustomerReader
            {
                Response = new CustomerOrderContactSnapshot(
                    CustomerId: customerId,
                    FullName: "Cliente Desde CRM",
                    Email: "crm@ejemplo.test",
                    Phone: "999111222",
                    DocumentType: "dni",
                    DocumentNumber: "12345678",
                    EmailVerified: true)
            };

            var showcase = ShowcaseWith(
                new ServiceSnapshot(
                    41,
                    "Impresión",
                    "impresion",
                    "Impresión a color",
                    null,
                    5m,
                    "Por hoja",
                    null,
                    null,
                    null));

            Guid orderId;

            await using (var database = Context(connection))
            {
                var result = await Application(
                        database,
                        showcase,
                        new OptionalServices(crm),
                        new Admin(AdminRole.Editor))
                    .CreateAsync(
                        new CreateServiceOrderRequest(
                            IdempotencyKey: Guid.CreateVersion7(),
                            CustomerId: customerId,
                            CustomerName: "Este nombre manual no debe ganar",
                            CustomerPhone: "000000000",
                            CustomerEmail: "manual@ejemplo.test",
                            ReceivedNotes: null,
                            ReceivedAt: null,
                            PromisedAt: null,
                            AssignToMe: false,
                            Items:
                            [
                                new CreateServiceOrderLineRequest(
                                    41,
                                    "Dos copias.",
                                    2m,
                                    5m)
                            ]),
                        CancellationToken.None);

                Assert.Equal(ServiceOrderAdminOutcome.Ok, result.Outcome);
                Assert.Equal(customerId, result.Value!.CustomerId);
                Assert.Equal("Cliente Desde CRM", result.Value.CustomerName);
                Assert.Equal("crm@ejemplo.test", result.Value.CustomerEmail);
                Assert.Equal("999111222", result.Value.CustomerPhone);
                orderId = result.Value.ServiceOrderId;
            }

            Assert.Equal(1, crm.Calls);

            crm.Response = new CustomerOrderContactSnapshot(
                CustomerId: customerId,
                FullName: "Nombre Cambiado Después",
                Email: "nuevo@ejemplo.test",
                Phone: "988888888",
                DocumentType: "dni",
                DocumentNumber: "12345678",
                EmailVerified: true);

            await using var readingDatabase = Context(connection);

            var stored = await Application(
                    readingDatabase,
                    showcase,
                    new OptionalServices(crm),
                    new Admin(AdminRole.Editor))
                .GetAdminAsync(orderId, CancellationToken.None);

            Assert.NotNull(stored);
            Assert.Equal("Cliente Desde CRM", stored.CustomerName);
            Assert.Equal("crm@ejemplo.test", stored.CustomerEmail);
            Assert.Equal("999111222", stored.CustomerPhone);
            Assert.Equal(1, crm.Calls);
        });

    [Fact]
    public Task Editing_keeps_existing_snapshot_and_only_snapshots_new_lines()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = ShowcaseWith(
                new ServiceSnapshot(
                    51,
                    "Anillado original",
                    "anillado-original",
                    "Original",
                    null,
                    10m,
                    "Por documento",
                    null,
                    null,
                    null),
                new ServiceSnapshot(
                    52,
                    "Plastificado",
                    "plastificado",
                    "Protección",
                    null,
                    8m,
                    "Por documento",
                    null,
                    null,
                    null));

            ServiceOrderAdminDetail created;

            await using (var database = Context(connection))
            {
                var result = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .CreateAsync(
                        ManualRequest(
                            51,
                            "Detalle inicial.",
                            1m,
                            10m),
                        CancellationToken.None);

                Assert.Equal(ServiceOrderAdminOutcome.Ok, result.Outcome);
                created = result.Value!;
            }

            showcase.Snapshots[51] = new ServiceSnapshot(
                51,
                "Nombre cambiado en M05a",
                "nombre-cambiado",
                "Nuevo texto",
                null,
                99m,
                "Por otro concepto",
                null,
                null,
                null);

            ServiceOrderAdminOperation<ServiceOrderAdminDetail> updated;

            await using (var database = Context(connection))
            {
                updated = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .UpdateAsync(
                        created.ServiceOrderId,
                        new UpdateServiceOrderRequest(
                            ExpectedUpdatedAt: created.UpdatedAt,
                            CustomerId: null,
                            CustomerName: "Cliente Manual",
                            CustomerPhone: "999111222",
                            CustomerEmail: null,
                            ReceivedNotes: "Editada",
                            PromisedAt: Now.AddDays(3),
                            Items:
                            [
                                new UpdateServiceOrderLineRequest(
                                    ServiceOrderItemId:
                                        created.Items.Single().ServiceOrderItemId,
                                    ServiceId: null,
                                    RequestedDetails: "Detalle corregido.",
                                    Quantity: 2m,
                                    AgreedUnitPrice: 11m),

                                new UpdateServiceOrderLineRequest(
                                    ServiceOrderItemId: null,
                                    ServiceId: 52,
                                    RequestedDetails: "Con acabado mate.",
                                    Quantity: 1m,
                                    AgreedUnitPrice: 8m)
                            ]),
                        CancellationToken.None);
            }

            Assert.Equal(ServiceOrderAdminOutcome.Ok, updated.Outcome);
            Assert.Equal(2, updated.Value!.Items.Count);

            var existing = updated.Value.Items.Single(
                item => item.ServiceSourceId == 51);

            Assert.Equal("Anillado original", existing.ServiceName);
            Assert.Equal("anillado-original", existing.ServiceSlug);
            Assert.Equal("Original", existing.ServiceShortDescription);
            Assert.Equal(10m, existing.ShowcasePrice);
            Assert.Equal("Detalle corregido.", existing.RequestedDetails);
            Assert.Equal(2m, existing.Quantity);
            Assert.Equal(11m, existing.AgreedUnitPrice);

            var added = updated.Value.Items.Single(
                item => item.ServiceSourceId == 52);

            Assert.Equal("Plastificado", added.ServiceName);
            Assert.Equal("Con acabado mate.", added.RequestedDetails);

            Assert.Equal(1, showcase.Calls[51]);
            Assert.Equal(1, showcase.Calls[52]);
        });

    [Fact]
    public Task Take_and_release_are_atomic_and_stale_assignment_is_rejected()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = ShowcaseWith(
                new ServiceSnapshot(
                    61,
                    "Grabado",
                    "grabado",
                    "Grabado simple",
                    null,
                    20m,
                    "Por pieza",
                    null,
                    null,
                    null));

            ServiceOrderAdminDetail created;

            await using (var database = Context(connection))
            {
                var result = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .CreateAsync(
                        ManualRequest(
                            61,
                            "Iniciales A.M.",
                            1m,
                            20m),
                        CancellationToken.None);

                created = result.Value!;
                Assert.Null(created.CurrentAssignee);
            }

            ServiceOrderAdminOperation<ServiceOrderAdminDetail> taken;

            await using (var database = Context(connection))
            {
                taken = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .TakeAsync(
                        created.ServiceOrderId,
                        created.UpdatedAt,
                        CancellationToken.None);
            }

            Assert.Equal(ServiceOrderAdminOutcome.Ok, taken.Outcome);
            Assert.Equal(
                "Ana Operadora",
                taken.Value!.CurrentAssignee!.DisplayName);
            Assert.Single(taken.Value.AssignmentHistory);
            Assert.Equal(
                "assigned",
                taken.Value.AssignmentHistory[0].Action);

            await using (var staleDatabase = Context(connection))
            {
                var stale = await Application(
                        staleDatabase,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .TakeAsync(
                        created.ServiceOrderId,
                        created.UpdatedAt,
                        CancellationToken.None);

                Assert.Equal(
                    ServiceOrderAdminOutcome.Conflict,
                    stale.Outcome);
            }

            ServiceOrderAdminOperation<ServiceOrderAdminDetail> released;

            await using (var database = Context(connection))
            {
                released = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .UnassignAsync(
                        created.ServiceOrderId,
                        taken.Value.UpdatedAt,
                        CancellationToken.None);
            }

            Assert.Equal(ServiceOrderAdminOutcome.Ok, released.Outcome);
            Assert.Null(released.Value!.CurrentAssignee);
            Assert.Equal(2, released.Value.AssignmentHistory.Count);

            var unassigned = released.Value.AssignmentHistory.Single(
                item => item.Action == "unassigned");

            Assert.Equal("Ana Operadora", unassigned.Assignee.DisplayName);
            Assert.Equal(19, unassigned.Assignee.AdminUserId);
            Assert.Equal("cuenta-lima", unassigned.Assignee.HomeNode);
            Assert.Equal("Ana Operadora", unassigned.PerformedBy.DisplayName);
        });

    [Fact]
    public Task Ready_requires_reopen_to_in_progress_before_content_edit()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = ShowcaseWith(
                new ServiceSnapshot(
                    71,
                    "Encuadernado",
                    "encuadernado",
                    "Encuadernado simple",
                    null,
                    15m,
                    "Por documento",
                    null,
                    null,
                    null));

            ServiceOrderAdminDetail created;

            await using (var database = Context(connection))
            {
                var result = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .CreateAsync(
                        ManualRequest(
                            71,
                            "Detalle original.",
                            1m,
                            15m),
                        CancellationToken.None);

                Assert.Equal(ServiceOrderAdminOutcome.Ok, result.Outcome);
                created = result.Value!;
            }

            async Task TransitionAsync(string fromStatus, string toStatus)
            {
                await using var database = Context(connection);

                var result = await new ServiceOrderTransitionService(
                        database,
                        new Admin(AdminRole.Editor),
                        new FixedTimeProvider(Now))
                    .TransitionAsync(
                        created.ServiceOrderId,
                        fromStatus,
                        toStatus,
                        CancellationToken.None);

                Assert.Equal(ServiceOrderOutcome.Ok, result.Outcome);
            }

            await TransitionAsync(
                ServiceOrderStatuses.Received,
                ServiceOrderStatuses.InProgress);

            await TransitionAsync(
                ServiceOrderStatuses.InProgress,
                ServiceOrderStatuses.Ready);

            ServiceOrderAdminDetail ready;

            await using (var database = Context(connection))
            {
                ready = (await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .GetAdminAsync(
                        created.ServiceOrderId,
                        CancellationToken.None))!;
            }

            Assert.Equal(ServiceOrderStatuses.Ready, ready.CurrentStatus);

            ServiceOrderAdminOperation<ServiceOrderAdminDetail> blocked;

            await using (var database = Context(connection))
            {
                blocked = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .UpdateAsync(
                        created.ServiceOrderId,
                        new UpdateServiceOrderRequest(
                            ExpectedUpdatedAt: ready.UpdatedAt,
                            CustomerId: ready.CustomerId,
                            CustomerName: ready.CustomerName,
                            CustomerPhone: ready.CustomerPhone,
                            CustomerEmail: ready.CustomerEmail,
                            ReceivedNotes: "Esto no debe persistirse.",
                            PromisedAt: ready.PromisedAt,
                            Items:
                            [
                                new UpdateServiceOrderLineRequest(
                                    ServiceOrderItemId:
                                        ready.Items.Single().ServiceOrderItemId,
                                    ServiceId: null,
                                    RequestedDetails:
                                        "Tampoco debe persistirse.",
                                    Quantity:
                                        ready.Items.Single().Quantity,
                                    AgreedUnitPrice:
                                        ready.Items.Single().AgreedUnitPrice)
                            ]),
                        CancellationToken.None);
            }

            Assert.Equal(
                ServiceOrderAdminOutcome.Conflict,
                blocked.Outcome);

            Assert.Contains(
                "in_progress",
                blocked.Error!,
                StringComparison.OrdinalIgnoreCase);

            await using (var database = Context(connection))
            {
                var afterBlocked = (await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .GetAdminAsync(
                        created.ServiceOrderId,
                        CancellationToken.None))!;

                Assert.Equal(
                    ServiceOrderStatuses.Ready,
                    afterBlocked.CurrentStatus);

                Assert.Equal(
                    ready.UpdatedAt,
                    afterBlocked.UpdatedAt);

                Assert.Null(afterBlocked.ReceivedNotes);

                Assert.Equal(
                    "Detalle original.",
                    afterBlocked.Items.Single().RequestedDetails);
            }

            await TransitionAsync(
                ServiceOrderStatuses.Ready,
                ServiceOrderStatuses.InProgress);

            ServiceOrderAdminDetail reopened;

            await using (var database = Context(connection))
            {
                reopened = (await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .GetAdminAsync(
                        created.ServiceOrderId,
                        CancellationToken.None))!;
            }

            Assert.Equal(
                ServiceOrderStatuses.InProgress,
                reopened.CurrentStatus);

            await using (var database = Context(connection))
            {
                var updated = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .UpdateAsync(
                        created.ServiceOrderId,
                        new UpdateServiceOrderRequest(
                            ExpectedUpdatedAt: reopened.UpdatedAt,
                            CustomerId: reopened.CustomerId,
                            CustomerName: reopened.CustomerName,
                            CustomerPhone: reopened.CustomerPhone,
                            CustomerEmail: reopened.CustomerEmail,
                            ReceivedNotes:
                                "Editada después de reabrir.",
                            PromisedAt: reopened.PromisedAt,
                            Items:
                            [
                                new UpdateServiceOrderLineRequest(
                                    ServiceOrderItemId:
                                        reopened.Items.Single()
                                            .ServiceOrderItemId,
                                    ServiceId: null,
                                    RequestedDetails:
                                        "Detalle editado después de reabrir.",
                                    Quantity: 2m,
                                    AgreedUnitPrice: 16m)
                            ]),
                        CancellationToken.None);

                Assert.Equal(
                    ServiceOrderAdminOutcome.Ok,
                    updated.Outcome);

                Assert.Equal(
                    ServiceOrderStatuses.InProgress,
                    updated.Value!.CurrentStatus);

                Assert.Equal(
                    "Editada después de reabrir.",
                    updated.Value.ReceivedNotes);

                Assert.Equal(
                    "Detalle editado después de reabrir.",
                    updated.Value.Items.Single().RequestedDetails);

                Assert.Equal(
                    2m,
                    updated.Value.Items.Single().Quantity);

                Assert.Equal(
                    16m,
                    updated.Value.Items.Single().AgreedUnitPrice);
            }
        });

    [Fact]
    public Task Reordering_two_persisted_lines_preserves_both_without_sort_collision()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = ShowcaseWith(
                new ServiceSnapshot(
                    91,
                    "Servicio A",
                    "servicio-a",
                    "Primer servicio",
                    null,
                    10m,
                    "Unidad",
                    null,
                    null,
                    null),
                new ServiceSnapshot(
                    92,
                    "Servicio B",
                    "servicio-b",
                    "Segundo servicio",
                    null,
                    20m,
                    "Unidad",
                    null,
                    null,
                    null));

            ServiceOrderAdminDetail created;

            await using (var database = Context(connection))
            {
                var result = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
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
                            Items:
                            [
                                new CreateServiceOrderLineRequest(
                                    91,
                                    "Primera.",
                                    1m,
                                    10m),
                                new CreateServiceOrderLineRequest(
                                    92,
                                    "Segunda.",
                                    1m,
                                    20m)
                            ]),
                        CancellationToken.None);

                Assert.Equal(ServiceOrderAdminOutcome.Ok, result.Outcome);
                created = result.Value!;
            }

            var first = created.Items.Single(
                item => item.ServiceSourceId == 91);

            var second = created.Items.Single(
                item => item.ServiceSourceId == 92);

            await using (var database = Context(connection))
            {
                var result = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .UpdateAsync(
                        created.ServiceOrderId,
                        new UpdateServiceOrderRequest(
                            ExpectedUpdatedAt: created.UpdatedAt,
                            CustomerId: created.CustomerId,
                            CustomerName: created.CustomerName,
                            CustomerPhone: created.CustomerPhone,
                            CustomerEmail: created.CustomerEmail,
                            ReceivedNotes: created.ReceivedNotes,
                            PromisedAt: created.PromisedAt,
                            Items:
                            [
                                new UpdateServiceOrderLineRequest(
                                    second.ServiceOrderItemId,
                                    null,
                                    second.RequestedDetails,
                                    second.Quantity,
                                    second.AgreedUnitPrice),
                                new UpdateServiceOrderLineRequest(
                                    first.ServiceOrderItemId,
                                    null,
                                    first.RequestedDetails,
                                    first.Quantity,
                                    first.AgreedUnitPrice)
                            ]),
                        CancellationToken.None);

                Assert.Equal(ServiceOrderAdminOutcome.Ok, result.Outcome);

                Assert.Equal(
                    [92, 91],
                    result.Value!.Items
                        .OrderBy(item => item.SortOrder)
                        .Select(item => item.ServiceSourceId)
                        .ToArray());
            }
        });

    [Fact]
    public Task Inserting_new_line_first_shifts_persisted_lines_without_sort_collision()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = ShowcaseWith(
                new ServiceSnapshot(
                    101,
                    "Servicio A",
                    "servicio-a",
                    "Primer servicio",
                    null,
                    10m,
                    "Unidad",
                    null,
                    null,
                    null),
                new ServiceSnapshot(
                    102,
                    "Servicio B",
                    "servicio-b",
                    "Segundo servicio",
                    null,
                    20m,
                    "Unidad",
                    null,
                    null,
                    null),
                new ServiceSnapshot(
                    103,
                    "Servicio C",
                    "servicio-c",
                    "Nuevo servicio",
                    null,
                    30m,
                    "Unidad",
                    null,
                    null,
                    null));

            ServiceOrderAdminDetail created;

            await using (var database = Context(connection))
            {
                var result = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
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
                            Items:
                            [
                                new CreateServiceOrderLineRequest(
                                    101,
                                    "Primera persistida.",
                                    1m,
                                    10m),
                                new CreateServiceOrderLineRequest(
                                    102,
                                    "Segunda persistida.",
                                    1m,
                                    20m)
                            ]),
                        CancellationToken.None);

                Assert.Equal(ServiceOrderAdminOutcome.Ok, result.Outcome);
                created = result.Value!;
            }

            var first = created.Items.Single(
                item => item.ServiceSourceId == 101);

            var second = created.Items.Single(
                item => item.ServiceSourceId == 102);

            ServiceOrderAdminDetail updated;

            await using (var database = Context(connection))
            {
                var result = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .UpdateAsync(
                        created.ServiceOrderId,
                        new UpdateServiceOrderRequest(
                            ExpectedUpdatedAt: created.UpdatedAt,
                            CustomerId: created.CustomerId,
                            CustomerName: created.CustomerName,
                            CustomerPhone: created.CustomerPhone,
                            CustomerEmail: created.CustomerEmail,
                            ReceivedNotes: created.ReceivedNotes,
                            PromisedAt: created.PromisedAt,
                            Items:
                            [
                                new UpdateServiceOrderLineRequest(
                                    ServiceOrderItemId: null,
                                    ServiceId: 103,
                                    RequestedDetails: "Nueva primera.",
                                    Quantity: 1m,
                                    AgreedUnitPrice: 30m),

                                new UpdateServiceOrderLineRequest(
                                    ServiceOrderItemId:
                                        first.ServiceOrderItemId,
                                    ServiceId: null,
                                    RequestedDetails:
                                        first.RequestedDetails,
                                    Quantity: first.Quantity,
                                    AgreedUnitPrice:
                                        first.AgreedUnitPrice),

                                new UpdateServiceOrderLineRequest(
                                    ServiceOrderItemId:
                                        second.ServiceOrderItemId,
                                    ServiceId: null,
                                    RequestedDetails:
                                        second.RequestedDetails,
                                    Quantity: second.Quantity,
                                    AgreedUnitPrice:
                                        second.AgreedUnitPrice)
                            ]),
                        CancellationToken.None);

                Assert.Equal(ServiceOrderAdminOutcome.Ok, result.Outcome);
                updated = result.Value!;
            }

            Assert.Equal(3, updated.Items.Count);

            Assert.Equal(
                [103, 101, 102],
                updated.Items
                    .OrderBy(item => item.SortOrder)
                    .Select(item => item.ServiceSourceId)
                    .ToArray());

            Assert.Equal(
                first.ServiceOrderItemId,
                updated.Items.Single(
                    item => item.ServiceSourceId == 101)
                    .ServiceOrderItemId);

            Assert.Equal(
                second.ServiceOrderItemId,
                updated.Items.Single(
                    item => item.ServiceSourceId == 102)
                    .ServiceOrderItemId);

            Assert.Equal(
                [0, 1, 2],
                updated.Items
                    .OrderBy(item => item.SortOrder)
                    .Select(item => item.SortOrder)
                    .ToArray());

            Assert.Equal(1, showcase.Calls[101]);
            Assert.Equal(1, showcase.Calls[102]);
            Assert.Equal(1, showcase.Calls[103]);

            Assert.Equal(
                3L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    $"""
                    SELECT count(*)
                      FROM service_orders.service_order_items
                     WHERE service_order_id = '{created.ServiceOrderId}'
                    """));
        });

    [Fact]
    public Task Persisted_line_omission_is_rejected_without_deleting_or_mutating_lines()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var showcase = ShowcaseWith(
                new ServiceSnapshot(
                    81,
                    "Impresión",
                    "impresion",
                    "Impresión simple",
                    null,
                    4m,
                    "Por hoja",
                    null,
                    null,
                    null),
                new ServiceSnapshot(
                    82,
                    "Anillado",
                    "anillado",
                    "Anillado simple",
                    null,
                    9m,
                    "Por documento",
                    null,
                    null,
                    null));

            ServiceOrderAdminDetail created;

            await using (var database = Context(connection))
            {
                var result = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
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
                            Items:
                            [
                                new CreateServiceOrderLineRequest(
                                    81,
                                    "Primera línea original.",
                                    2m,
                                    4m),
                                new CreateServiceOrderLineRequest(
                                    82,
                                    "Segunda línea original.",
                                    1m,
                                    9m)
                            ]),
                        CancellationToken.None);

                Assert.Equal(
                    ServiceOrderAdminOutcome.Ok,
                    result.Outcome);

                created = result.Value!;
            }

            Assert.Equal(2, created.Items.Count);

            var first = created.Items.Single(
                item => item.ServiceSourceId == 81);

            ServiceOrderAdminOperation<ServiceOrderAdminDetail> rejected;

            await using (var database = Context(connection))
            {
                rejected = await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .UpdateAsync(
                        created.ServiceOrderId,
                        new UpdateServiceOrderRequest(
                            ExpectedUpdatedAt: created.UpdatedAt,
                            CustomerId: created.CustomerId,
                            CustomerName: created.CustomerName,
                            CustomerPhone: created.CustomerPhone,
                            CustomerEmail: created.CustomerEmail,
                            ReceivedNotes: "No debe persistirse.",
                            PromisedAt: created.PromisedAt,
                            Items:
                            [
                                new UpdateServiceOrderLineRequest(
                                    ServiceOrderItemId:
                                        first.ServiceOrderItemId,
                                    ServiceId: null,
                                    RequestedDetails:
                                        "Mutación que tampoco debe persistirse.",
                                    Quantity: 7m,
                                    AgreedUnitPrice: 44m)
                            ]),
                        CancellationToken.None);
            }

            Assert.Equal(
                ServiceOrderAdminOutcome.Invalid,
                rejected.Outcome);

            Assert.Contains(
                "no se elimina",
                rejected.Error!,
                StringComparison.OrdinalIgnoreCase);

            await using (var database = Context(connection))
            {
                var stored = (await Application(
                        database,
                        showcase,
                        new OptionalServices(),
                        new Admin(AdminRole.Editor))
                    .GetAdminAsync(
                        created.ServiceOrderId,
                        CancellationToken.None))!;

                Assert.Equal(2, stored.Items.Count);
                Assert.Equal(created.UpdatedAt, stored.UpdatedAt);
                Assert.Null(stored.ReceivedNotes);

                var storedFirst = stored.Items.Single(
                    item => item.ServiceSourceId == 81);

                var storedSecond = stored.Items.Single(
                    item => item.ServiceSourceId == 82);

                Assert.Equal(
                    "Primera línea original.",
                    storedFirst.RequestedDetails);

                Assert.Equal(2m, storedFirst.Quantity);
                Assert.Equal(4m, storedFirst.AgreedUnitPrice);

                Assert.Equal(
                    "Segunda línea original.",
                    storedSecond.RequestedDetails);

                Assert.Equal(1m, storedSecond.Quantity);
                Assert.Equal(9m, storedSecond.AgreedUnitPrice);
            }

            Assert.Equal(1, showcase.Calls[81]);
            Assert.Equal(1, showcase.Calls[82]);
        });

    [Fact]
    public Task Cancellation_is_forbidden_to_editor_inside_authoritative_operation()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            var id = Guid.CreateVersion7();

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(id));

            await using (var editorDatabase = Context(connection))
            {
                var denied = await new ServiceOrderTransitionService(
                        editorDatabase,
                        new Admin(AdminRole.Editor),
                        new FixedTimeProvider(Now))
                    .TransitionAsync(
                        id,
                        ServiceOrderStatuses.Received,
                        ServiceOrderStatuses.Cancelled,
                        CancellationToken.None);

                Assert.Equal(ServiceOrderOutcome.Invalid, denied.Outcome);
                Assert.Contains(
                    "admin",
                    denied.Error!,
                    StringComparison.OrdinalIgnoreCase);
            }

            Assert.Equal(
                ServiceOrderStatuses.Received,
                await EphemeralDatabase.ScalarAsync<string>(
                    connection,
                    $"""
                    SELECT status
                      FROM service_orders.service_orders
                     WHERE service_order_id = '{id}'
                    """));

            Assert.Equal(
                0L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    $"""
                    SELECT count(*)
                      FROM service_orders.service_order_status_history
                     WHERE service_order_id = '{id}'
                    """));

            await using (var adminDatabase = Context(connection))
            {
                var allowed = await new ServiceOrderTransitionService(
                        adminDatabase,
                        new Admin(AdminRole.Admin),
                        new FixedTimeProvider(Now))
                    .TransitionAsync(
                        id,
                        ServiceOrderStatuses.Received,
                        ServiceOrderStatuses.Cancelled,
                        CancellationToken.None);

                Assert.Equal(ServiceOrderOutcome.Ok, allowed.Outcome);
            }

            Assert.Equal(
                ServiceOrderStatuses.Cancelled,
                await EphemeralDatabase.ScalarAsync<string>(
                    connection,
                    $"""
                    SELECT status
                      FROM service_orders.service_orders
                     WHERE service_order_id = '{id}'
                    """));

            Assert.Equal(
                1L,
                await EphemeralDatabase.ScalarAsync<long>(
                    connection,
                    $"""
                    SELECT count(*)
                      FROM service_orders.service_order_status_history
                     WHERE service_order_id = '{id}'
                    """));
        });

    private static CreateServiceOrderRequest ManualRequest(
        int serviceId,
        string details,
        decimal quantity,
        decimal? price)
        => new(
            IdempotencyKey: Guid.CreateVersion7(),
            CustomerId: null,
            CustomerName: "Cliente Manual",
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
                    details,
                    quantity,
                    price)
            ]);

    private static ServiceOrderApplicationService Application(
        ServiceOrdersDbContext database,
        IServiceShowcaseSnapshots showcase,
        IServiceProvider optionalServices,
        ICurrentAdmin admin)
        => new(
            database,
            showcase,
            optionalServices,
            admin,
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

    private static Showcase ShowcaseWith(
        params ServiceSnapshot[] snapshots)
        => new(snapshots);

    private sealed class Showcase(
        IEnumerable<ServiceSnapshot> snapshots)
        : IServiceShowcaseSnapshots
    {
        public Dictionary<int, ServiceSnapshot> Snapshots { get; } =
            snapshots.ToDictionary(snapshot => snapshot.ServiceId);

        public Dictionary<int, int> Calls { get; } = [];

        public Task<ServiceSnapshot?> GetPublishedSnapshotAsync(
            int serviceId,
            CancellationToken cancellationToken)
        {
            Calls[serviceId] = Calls.GetValueOrDefault(serviceId) + 1;

            return Task.FromResult(
                Snapshots.GetValueOrDefault(serviceId));
        }
    }

    private sealed class CustomerReader : ICustomerSnapshotReader
    {
        public CustomerOrderContactSnapshot? Response { get; set; }
        public int Calls { get; private set; }

        public Task<CustomerOrderContactSnapshot?> GetForOrderAsync(
            Guid customerId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Response);
        }

        public Task<CustomerOrderSnapshot?> GetForOrderAsync(
            Guid customerId,
            Guid customerAddressId,
            CancellationToken cancellationToken)
            => Task.FromResult<CustomerOrderSnapshot?>(null);
    }

    private sealed class OptionalServices(
        ICustomerSnapshotReader? customers = null)
        : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(ICustomerSnapshotReader)
                ? customers
                : null;
    }

    private sealed class Admin(string role) : ICurrentAdmin
    {
        public int? AdminUserId => 19;
        public string? Email => "ana@example.test";
        public string? Role => role;
        public string? DisplayName => "Ana Operadora";
        public string? HomeNode => "cuenta-lima";

        public bool IsInRole(string requested)
            => role switch
            {
                AdminRole.SuperAdmin => true,
                AdminRole.Admin =>
                    requested is AdminRole.Admin or AdminRole.Editor,
                AdminRole.Editor =>
                    requested == AdminRole.Editor,
                _ => false
            };
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
