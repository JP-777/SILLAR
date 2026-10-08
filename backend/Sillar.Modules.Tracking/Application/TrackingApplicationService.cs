using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.Tracking.Data;
using Sillar.Modules.Tracking.Domain;
using Sillar.Modules.Tracking.Dtos;
using Sillar.Shared.Paging;

namespace Sillar.Modules.Tracking.Application;

/// <summary>Casos de uso administrativos de M06; nunca accede al schema de M05b.</summary>
internal sealed class TrackingApplicationService(
    TrackingDbContext database,
    IServiceOrderTrackingSource orders,
    IServiceOrderTransitions transitions,
    ICurrentAdmin currentAdmin,
    IAuditWriter audit,
    TimeProvider clock)
{
    internal Task<TrackingBoardResponse> GetBoardAsync(CancellationToken cancellationToken)
        => GetBoardAsync(
            ServiceOrderScope.Open,
            PageRequest.Of(1, PageRequest.MaxSize),
            cancellationToken);

    internal async Task<TrackingBoardResponse> GetBoardAsync(
        ServiceOrderScope scope,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        var selectedOrders = new List<ServiceOrderTrackingSummary>();
        PagedResult<ServiceOrderTrackingSummary>? paged = null;

        if (scope == ServiceOrderScope.Open)
        {
            var pageNumber = 1;
            while (true)
            {
                var page = await orders.ListAsync(
                    new ServiceOrderQuery(
                        ServiceOrderScope.Open,
                        Status: null,
                        ServiceOrderSort.ReceivedAt,
                        ServiceOrderSortDirection.Ascending,
                        PageRequest.Of(pageNumber, PageRequest.MaxSize)),
                    cancellationToken);

                selectedOrders.AddRange(page.Items);
                if (!page.HasNext)
                {
                    break;
                }

                pageNumber++;
            }
        }
        else
        {
            paged = await orders.ListAsync(
                new ServiceOrderQuery(
                    scope,
                    Status: null,
                    ServiceOrderSort.ReceivedAt,
                    ServiceOrderSortDirection.Descending,
                    pageRequest),
                cancellationToken);
            selectedOrders.AddRange(paged.Items);
        }

        var orderIds = selectedOrders.Select(order => order.ServiceOrderId).Distinct().ToArray();
        var ownRows = orderIds.Length == 0
            ? new Dictionary<Guid, OwnBoardData>()
            : await database.OrderTracking
                .AsNoTracking()
                .Where(row => row.IsActive && orderIds.Contains(row.ServiceOrderId))
                .Select(row => new OwnBoardData(
                    row.ServiceOrderId,
                    row.BoardPriority,
                    row.Pinned,
                    row.InternalDueAt))
                .ToDictionaryAsync(row => row.ServiceOrderId, cancellationToken);

        var cards = selectedOrders.Select(order =>
        {
            ownRows.TryGetValue(order.ServiceOrderId, out var own);
            return new TrackingBoardCardResponse(
                order.ServiceOrderId,
                order.VisibleCode,
                order.CustomerName,
                order.CurrentStatus,
                order.ReceivedAt,
                order.PromisedAt,
                Staff(order.CurrentAssignee),
                own?.BoardPriority,
                own?.Pinned ?? false,
                own?.InternalDueAt,
                order.UpdatedAt);
        }).ToArray();

        var columns = ServiceOrderStatuses.All
            .OrderBy(state => state.DisplayOrder)
            .Select(state => new TrackingBoardColumnResponse(
                state.Code,
                state.DisplayName,
                state.DisplayOrder,
                state.IsTerminal,
                ServiceOrderStatuses.LegalTransitions
                    .Where(transition => transition.FromStatus == state.Code)
                    .Select(transition => transition.ToStatus)
                    .ToArray(),
                SortCards(
                    cards.Where(card => card.CurrentStatus == state.Code),
                    scope)))
            .ToArray();

        var pagination = paged is null
            ? null
            : new TrackingBoardPaginationResponse(
                paged.Page,
                paged.PageSize,
                paged.TotalItems,
                paged.TotalPages,
                paged.HasNext);

        return new TrackingBoardResponse(columns, pagination);
    }

    internal async Task<TrackingOrderDetailResponse?> GetDetailAsync(
        Guid serviceOrderId,
        CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(serviceOrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var own = await database.OrderTracking
            .AsNoTracking()
            .Include(row => row.Notes.Where(note => note.IsActive))
            .SingleOrDefaultAsync(
                row => row.ServiceOrderId == serviceOrderId && row.IsActive,
                cancellationToken);

        return Detail(order, own);
    }

    internal async Task<TrackingOperation<TrackingMutationResponse>> SetPriorityAsync(
        Guid serviceOrderId,
        SetTrackingPriorityRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BoardPriority is < 0)
        {
            return Invalid<TrackingMutationResponse>(
                "La prioridad manual no puede ser negativa.",
                "boardPriority");
        }

        if (request.OrderedPeerIds is not null && request.BoardPriority is not null)
        {
            return Invalid<TrackingMutationResponse>(
                "Envía una prioridad directa o un orden de pares, no ambos a la vez.",
                "orderedPeerIds");
        }

        var order = await orders.GetAsync(serviceOrderId, cancellationToken);
        if (order is null)
        {
            return Missing<TrackingMutationResponse>();
        }

        if (request.OrderedPeerIds is { } orderedPeerIds)
        {
            return await ReorderPriorityAsync(
                order,
                request.Pinned,
                orderedPeerIds,
                cancellationToken);
        }

        await using var transaction = await BeginBoardWriteAsync(order.CurrentStatus, cancellationToken);
        var row = await GetOrCreateAsync(serviceOrderId, cancellationToken);
        row.BoardPriority = request.BoardPriority;
        row.Pinned = request.Pinned;
        Touch(row);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await AuditAsync(
            AuditAction.Update,
            "order_tracking",
            row.OrderTrackingId,
            $"Prioridad de la orden «{order.VisibleCode}» cambiada.",
            cancellationToken);

        return Ok(Mutation(row));
    }

    private async Task<TrackingOperation<TrackingMutationResponse>> ReorderPriorityAsync(
        ServiceOrderTrackingSnapshot targetOrder,
        bool pinned,
        IReadOnlyList<Guid> orderedPeerIds,
        CancellationToken cancellationToken)
    {
        if (orderedPeerIds.Count == 0
            || orderedPeerIds.Distinct().Count() != orderedPeerIds.Count
            || !orderedPeerIds.Contains(targetOrder.ServiceOrderId))
        {
            return Invalid<TrackingMutationResponse>(
                "El orden enviado debe contener una sola vez la orden que se está moviendo.",
                "orderedPeerIds");
        }

        await using var transaction = await BeginBoardWriteAsync(
            targetOrder.CurrentStatus,
            cancellationToken);

        var currentStatusOrders = await ListStatusAsync(
            targetOrder.CurrentStatus,
            cancellationToken);
        var statusIds = currentStatusOrders.Select(order => order.ServiceOrderId).ToArray();
        var existingRows = statusIds.Length == 0
            ? new Dictionary<Guid, OrderTracking>()
            : await database.OrderTracking
                .Where(row => row.IsActive && statusIds.Contains(row.ServiceOrderId))
                .ToDictionaryAsync(row => row.ServiceOrderId, cancellationToken);

        bool EffectivePinned(Guid id)
            => id == targetOrder.ServiceOrderId
                ? pinned
                : existingRows.TryGetValue(id, out var row) && row.Pinned;

        var expectedPeers = currentStatusOrders
            .Where(order => EffectivePinned(order.ServiceOrderId) == pinned)
            .Select(order => order.ServiceOrderId)
            .ToHashSet();
        var requestedPeers = orderedPeerIds.ToHashSet();

        if (!expectedPeers.SetEquals(requestedPeers))
        {
            return Invalid<TrackingMutationResponse>(
                "El tablero cambió mientras se reordenaba. Recárgalo antes de volver a intentarlo.",
                "orderedPeerIds");
        }

        var visibleCodes = currentStatusOrders.ToDictionary(
            order => order.ServiceOrderId,
            order => order.VisibleCode);
        var changedRows = new List<OrderTracking>(orderedPeerIds.Count);

        for (var index = 0; index < orderedPeerIds.Count; index++)
        {
            var id = orderedPeerIds[index];
            if (!existingRows.TryGetValue(id, out var row))
            {
                row = new OrderTracking { ServiceOrderId = id };
                existingRows.Add(id, row);
                database.OrderTracking.Add(row);
            }

            row.Pinned = pinned;
            row.BoardPriority = index;
            Touch(row);
            changedRows.Add(row);
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var row in changedRows)
        {
            await AuditAsync(
                AuditAction.Update,
                "order_tracking",
                row.OrderTrackingId,
                $"Orden de trabajo de la orden «{visibleCodes[row.ServiceOrderId]}» cambiada.",
                cancellationToken);
        }

        return Ok(Mutation(existingRows[targetOrder.ServiceOrderId]));
    }

    internal async Task<TrackingOperation<TrackingMutationResponse>> SetDueAsync(
        Guid serviceOrderId,
        SetTrackingDueRequest request,
        CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(serviceOrderId, cancellationToken);
        if (order is null)
        {
            return Missing<TrackingMutationResponse>();
        }

        if (request.InternalDueAt is { } due && due < order.ReceivedAt)
        {
            return Invalid<TrackingMutationResponse>(
                $"El plazo no puede ser anterior a la recepción de la orden, que fue el {order.ReceivedAt:dd/MM/yyyy HH:mm}.",
                "internalDueAt");
        }

        await using var transaction = await BeginLazyWriteAsync(serviceOrderId, cancellationToken);
        var row = await GetOrCreateAsync(serviceOrderId, cancellationToken);
        row.InternalDueAt = request.InternalDueAt;
        Touch(row);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await AuditAsync(
            AuditAction.Update,
            "order_tracking",
            row.OrderTrackingId,
            $"Plazo interno de la orden «{order.VisibleCode}» cambiado.",
            cancellationToken);

        return Ok(Mutation(row));
    }

    internal async Task<TrackingOperation<TrackingNoteResponse>> AddNoteAsync(
        Guid serviceOrderId,
        AddTrackingNoteRequest request,
        CancellationToken cancellationToken)
    {
        var body = request.Body?.Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return Invalid<TrackingNoteResponse>(
                "Escribe la nota antes de guardarla.",
                "body");
        }

        var order = await orders.GetAsync(serviceOrderId, cancellationToken);
        if (order is null)
        {
            return Missing<TrackingNoteResponse>();
        }

        await using var transaction = await BeginLazyWriteAsync(serviceOrderId, cancellationToken);
        var row = await GetOrCreateAsync(serviceOrderId, cancellationToken);
        var actor = Actor();
        var note = new TrackingNote
        {
            OrderTrackingId = row.OrderTrackingId,
            Body = body,
            AuthorName = actor?.DisplayName,
            AuthorAdminUserId = actor?.AdminUserId,
            AuthorAdminUserHomeNode = actor?.HomeNode
        };

        row.Notes.Add(note);
        Touch(row, actor);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await AuditAsync(
            AuditAction.Create,
            "tracking_note",
            note.TrackingNoteId,
            $"Nota de seguimiento añadida a la orden «{order.VisibleCode}».",
            cancellationToken);

        return Ok(Note(note));
    }

    internal async Task<TrackingOperation<TrackingNoteResponse>> DeactivateNoteAsync(
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var note = await database.TrackingNotes
            .Include(candidate => candidate.OrderTracking)
            .SingleOrDefaultAsync(
                candidate => candidate.TrackingNoteId == noteId && candidate.IsActive,
                cancellationToken);
        if (note?.OrderTracking is null)
        {
            return Missing<TrackingNoteResponse>("La nota de seguimiento no existe o ya fue dada de baja.");
        }

        var order = await orders.GetAsync(note.OrderTracking.ServiceOrderId, cancellationToken);
        if (order is null)
        {
            return Missing<TrackingNoteResponse>();
        }

        note.IsActive = false;
        Touch(note.OrderTracking);
        await database.SaveChangesAsync(cancellationToken);
        await AuditAsync(
            AuditAction.Delete,
            "tracking_note",
            note.TrackingNoteId,
            $"Nota de seguimiento de la orden «{order.VisibleCode}» dada de baja.",
            cancellationToken);

        return Ok(Note(note));
    }

    internal async Task<ServiceOrderOperation<TransitionTrackingStatusResponse>> TransitionAsync(
        Guid serviceOrderId,
        TransitionTrackingStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExpectedStatus)
            || string.IsNullOrWhiteSpace(request.TargetStatus))
        {
            return new(
                ServiceOrderOutcome.Invalid,
                "Indica el estado visto y el estado de destino.");
        }

        var result = await transitions.TransitionAsync(
            serviceOrderId,
            request.ExpectedStatus,
            request.TargetStatus,
            cancellationToken);

        return result.Outcome == ServiceOrderOutcome.Ok
            ? new(
                ServiceOrderOutcome.Ok,
                Value: new TransitionTrackingStatusResponse(
                    result.Value!.ServiceOrderId,
                    result.Value.CurrentStatus,
                    History(result.Value.HistoryEntry)))
            : new(result.Outcome, result.Error);
    }

    private async Task<IReadOnlyList<ServiceOrderTrackingSummary>> ListStatusAsync(
        string status,
        CancellationToken cancellationToken)
    {
        var result = new List<ServiceOrderTrackingSummary>();
        var pageNumber = 1;

        while (true)
        {
            var page = await orders.ListAsync(
                new ServiceOrderQuery(
                    ServiceOrderScope.All,
                    status,
                    ServiceOrderSort.ReceivedAt,
                    ServiceOrderSortDirection.Ascending,
                    PageRequest.Of(pageNumber, PageRequest.MaxSize)),
                cancellationToken);
            result.AddRange(page.Items);

            if (!page.HasNext)
            {
                return result;
            }

            pageNumber++;
        }
    }

    private async Task<OrderTracking> GetOrCreateAsync(
        Guid serviceOrderId,
        CancellationToken cancellationToken)
    {
        _ = database.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "La fila lazy de Tracking solo puede materializarse dentro de su operación transaccional.");

        var row = await database.OrderTracking.SingleOrDefaultAsync(
            candidate => candidate.ServiceOrderId == serviceOrderId,
            cancellationToken);
        if (row is not null)
        {
            return row;
        }

        row = new OrderTracking { ServiceOrderId = serviceOrderId };
        database.OrderTracking.Add(row);
        return row;
    }

    private async Task<IDbContextTransaction> BeginBoardWriteAsync(
        string status,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var lockName = $"tracking:board:{status}";
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockName}, 0))",
                cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private async Task<IDbContextTransaction> BeginLazyWriteAsync(
        Guid serviceOrderId,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var lockName = $"tracking:order:{serviceOrderId:D}";

            // Serializa únicamente la primera escritura propia de esta orden. No lee ni
            // bloquea tablas de M05b y se libera automáticamente con la transacción.
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockName}, 0))",
                cancellationToken);

            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private void Touch(OrderTracking row, FrozenActor? actor = null)
    {
        actor ??= Actor();
        row.LastTouchedByName = actor?.DisplayName;
        row.LastTouchedByAdminUserId = actor?.AdminUserId;
        row.LastTouchedByAdminUserHomeNode = actor?.HomeNode;
        row.LastTouchedAt = clock.GetUtcNow();
    }

    private FrozenActor? Actor()
    {
        var name = currentAdmin.DisplayName?.Trim();
        var node = currentAdmin.HomeNode?.Trim();
        return currentAdmin.AdminUserId is > 0 and var id
            && !string.IsNullOrWhiteSpace(name)
            && !string.IsNullOrWhiteSpace(node)
                ? new FrozenActor(name, id, node)
                : null;
    }

    private Task AuditAsync(
        string action,
        string entityType,
        Guid entityId,
        string summary,
        CancellationToken cancellationToken)
        => audit.WriteAsync(
            new AuditEntry(action)
            {
                AdminUserId = currentAdmin.AdminUserId,
                AdminUserEmail = currentAdmin.Email,
                ModuleCode = TrackingModule.ModuleCode,
                EntityType = entityType,
                EntityId = entityId.ToString(),
                Summary = summary
            },
            cancellationToken);

    private static IReadOnlyList<TrackingBoardCardResponse> SortCards(
        IEnumerable<TrackingBoardCardResponse> cards,
        ServiceOrderScope scope)
        => scope == ServiceOrderScope.Closed
            ? cards.OrderByDescending(card => card.ReceivedAt)
                .ThenBy(card => card.ServiceOrderId)
                .ToArray()
            : cards.OrderByDescending(card => card.Pinned)
                .ThenBy(card => card.BoardPriority.HasValue ? 0 : 1)
                .ThenBy(card => card.BoardPriority)
                .ThenBy(card => card.ReceivedAt)
                .ThenBy(card => card.ServiceOrderId)
                .ToArray();

    private static TrackingOrderDetailResponse Detail(
        ServiceOrderTrackingSnapshot order,
        OrderTracking? own)
        => new(
            new TrackingOrderReadOnlyResponse(
                order.ServiceOrderId,
                order.VisibleCode,
                order.CustomerName,
                order.CurrentStatus,
                order.ReceivedAt,
                order.PromisedAt,
                order.Items.Select(item => new TrackingWorkItemResponse(
                    item.ServiceOrderItemId,
                    item.ServiceName,
                    item.SaleUnit,
                    item.Quantity,
                    item.RequestedDetails)).ToArray(),
                Staff(order.CurrentAssignee),
                order.StatusHistory.Select(History).ToArray(),
                order.UpdatedAt),
            new TrackingEditableResponse(
                own?.BoardPriority,
                own?.Pinned ?? false,
                own?.InternalDueAt,
                own?.Notes.Where(note => note.IsActive)
                    .OrderBy(note => note.CreatedAt)
                    .ThenBy(note => note.TrackingNoteId)
                    .Select(Note)
                    .ToArray() ?? []));

    private static TrackingMutationResponse Mutation(OrderTracking row)
        => new(
            row.ServiceOrderId,
            row.BoardPriority,
            row.Pinned,
            row.InternalDueAt,
            row.UpdatedAt);

    private static TrackingNoteResponse Note(TrackingNote note)
        => new(
            note.TrackingNoteId,
            note.Body,
            note.AuthorName is null
                ? null
                : new TrackingStaffResponse(
                    note.AuthorName,
                    note.AuthorAdminUserId!.Value,
                    note.AuthorAdminUserHomeNode!),
            note.CreatedAt,
            note.IsActive);

    private static TrackingStatusHistoryResponse History(ServiceOrderStatusHistorySnapshot history)
        => new(
            history.StatusHistoryId,
            history.FromStatus,
            history.ToStatus,
            history.OccurredAt,
            Staff(history.PerformedBy),
            history.OriginNode);

    private static TrackingStaffResponse? Staff(StaffSnapshot? staff)
        => staff is null
            ? null
            : new TrackingStaffResponse(staff.DisplayName, staff.AdminUserId, staff.HomeNode);

    private static TrackingOperation<T> Ok<T>(T value)
        => new(TrackingOutcome.Ok, Value: value);

    private static TrackingOperation<T> Missing<T>(
        string error = "Esa orden ya no está disponible. Vuelve al tablero para ver las actuales.")
        => new(TrackingOutcome.NotFound, error);

    private static TrackingOperation<T> Invalid<T>(string error, string field)
        => new(TrackingOutcome.Invalid, error, field);

    private sealed record OwnBoardData(
        Guid ServiceOrderId,
        int? BoardPriority,
        bool Pinned,
        DateTimeOffset? InternalDueAt);

    private sealed record FrozenActor(string DisplayName, int AdminUserId, string HomeNode);
}
