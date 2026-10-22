using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Data;
using Sillar.Modules.ServiceOrders.Services;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Tests;

public sealed class TransitionMatrixPostgresTests
{
    // Oráculo fijo: deliberadamente NO se deriva de LegalTransitions.
    private static readonly string[] States =
        ["received", "in_progress", "ready", "completed", "cancelled"];

    private static readonly HashSet<string> Legal =
    [
        "received>in_progress", "received>cancelled",
        "in_progress>ready", "in_progress>cancelled",
        "ready>completed", "ready>in_progress", "ready>cancelled",
    ];

    [Fact]
    public Task Exactly_seven_legal_and_eighteen_illegal_status_pairs_are_enforced()
        => EphemeralDatabase.RunAsync(async connection =>
        {
            // BARRERA 1: lista y atributos de terminalidad son parte del contrato.
            Assert.Equal(States, ServiceOrderStatuses.All.Select(s => s.Code));
            Assert.Equal(new[] { "completed", "cancelled" },
                ServiceOrderStatuses.All.Where(s => s.IsTerminal).Select(s => s.Code));
            var published = ServiceOrderStatuses.LegalTransitions
                .Select(t => $"{t.FromStatus}>{t.ToStatus}")
                .ToHashSet(StringComparer.Ordinal);
            Assert.True(Legal.SetEquals(published),
                $"Mapa publicado distinto. Esperado: {string.Join(",", Legal.Order())}; " +
                $"actual: {string.Join(",", published.Order())}");

            await EphemeralDatabase.InstallM05bAsync(connection);
            var allowed = 0;
            var rejected = 0;
            var index = 0;

            // BARRERA 2: 25 operaciones REALES en base PostgreSQL temporal, no solo una lista.
            foreach (var from in States)
            foreach (var to in States)
            {
                index++;
                var id = Guid.CreateVersion7();
                await EphemeralDatabase.ExecuteAsync(connection,
                    EphemeralDatabase.InsertOrder(id, $"S-2026-{index:0000}"));

                // Estado inicial preparado por SQL solo en el fixture; acción real por la API de dominio.
                await EphemeralDatabase.ExecuteAsync(connection, $"""
                    UPDATE service_orders.service_orders
                       SET status = '{from}'
                     WHERE service_order_id = '{id}'
                    """);

                await using var database = new ServiceOrdersDbContext(
                    new DbContextOptionsBuilder<ServiceOrdersDbContext>()
                        .UseNpgsql(connection, options => options.MigrationsHistoryTable(
                            ServiceOrdersDbContext.MigrationsHistoryTable,
                            ServiceOrdersDbContext.Schema))
                        .Options,
                    new NodeIdentity("principal"),
                    new FixedClock());

                var service = new ServiceOrderTransitionService(
                    database, new TestAdmin(), new NoopAudit(), new FixedClock());
                var result = await service.TransitionAsync(id, from, to, CancellationToken.None);
                var isLegal = Legal.Contains($"{from}>{to}");

                var realStatus = await EphemeralDatabase.ScalarAsync<string>(connection,
                    $"SELECT status FROM service_orders.service_orders WHERE service_order_id = '{id}'");
                var rows = await EphemeralDatabase.ScalarAsync<long>(connection,
                    $"SELECT count(*) FROM service_orders.service_order_status_history WHERE service_order_id = '{id}'");

                if (isLegal)
                {
                    allowed++;
                    Assert.True(result.Outcome == ServiceOrderOutcome.Ok,
                        $"Legal {from}>{to}: {result.Outcome} {result.Error}");
                    Assert.Equal(to, realStatus);
                    Assert.Equal(1L, rows);
                }
                else
                {
                    rejected++;
                    Assert.True(result.Outcome == ServiceOrderOutcome.Invalid,
                        $"Ilegal {from}>{to}: {result.Outcome} {result.Error}");
                    Assert.Equal(from, realStatus);
                    Assert.Equal(0L, rows);
                }
            }

            Assert.Equal(25, index);
            Assert.Equal(7, allowed);
            Assert.Equal(18, rejected);
        });

    private sealed class TestAdmin : ICurrentAdmin
    {
        public int? AdminUserId => 19;
        public string? Email => "control@sillar.test";
        public string? Role => AdminRole.Admin;
        public string? DisplayName => "Operadora de QA";
        public string? HomeNode => "principal";
        public bool IsInRole(string role) => role == AdminRole.Admin;
    }

    private sealed class NoopAudit : IAuditWriter
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
            => new(2026, 10, 21, 15, 0, 0, TimeSpan.Zero);
    }
}
