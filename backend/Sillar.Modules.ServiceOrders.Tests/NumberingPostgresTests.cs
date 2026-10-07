using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders.Data;
using Sillar.Modules.ServiceOrders.Numbering;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Tests;

public sealed class NumberingPostgresTests
{
    [Fact]
    public Task Own_series_is_consecutive_under_concurrency()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<string> ReserveAsync()
            {
                await start.Task;
                await using var db = Context(connection);
                await using var transaction = await db.Database.BeginTransactionAsync();
                var code = await Allocator(db).NextAsync(CancellationToken.None);
                await transaction.CommitAsync();
                return code;
            }

            var reservations = Enumerable.Range(0, 8).Select(_ => ReserveAsync()).ToArray();
            start.SetResult();
            var codes = await Task.WhenAll(reservations);

            Assert.Equal(
                Enumerable.Range(1, 8).Select(number => $"S-2026-{number:0000}").Order(),
                codes.Order());
            Assert.Equal(8, await EphemeralDatabase.ScalarAsync<int>(connection,
                "SELECT last_number FROM service_orders.service_order_series WHERE node_code='principal' AND year=2026"));
        });

    [Fact]
    public Task Rollback_does_not_consume_a_number()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM05bAsync(connection);

            await using (var first = Context(connection))
            {
                await using var transaction = await first.Database.BeginTransactionAsync();
                Assert.Equal("S-2026-0001", await Allocator(first).NextAsync(CancellationToken.None));
                await transaction.RollbackAsync();
            }

            await using (var second = Context(connection))
            {
                await using var transaction = await second.Database.BeginTransactionAsync();
                Assert.Equal("S-2026-0001", await Allocator(second).NextAsync(CancellationToken.None));
                await transaction.CommitAsync();
            }

            Assert.Equal(1, await EphemeralDatabase.ScalarAsync<int>(connection,
                "SELECT last_number FROM service_orders.service_order_series WHERE node_code='principal' AND year=2026"));
        });

    [Fact]
    public async Task It_refuses_to_number_outside_the_document_transaction()
    {
        await using var database = Context("Host=localhost;Database=not_used;Username=none;Password=none");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Allocator(database).NextAsync(CancellationToken.None));
        Assert.Contains("transacción", error.Message, StringComparison.Ordinal);
        Assert.Contains("hueco", error.Message, StringComparison.Ordinal);
    }

    private static ServiceOrdersDbContext Context(string connection)
        => new(
            new DbContextOptionsBuilder<ServiceOrdersDbContext>()
                .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable(
                    ServiceOrdersDbContext.MigrationsHistoryTable,
                    ServiceOrdersDbContext.Schema))
                .Options,
            new NodeIdentity("principal"),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero)));

    private static ServiceOrderCodeAllocator Allocator(ServiceOrdersDbContext database)
        => new(
            database,
            new Settings("S"),
            new NodeIdentity("principal"),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero)));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Settings(string label) : ISettingsReader
    {
        public string? Get(string key) => key == ServiceOrderSettingsKeys.SeriesLabel ? label : null;
        public T? Get<T>(string key) => default;
        public IReadOnlyDictionary<string, string> GetPublic() => new Dictionary<string, string>();
    }
}
