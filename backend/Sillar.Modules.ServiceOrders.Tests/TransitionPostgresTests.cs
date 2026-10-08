using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Data;
using Sillar.Modules.ServiceOrders.Services;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Tests;

public sealed class TransitionPostgresTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 7, 18, 30, 0, TimeSpan.Zero);

    [Fact]
    public Task A_legal_transition_commits_state_and_exactly_one_history_row()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);
            var id = Guid.CreateVersion7();
            await EphemeralDatabase.ExecuteAsync(connection, EphemeralDatabase.InsertOrder(id));

            await using var database = Context(connection);
            var audit = new RecordingAuditWriter();

            IServiceOrderTransitions transitions = Service(database, audit);
            var result = await transitions.TransitionAsync(
                id,
                ServiceOrderStatuses.Received,
                ServiceOrderStatuses.InProgress,
                CancellationToken.None);

            Assert.Equal(ServiceOrderOutcome.Ok, result.Outcome);
            Assert.Equal(ServiceOrderStatuses.InProgress, result.Value!.CurrentStatus);
            Assert.Equal("Ana Operadora", result.Value.HistoryEntry.PerformedBy!.DisplayName);
            Assert.Equal(19, result.Value.HistoryEntry.PerformedBy.AdminUserId);
            Assert.Equal("cuenta-lima", result.Value.HistoryEntry.PerformedBy.HomeNode);
            Assert.Equal(1, await HistoryCount(connection, id));
            Assert.Equal("principal", result.Value.HistoryEntry.OriginNode);
            Assert.Equal(ServiceOrderStatuses.InProgress, await Status(connection, id));

            Assert.Collection(
                audit.Entries,
                entry =>
                {
                    Assert.Equal(AuditAction.Update, entry.Action);
                    Assert.Equal(19, entry.AdminUserId);
                    Assert.Equal("ana@example.test", entry.AdminUserEmail);
                    Assert.Equal(ServiceOrdersModule.ModuleCode, entry.ModuleCode);
                    Assert.Equal("service_order", entry.EntityType);
                    Assert.Equal(id.ToString(), entry.EntityId);
                    Assert.Equal(
                        "Orden de servicio S-2026-0001 pasó de received a in_progress.",
                        entry.Summary);
                });
        });

    [Fact]
    public Task Invalid_and_missing_orders_produce_no_writes()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);
            var id = Guid.CreateVersion7();
            await EphemeralDatabase.ExecuteAsync(connection, EphemeralDatabase.InsertOrder(id));

            await using var database = Context(connection);
            var audit = new RecordingAuditWriter();

            IServiceOrderTransitions transitions = Service(database, audit);
            var invalid = await transitions.TransitionAsync(
                id,
                ServiceOrderStatuses.Received,
                ServiceOrderStatuses.Completed,
                CancellationToken.None);
            var missing = await transitions.TransitionAsync(
                Guid.CreateVersion7(),
                ServiceOrderStatuses.Received,
                ServiceOrderStatuses.InProgress,
                CancellationToken.None);

            Assert.Equal(ServiceOrderOutcome.Invalid, invalid.Outcome);
            Assert.Equal(ServiceOrderOutcome.NotFound, missing.Outcome);
            Assert.Equal(ServiceOrderStatuses.Received, await Status(connection, id));
            Assert.Equal(0, await HistoryCount(connection, id));
            Assert.Empty(audit.Entries);
        });

    [Fact]
    public Task Two_consumers_with_the_same_expected_state_yield_one_ok_and_one_conflict()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);
            var id = Guid.CreateVersion7();
            await EphemeralDatabase.ExecuteAsync(connection, EphemeralDatabase.InsertOrder(id));
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var audit = new RecordingAuditWriter();

            async Task<ServiceOrderOperation<ServiceOrderTransitionResult>> ChangeAsync(string target)
            {
                await using var database = Context(connection);
                await start.Task;
                IServiceOrderTransitions transitions = Service(database, audit);
                return await transitions.TransitionAsync(
                    id,
                    ServiceOrderStatuses.Received,
                    target,
                    CancellationToken.None);
            }

            var first = ChangeAsync(ServiceOrderStatuses.InProgress);
            var second = ChangeAsync(ServiceOrderStatuses.Cancelled);
            start.SetResult();
            var results = await Task.WhenAll(first, second);

            Assert.Single(results, result => result.Outcome == ServiceOrderOutcome.Ok);
            Assert.Single(results, result => result.Outcome == ServiceOrderOutcome.Conflict);
            Assert.Equal(1, await HistoryCount(connection, id));
            Assert.Equal(
                results.Single(result => result.Outcome == ServiceOrderOutcome.Ok).Value!.CurrentStatus,
                await Status(connection, id));
            Assert.Single(audit.Entries);
        });

    [Fact]
    public Task A_history_failure_rolls_back_the_state_change()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);
            var id = Guid.CreateVersion7();
            await EphemeralDatabase.ExecuteAsync(connection, EphemeralDatabase.InsertOrder(id));
            await EphemeralDatabase.ExecuteAsync(connection, """
                CREATE FUNCTION service_orders.reject_history() RETURNS trigger
                LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'sabotaje historial'; END $$;
                CREATE TRIGGER reject_history
                BEFORE INSERT ON service_orders.service_order_status_history
                FOR EACH ROW EXECUTE FUNCTION service_orders.reject_history();
                """);

            await using var database = Context(connection);
            var audit = new RecordingAuditWriter();

            IServiceOrderTransitions transitions = Service(database, audit);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => transitions.TransitionAsync(
                id,
                ServiceOrderStatuses.Received,
                ServiceOrderStatuses.InProgress,
                CancellationToken.None));

            var postgres = Assert.IsType<PostgresException>(error.InnerException);
            Assert.Contains("sabotaje historial", postgres.MessageText, StringComparison.Ordinal);
            Assert.Equal(ServiceOrderStatuses.Received, await Status(connection, id));
            Assert.Equal(0, await HistoryCount(connection, id));
            Assert.Empty(audit.Entries);
        });

    private static ServiceOrderTransitionService Service(
        ServiceOrdersDbContext database,
        IAuditWriter audit)
        => new(database, new Admin(), audit, new FixedTimeProvider(Now));

    private static ServiceOrdersDbContext Context(string connection)
        => new(
            new DbContextOptionsBuilder<ServiceOrdersDbContext>()
                .UseNpgsql(connection, options => options.MigrationsHistoryTable(
                    ServiceOrdersDbContext.MigrationsHistoryTable,
                    ServiceOrdersDbContext.Schema))
                .Options,
            new NodeIdentity("principal"),
            new FixedTimeProvider(Now));

    private static Task<string> Status(string connection, Guid id)
        => EphemeralDatabase.ScalarAsync<string>(connection,
            $"SELECT status FROM service_orders.service_orders WHERE service_order_id = '{id}'");

    private static Task<long> HistoryCount(string connection, Guid id)
        => EphemeralDatabase.ScalarAsync<long>(connection,
            $"SELECT count(*) FROM service_orders.service_order_status_history WHERE service_order_id = '{id}'");

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        private readonly object gate = new();
        private readonly List<AuditEntry> entries = [];

        public IReadOnlyList<AuditEntry> Entries
        {
            get
            {
                lock (gate)
                {
                    return entries.ToArray();
                }
            }
        }

        public Task WriteAsync(
            AuditEntry entry,
            CancellationToken cancellationToken)
        {
            lock (gate)
            {
                entries.Add(entry);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Admin : ICurrentAdmin
    {
        public int? AdminUserId => 19;
        public string? Email => "ana@example.test";
        public string? Role => "editor";
        public string? DisplayName => "Ana Operadora";
        public string? HomeNode => "cuenta-lima";
        public bool IsInRole(string role) => true;
    }
}
