using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.Tracking.Application;
using Sillar.Modules.Tracking.Data;
using Sillar.Modules.Tracking.Dtos;
using Sillar.Shared.Paging;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Tracking.Tests;

public sealed class TableroContratoPostgresTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 10, 8, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task La_vista_cerrada_respeta_la_paginacion_del_contrato()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var abierta = Resumen(Guid.CreateVersion7(), "OS-1", ServiceOrderStatuses.Received, Ahora.AddHours(-3));
            var cerrada1 = Resumen(Guid.CreateVersion7(), "OS-2", ServiceOrderStatuses.Completed, Ahora.AddHours(-2));
            var cerrada2 = Resumen(Guid.CreateVersion7(), "OS-3", ServiceOrderStatuses.Cancelled, Ahora.AddHours(-1));
            var source = new FuenteOrdenes([abierta, cerrada1, cerrada2]);

            await using var database = Contexto(connection);
            var board = await Servicio(database, source).GetBoardAsync(
                ServiceOrderScope.Closed,
                PageRequest.Of(1, 1),
                CancellationToken.None);

            Assert.NotNull(board.Pagination);
            Assert.Equal(1, board.Pagination.Page);
            Assert.Equal(1, board.Pagination.PageSize);
            Assert.Equal(2, board.Pagination.TotalItems);
            Assert.Equal(2, board.Pagination.TotalPages);
            Assert.True(board.Pagination.HasNext);
            Assert.Equal(1, board.Columns.Sum(column => column.Cards.Count));
            Assert.All(
                board.Columns.Where(column => column.Cards.Count > 0),
                column => Assert.True(column.IsTerminal));
            Assert.Single(source.Queries);
            Assert.Equal(ServiceOrderScope.Closed, source.Queries[0].Scope);
        });
    }

    [Fact]
    public async Task Reordenar_un_grupo_es_atomico_materializa_sus_pares_y_deja_el_orden_exacto()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var primero = Resumen(Guid.CreateVersion7(), "OS-1", ServiceOrderStatuses.Received, Ahora.AddHours(-3));
            var segundo = Resumen(Guid.CreateVersion7(), "OS-2", ServiceOrderStatuses.Received, Ahora.AddHours(-2));
            var tercero = Resumen(Guid.CreateVersion7(), "OS-3", ServiceOrderStatuses.Received, Ahora.AddHours(-1));
            var summaries = new[] { primero, segundo, tercero };

            foreach (var order in summaries)
            {
                await EphemeralDatabase.ExecuteAsync(
                    connection,
                    EphemeralDatabase.InsertOrder(
                        order.ServiceOrderId,
                        order.VisibleCode,
                        order.ReceivedAt.ToString("O")));
            }

            var source = new FuenteOrdenes(summaries);
            await using var database = Contexto(connection);
            var audit = new Auditoria();
            var service = Servicio(database, source, audit);
            var requested = new[]
            {
                tercero.ServiceOrderId,
                primero.ServiceOrderId,
                segundo.ServiceOrderId
            };

            var result = await service.SetPriorityAsync(
                tercero.ServiceOrderId,
                new SetTrackingPriorityRequest(null, false, requested),
                CancellationToken.None);

            Assert.Equal(TrackingOutcome.Ok, result.Outcome);
            Assert.Equal(3, await EphemeralDatabase.ScalarAsync<int>(
                connection,
                "SELECT count(*)::int FROM tracking.order_tracking;"));
            Assert.Equal(3, audit.Entries.Count);

            var storedOrder = await EphemeralDatabase.ScalarAsync<string>(
                connection,
                """
                SELECT string_agg(service_order_id::text, ',' ORDER BY board_priority)
                  FROM tracking.order_tracking
                 WHERE is_active AND NOT pinned;
                """);
            Assert.Equal(string.Join(',', requested), storedOrder);

            var board = await service.GetBoardAsync(CancellationToken.None);
            Assert.Equal(
                requested,
                board.Columns.Single(column => column.Status == ServiceOrderStatuses.Received)
                    .Cards.Select(card => card.ServiceOrderId));
        });
    }

    [Fact]
    public async Task Reordenar_con_una_lista_obsoleta_se_rechaza_sin_escribir()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var primero = Resumen(Guid.CreateVersion7(), "OS-1", ServiceOrderStatuses.Received, Ahora.AddHours(-2));
            var segundo = Resumen(Guid.CreateVersion7(), "OS-2", ServiceOrderStatuses.Received, Ahora.AddHours(-1));
            foreach (var order in new[] { primero, segundo })
            {
                await EphemeralDatabase.ExecuteAsync(
                    connection,
                    EphemeralDatabase.InsertOrder(
                        order.ServiceOrderId,
                        order.VisibleCode,
                        order.ReceivedAt.ToString("O")));
            }

            await using var database = Contexto(connection);
            var service = Servicio(database, new FuenteOrdenes([primero, segundo]));
            var result = await service.SetPriorityAsync(
                primero.ServiceOrderId,
                new SetTrackingPriorityRequest(
                    null,
                    false,
                    [primero.ServiceOrderId]),
                CancellationToken.None);

            Assert.Equal(TrackingOutcome.Invalid, result.Outcome);
            Assert.Contains("cambió", result.Error, StringComparison.Ordinal);
            Assert.Equal(0, await EphemeralDatabase.ScalarAsync<int>(
                connection,
                "SELECT count(*)::int FROM tracking.order_tracking;"));
        });
    }

    private static TrackingApplicationService Servicio(
        TrackingDbContext database,
        FuenteOrdenes source,
        Auditoria? audit = null)
        => new(
            database,
            source,
            new Transiciones(),
            new Administradora(),
            audit ?? new Auditoria(),
            new Reloj(Ahora));

    private static TrackingDbContext Contexto(string connection)
        => new(
            new DbContextOptionsBuilder<TrackingDbContext>()
                .UseNpgsql(connection, options => options.MigrationsHistoryTable(
                    TrackingDbContext.MigrationsHistoryTable,
                    TrackingDbContext.Schema))
                .Options,
            new NodeIdentity("principal"),
            new Reloj(Ahora));

    private static ServiceOrderTrackingSummary Resumen(
        Guid id,
        string code,
        string status,
        DateTimeOffset receivedAt)
        => new(id, code, "Cliente", status, receivedAt, null, null, receivedAt);

    private sealed class FuenteOrdenes(
        IEnumerable<ServiceOrderTrackingSummary> summaries) : IServiceOrderTrackingSource
    {
        private readonly List<ServiceOrderTrackingSummary> _summaries = summaries.ToList();
        internal List<ServiceOrderQuery> Queries { get; } = [];

        public Task<ServiceOrderTrackingSnapshot?> GetAsync(
            Guid serviceOrderId,
            CancellationToken cancellationToken)
        {
            var summary = _summaries.SingleOrDefault(order => order.ServiceOrderId == serviceOrderId);
            return Task.FromResult(summary is null ? null : Snapshot(summary));
        }

        public Task<PagedResult<ServiceOrderTrackingSummary>> ListAsync(
            ServiceOrderQuery query,
            CancellationToken cancellationToken)
        {
            Queries.Add(query);
            IEnumerable<ServiceOrderTrackingSummary> filtered = _summaries;

            filtered = query.Scope switch
            {
                ServiceOrderScope.Open => filtered.Where(order => !IsTerminal(order.CurrentStatus)),
                ServiceOrderScope.Closed => filtered.Where(order => IsTerminal(order.CurrentStatus)),
                _ => filtered
            };

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                filtered = filtered.Where(order => order.CurrentStatus == query.Status);
            }

            filtered = query.Direction == ServiceOrderSortDirection.Descending
                ? filtered.OrderByDescending(order => order.ReceivedAt)
                : filtered.OrderBy(order => order.ReceivedAt);

            var materialized = filtered.ToArray();
            var items = materialized.Skip(query.Page.Skip).Take(query.Page.Size).ToArray();
            return Task.FromResult(PagedResult<ServiceOrderTrackingSummary>.From(
                query.Page,
                items,
                materialized.LongLength));
        }

        private static bool IsTerminal(string status)
            => ServiceOrderStatuses.All.Single(state => state.Code == status).IsTerminal;

        private static ServiceOrderTrackingSnapshot Snapshot(ServiceOrderTrackingSummary order)
            => new(
                order.ServiceOrderId,
                order.VisibleCode,
                order.CustomerName,
                order.CurrentStatus,
                order.ReceivedAt,
                order.PromisedAt,
                [],
                order.CurrentAssignee,
                [],
                order.UpdatedAt);
    }

    private sealed class Transiciones : IServiceOrderTransitions
    {
        public Task<ServiceOrderOperation<ServiceOrderTransitionResult>> TransitionAsync(
            Guid serviceOrderId,
            string expectedStatus,
            string targetStatus,
            CancellationToken cancellationToken)
            => Task.FromResult(new ServiceOrderOperation<ServiceOrderTransitionResult>(
                ServiceOrderOutcome.NotFound,
                "No existe."));
    }

    private sealed class Administradora : ICurrentAdmin
    {
        public int? AdminUserId => 23;
        public string? Email => "rosa@example.test";
        public string? Role => AdminRole.Editor;
        public string? DisplayName => "Rosa Operadora";
        public string? HomeNode => "cuenta-lima";
        public bool IsInRole(string role) => true;
    }

    private sealed class Auditoria : IAuditWriter
    {
        internal List<AuditEntry> Entries { get; } = [];
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class Reloj(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
