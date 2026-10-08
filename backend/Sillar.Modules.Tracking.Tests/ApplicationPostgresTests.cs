using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.Tracking.Application;
using Sillar.Modules.Tracking.Data;
using Sillar.Modules.Tracking.Dtos;
using Sillar.Shared.Paging;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Tracking.Tests;

public sealed class AplicacionPostgresTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 10, 7, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task El_tablero_agota_paginas_ordena_globalmente_y_no_materializa_filas()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var summaries = Enumerable.Range(0, 201)
                .Select(index => Resumen(
                    Guid.CreateVersion7(),
                    $"OS-2026-{index + 1:0000}",
                    Ahora.AddMinutes(index)))
                .ToList();
            var prioritario = summaries[^1];
            var fijado = summaries[0];

            foreach (var order in new[] { prioritario, fijado })
            {
                await EphemeralDatabase.ExecuteAsync(
                    connection,
                    EphemeralDatabase.InsertOrder(
                        order.ServiceOrderId,
                        order.VisibleCode,
                        order.ReceivedAt.ToString("O")));
            }
            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertTracking(
                    Guid.CreateVersion7(),
                    prioritario.ServiceOrderId,
                    "0"));
            var fixedTracking = Guid.CreateVersion7();
            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertTracking(fixedTracking, fijado.ServiceOrderId));
            await EphemeralDatabase.ExecuteAsync(
                connection,
                $"UPDATE tracking.order_tracking SET pinned = true WHERE order_tracking_id = '{fixedTracking}';");

            var source = new FuenteOrdenes(summaries);
            await using var database = Contexto(connection);
            var result = await Servicio(database, source).GetBoardAsync(CancellationToken.None);

            Assert.Equal(2, source.ListCalls);
            Assert.All(source.PageSizes, size => Assert.Equal(PageRequest.MaxSize, size));
            Assert.Equal(
                ServiceOrderStatuses.All.Select(state => state.Code),
                result.Columns.Select(column => column.Status));
            Assert.Equal(
                fijado.ServiceOrderId,
                result.Columns.Single(column => column.Status == ServiceOrderStatuses.Received)
                    .Cards[0].ServiceOrderId);
            Assert.Equal(
                prioritario.ServiceOrderId,
                result.Columns.Single(column => column.Status == ServiceOrderStatuses.Received)
                    .Cards[1].ServiceOrderId);
            Assert.Equal(2, await FilasTracking(connection));
        });
    }

    [Fact]
    public async Task Sin_prioridad_la_tarjeta_con_o_sin_fila_conserva_el_orden_de_recepcion()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var temprano = Resumen(Guid.CreateVersion7(), "OS-1", Ahora.AddHours(-2));
            var conFila = Resumen(Guid.CreateVersion7(), "OS-2", Ahora.AddHours(-1));
            var tarde = Resumen(Guid.CreateVersion7(), "OS-3", Ahora);
            foreach (var order in new[] { temprano, conFila, tarde })
            {
                await EphemeralDatabase.ExecuteAsync(
                    connection,
                    EphemeralDatabase.InsertOrder(
                        order.ServiceOrderId,
                        order.VisibleCode,
                        order.ReceivedAt.ToString("O")));
            }

            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertTracking(Guid.CreateVersion7(), conFila.ServiceOrderId));

            await using var database = Contexto(connection);
            var board = await Servicio(database, new FuenteOrdenes([tarde, conFila, temprano]))
                .GetBoardAsync(CancellationToken.None);

            Assert.Equal(
                [temprano.ServiceOrderId, conFila.ServiceOrderId, tarde.ServiceOrderId],
                board.Columns.Single(column => column.Status == ServiceOrderStatuses.Received)
                    .Cards.Select(card => card.ServiceOrderId));
        });
    }

    [Fact]
    public async Task Prioridad_cero_es_valida_negativa_es_invalida_y_la_escritura_lazy_atribuye()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var order = Snapshot(Guid.CreateVersion7());
            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(order.ServiceOrderId, order.VisibleCode));

            var source = new FuenteOrdenes([Resumen(order)] , [order]);
            await using var database = Contexto(connection);
            var service = Servicio(database, source);

            var negative = await service.SetPriorityAsync(
                order.ServiceOrderId,
                new SetTrackingPriorityRequest(-1, false),
                CancellationToken.None);
            Assert.Equal(TrackingOutcome.Invalid, negative.Outcome);
            Assert.Equal(0, await FilasTracking(connection));

            var zero = await service.SetPriorityAsync(
                order.ServiceOrderId,
                new SetTrackingPriorityRequest(0, true),
                CancellationToken.None);
            Assert.Equal(TrackingOutcome.Ok, zero.Outcome);
            Assert.Equal(0, zero.Value!.BoardPriority);
            Assert.True(zero.Value.Pinned);

            var actor = await EphemeralDatabase.ScalarAsync<string>(
                connection,
                """
                SELECT concat_ws('|', last_touched_by_name, last_touched_by_admin_user_id, last_touched_by_admin_user_home_node)
                FROM tracking.order_tracking;
                """);
            Assert.Equal("Rosa Operadora|23|cuenta-lima", actor);
        });
    }

    [Fact]
    public async Task El_plazo_usa_received_at_del_contrato_y_solo_materializa_si_es_valido()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var order = Snapshot(Guid.CreateVersion7(), receivedAt: Ahora);
            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(
                    order.ServiceOrderId,
                    order.VisibleCode,
                    Ahora.AddDays(-10).ToString("O")));

            await using var database = Contexto(connection);
            var service = Servicio(database, new FuenteOrdenes([Resumen(order)], [order]));
            var invalid = await service.SetDueAsync(
                order.ServiceOrderId,
                new SetTrackingDueRequest(Ahora.AddMinutes(-1)),
                CancellationToken.None);

            Assert.Equal(TrackingOutcome.Invalid, invalid.Outcome);
            Assert.Contains("recepción", invalid.Error, StringComparison.Ordinal);
            Assert.Equal(0, await FilasTracking(connection));

            var valid = await service.SetDueAsync(
                order.ServiceOrderId,
                new SetTrackingDueRequest(Ahora.AddHours(2)),
                CancellationToken.None);
            Assert.Equal(TrackingOutcome.Ok, valid.Outcome);
            Assert.Equal(Ahora.AddHours(2), valid.Value!.InternalDueAt);
            Assert.Equal(1, await FilasTracking(connection));
        });
    }

    [Fact]
    public async Task Nota_vacia_no_escribe_y_nota_valida_conserva_prioridad_null_y_triple_completa()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var order = Snapshot(Guid.CreateVersion7());
            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(order.ServiceOrderId, order.VisibleCode));
            var audit = new Auditoria();
            await using var database = Contexto(connection);
            var service = Servicio(database, new FuenteOrdenes([Resumen(order)], [order]), audit: audit);

            var empty = await service.AddNoteAsync(
                order.ServiceOrderId,
                new AddTrackingNoteRequest("  "),
                CancellationToken.None);
            Assert.Equal(TrackingOutcome.Invalid, empty.Outcome);
            Assert.Equal("Escribe la nota antes de guardarla.", empty.Error);
            Assert.Equal(0, await FilasTracking(connection));

            var valid = await service.AddNoteAsync(
                order.ServiceOrderId,
                new AddTrackingNoteRequest("  Avance interno  "),
                CancellationToken.None);
            Assert.Equal(TrackingOutcome.Ok, valid.Outcome);
            Assert.Equal("Avance interno", valid.Value!.Body);

            var stored = await database.OrderTracking.AsNoTracking()
                .Include(row => row.Notes)
                .SingleAsync();
            Assert.Null(stored.BoardPriority);
            var note = Assert.Single(stored.Notes);
            Assert.Equal("Rosa Operadora", note.AuthorName);
            Assert.Equal(23, note.AuthorAdminUserId);
            Assert.Equal("cuenta-lima", note.AuthorAdminUserHomeNode);
            Assert.Contains("OS-2026-9001", Assert.Single(audit.Entries).Summary);
        });
    }

    [Fact]
    public async Task La_baja_de_nota_es_logica_y_no_borra_la_fila()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var order = Snapshot(Guid.CreateVersion7());
            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(order.ServiceOrderId, order.VisibleCode));
            await using var database = Contexto(connection);
            var service = Servicio(database, new FuenteOrdenes([Resumen(order)], [order]));
            var added = await service.AddNoteAsync(
                order.ServiceOrderId,
                new AddTrackingNoteRequest("Se revisó el material"),
                CancellationToken.None);

            var removed = await service.DeactivateNoteAsync(
                added.Value!.TrackingNoteId,
                CancellationToken.None);
            Assert.Equal(TrackingOutcome.Ok, removed.Outcome);
            Assert.False(removed.Value!.IsActive);
            Assert.Equal(1, await EphemeralDatabase.ScalarAsync<int>(
                connection,
                "SELECT count(*)::int FROM tracking.tracking_notes;"));
            Assert.False(await EphemeralDatabase.ScalarAsync<bool>(
                connection,
                "SELECT is_active FROM tracking.tracking_notes;"));
        });
    }

    [Fact]
    public async Task El_detalle_separa_la_orden_leida_de_M05b_de_los_datos_editables_de_M06()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var order = Snapshot(Guid.CreateVersion7());
            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(order.ServiceOrderId, order.VisibleCode));
            await using var database = Contexto(connection);
            var service = Servicio(database, new FuenteOrdenes([Resumen(order)], [order]));
            await service.AddNoteAsync(
                order.ServiceOrderId,
                new AddTrackingNoteRequest("Nota propia de M06"),
                CancellationToken.None);
            await service.SetPriorityAsync(
                order.ServiceOrderId,
                new SetTrackingPriorityRequest(3, false),
                CancellationToken.None);

            var detail = await service.GetDetailAsync(order.ServiceOrderId, CancellationToken.None);

            Assert.NotNull(detail);
            Assert.Equal(order.VisibleCode, detail.Order.VisibleCode);
            Assert.Equal(order.CurrentStatus, detail.Order.CurrentStatus);
            Assert.Equal(3, detail.Tracking.BoardPriority);
            Assert.Equal("Nota propia de M06", Assert.Single(detail.Tracking.Notes).Body);
        });
    }

    [Fact]
    public async Task Conflicto_no_reintenta_invalid_conserva_mensaje_y_ninguna_transicion_escribe_tracking()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var id = Guid.CreateVersion7();
            await using var database = Contexto(connection);
            var transition = new Transiciones
            {
                Result = new(ServiceOrderOutcome.Conflict, "Estado ya cambiado por otra persona.")
            };
            var service = Servicio(database, new FuenteOrdenes([]), transition);

            var conflict = await service.TransitionAsync(
                id,
                new TransitionTrackingStatusRequest("received", "in_progress"),
                CancellationToken.None);
            Assert.Equal(ServiceOrderOutcome.Conflict, conflict.Outcome);
            Assert.Equal(1, transition.Calls);
            Assert.Equal("received", transition.ExpectedStatus);
            Assert.Equal(0, await FilasTracking(connection));

            transition.Result = new(ServiceOrderOutcome.Invalid, "Frase exacta de M05b.");
            var invalid = await service.TransitionAsync(
                id,
                new TransitionTrackingStatusRequest("received", "completed"),
                CancellationToken.None);
            Assert.Equal(ServiceOrderOutcome.Invalid, invalid.Outcome);
            Assert.Equal("Frase exacta de M05b.", invalid.Error);
            Assert.Equal(0, await FilasTracking(connection));
        });
    }

    [Fact]
    public async Task Transicion_exitosa_no_crea_tracking_y_el_tablero_refleja_la_relectura_sin_eventos()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var order = Snapshot(Guid.CreateVersion7());
            var source = new FuenteOrdenes([Resumen(order)], [order]);
            var history = order.StatusHistory[0];
            var transition = new Transiciones
            {
                Result = new(
                    ServiceOrderOutcome.Ok,
                    Value: new ServiceOrderTransitionResult(
                        order.ServiceOrderId,
                        ServiceOrderStatuses.InProgress,
                        history))
            };
            await using var database = Contexto(connection);
            var service = Servicio(database, source, transition);

            var result = await service.TransitionAsync(
                order.ServiceOrderId,
                new TransitionTrackingStatusRequest(
                    ServiceOrderStatuses.Received,
                    ServiceOrderStatuses.InProgress),
                CancellationToken.None);
            Assert.Equal(ServiceOrderOutcome.Ok, result.Outcome);
            Assert.Equal(0, await FilasTracking(connection));

            source.Summaries[0] = source.Summaries[0] with
            {
                CurrentStatus = ServiceOrderStatuses.InProgress
            };
            var board = await service.GetBoardAsync(CancellationToken.None);
            Assert.Empty(board.Columns.Single(column => column.Status == ServiceOrderStatuses.Received).Cards);
            Assert.Single(board.Columns.Single(column => column.Status == ServiceOrderStatuses.InProgress).Cards);
        });
    }

    [Fact]
    public async Task Escrituras_propias_no_cambian_estado_historial_ni_row_version_de_M05b()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var order = Snapshot(Guid.CreateVersion7());
            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(order.ServiceOrderId, order.VisibleCode));
            await EphemeralDatabase.ExecuteAsync(
                connection,
                $"""
                INSERT INTO service_orders.service_order_status_history
                    (status_history_id, service_order_id, from_status, to_status, occurred_at, origin_node, row_version)
                VALUES
                    ('{Guid.CreateVersion7()}', '{order.ServiceOrderId}', NULL, 'received', '{order.ReceivedAt:O}', 'principal', 1);
                """);
            var before = await M05bFingerprint(connection, order.ServiceOrderId);
            await using var database = Contexto(connection);
            var service = Servicio(database, new FuenteOrdenes([Resumen(order)], [order]));

            await service.SetPriorityAsync(
                order.ServiceOrderId,
                new SetTrackingPriorityRequest(4, true),
                CancellationToken.None);
            await service.SetDueAsync(
                order.ServiceOrderId,
                new SetTrackingDueRequest(Ahora.AddDays(1)),
                CancellationToken.None);

            Assert.Equal(before, await M05bFingerprint(connection, order.ServiceOrderId));
        });
    }

    [Fact]
    public async Task Dos_primeras_escrituras_concurrentes_comparten_una_fila_y_conservan_ambos_efectos()
    {
        await EphemeralDatabase.RunAsync(async connection =>
        {
            await EphemeralDatabase.InstallM06Async(connection);
            var order = Snapshot(Guid.CreateVersion7());
            await EphemeralDatabase.ExecuteAsync(
                connection,
                EphemeralDatabase.InsertOrder(order.ServiceOrderId, order.VisibleCode));
            await EphemeralDatabase.ExecuteAsync(
                connection,
                $"""
                INSERT INTO service_orders.service_order_status_history
                    (status_history_id, service_order_id, from_status, to_status, occurred_at, origin_node, row_version)
                VALUES
                    ('{Guid.CreateVersion7()}', '{order.ServiceOrderId}', NULL, 'received', '{order.ReceivedAt:O}', 'principal', 1);
                """);
            var before = await M05bFingerprint(connection, order.ServiceOrderId);
            var source = new FuenteOrdenes([Resumen(order)], [order]);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<TrackingOperation<TrackingNoteResponse>> AddNoteAsync()
            {
                await using var database = Contexto(connection);
                var service = Servicio(database, source);
                await start.Task;
                return await service.AddNoteAsync(
                    order.ServiceOrderId,
                    new AddTrackingNoteRequest("Primera nota concurrente"),
                    CancellationToken.None);
            }

            async Task<TrackingOperation<TrackingMutationResponse>> SetDueAsync()
            {
                await using var database = Contexto(connection);
                var service = Servicio(database, source);
                await start.Task;
                return await service.SetDueAsync(
                    order.ServiceOrderId,
                    new SetTrackingDueRequest(Ahora.AddDays(1)),
                    CancellationToken.None);
            }

            var noteTask = AddNoteAsync();
            var dueTask = SetDueAsync();
            start.SetResult();
            var note = await noteTask;
            var due = await dueTask;

            Assert.Equal(TrackingOutcome.Ok, note.Outcome);
            Assert.Equal(TrackingOutcome.Ok, due.Outcome);
            Assert.Equal(1, await FilasTracking(connection));
            Assert.Equal(1, await EphemeralDatabase.ScalarAsync<int>(
                connection,
                "SELECT count(*)::int FROM tracking.tracking_notes WHERE is_active;"));
            Assert.Equal(
                Ahora.AddDays(1).UtcDateTime,
                (await EphemeralDatabase.ScalarAsync<DateTime>(
                    connection,
                    "SELECT internal_due_at FROM tracking.order_tracking;")).ToUniversalTime());
            Assert.Equal(before, await M05bFingerprint(connection, order.ServiceOrderId));
        });
    }

    private static TrackingApplicationService Servicio(
        TrackingDbContext database,
        FuenteOrdenes source,
        Transiciones? transitions = null,
        Auditoria? audit = null)
        => new(
            database,
            source,
            transitions ?? new Transiciones(),
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
        DateTimeOffset receivedAt,
        string status = "received")
        => new(id, code, "Cliente", status, receivedAt, null, null, receivedAt);

    private static ServiceOrderTrackingSummary Resumen(ServiceOrderTrackingSnapshot order)
        => new(
            order.ServiceOrderId,
            order.VisibleCode,
            order.CustomerName,
            order.CurrentStatus,
            order.ReceivedAt,
            order.PromisedAt,
            order.CurrentAssignee,
            order.UpdatedAt);

    private static ServiceOrderTrackingSnapshot Snapshot(
        Guid id,
        DateTimeOffset? receivedAt = null)
    {
        var received = receivedAt ?? Ahora.AddHours(-2);
        return new(
            id,
            "OS-2026-9001",
            "Cliente prueba",
            ServiceOrderStatuses.Received,
            received,
            null,
            [],
            null,
            [new ServiceOrderStatusHistorySnapshot(
                Guid.CreateVersion7(),
                null,
                ServiceOrderStatuses.Received,
                received,
                null,
                "principal")],
            received);
    }

    private static Task<int> FilasTracking(string connection)
        => EphemeralDatabase.ScalarAsync<int>(
            connection,
            "SELECT count(*)::int FROM tracking.order_tracking;");

    private static Task<string> M05bFingerprint(string connection, Guid id)
        => EphemeralDatabase.ScalarAsync<string>(
            connection,
            $"""
            SELECT concat_ws('|', status, row_version,
                (SELECT count(*) FROM service_orders.service_order_status_history h WHERE h.service_order_id = o.service_order_id),
                (SELECT coalesce(sum(h.row_version), 0) FROM service_orders.service_order_status_history h WHERE h.service_order_id = o.service_order_id))
            FROM service_orders.service_orders o
            WHERE service_order_id = '{id}';
            """);

    private sealed class FuenteOrdenes(
        IEnumerable<ServiceOrderTrackingSummary> summaries,
        IEnumerable<ServiceOrderTrackingSnapshot>? snapshots = null) : IServiceOrderTrackingSource
    {
        internal List<ServiceOrderTrackingSummary> Summaries { get; } = summaries.ToList();
        private readonly Dictionary<Guid, ServiceOrderTrackingSnapshot> _snapshots =
            (snapshots ?? []).ToDictionary(order => order.ServiceOrderId);
        internal int ListCalls { get; private set; }
        internal List<int> PageSizes { get; } = [];

        public Task<ServiceOrderTrackingSnapshot?> GetAsync(Guid serviceOrderId, CancellationToken cancellationToken)
            => Task.FromResult(_snapshots.GetValueOrDefault(serviceOrderId));

        public Task<PagedResult<ServiceOrderTrackingSummary>> ListAsync(
            ServiceOrderQuery query,
            CancellationToken cancellationToken)
        {
            ListCalls++;
            PageSizes.Add(query.Page.Size);
            var items = Summaries.Skip(query.Page.Skip).Take(query.Page.Size).ToArray();
            return Task.FromResult(PagedResult<ServiceOrderTrackingSummary>.From(
                query.Page,
                items,
                Summaries.Count));
        }
    }

    private sealed class Transiciones : IServiceOrderTransitions
    {
        internal ServiceOrderOperation<ServiceOrderTransitionResult> Result { get; set; } =
            new(ServiceOrderOutcome.NotFound, "No existe.");
        internal int Calls { get; private set; }
        internal string? ExpectedStatus { get; private set; }

        public Task<ServiceOrderOperation<ServiceOrderTransitionResult>> TransitionAsync(
            Guid serviceOrderId,
            string expectedStatus,
            string targetStatus,
            CancellationToken cancellationToken)
        {
            Calls++;
            ExpectedStatus = expectedStatus;
            return Task.FromResult(Result);
        }
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
