using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Data;
using Sillar.Modules.ServiceOrders.Domain;
using Sillar.Modules.ServiceOrders.Numbering;
using Sillar.Modules.ServiceOrders.Services;
using Sillar.Modules.Services.Contracts;
using Sillar.Shared.Paging;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Application;

/// <summary>
/// Operaciones de aplicación de M05b. El estado y su historia siguen siendo
/// propiedad de <see cref="ServiceOrderTransitionService"/>.
/// </summary>
internal sealed class ServiceOrderApplicationService(
    ServiceOrdersDbContext database,
    IServiceShowcaseSnapshots showcase,
    IServiceProvider services,
    ICurrentAdmin currentAdmin,
    ServiceOrderCodeAllocator codes,
    NodeIdentity node,
    TimeProvider clock) : IServiceOrderTrackingSource
{
    public async Task<ServiceOrderAdminOperation<ServiceOrderAdminDetail>> CreateAsync(
        CreateServiceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var actor = CurrentActor();
        if (actor is null)
        {
            return Invalid<ServiceOrderAdminDetail>(
                "La sesión administrativa no trae nombre, identificador y nodo de cuenta completos.");
        }

        if (request.IdempotencyKey == Guid.Empty)
        {
            return Invalid<ServiceOrderAdminDetail>(
                "La creación necesita una clave de idempotencia válida.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return Invalid<ServiceOrderAdminDetail>("La orden necesita al menos una línea.");
        }

        var receivedAt = request.ReceivedAt ?? clock.GetUtcNow();
        if (request.PromisedAt is not null && request.PromisedAt < receivedAt)
        {
            return Invalid<ServiceOrderAdminDetail>(
                "La fecha prometida no puede ser anterior a la recepción.");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            SELECT pg_advisory_xact_lock(
                hashtextextended({request.IdempotencyKey.ToString()}, 0::bigint));
            """,
            cancellationToken);

        var existingOrderId = await database.ServiceOrders
            .AsNoTracking()
            .Where(order =>
                order.CreateIdempotencyKey == request.IdempotencyKey)
            .Select(order => (Guid?)order.ServiceOrderId)
            .SingleOrDefaultAsync(cancellationToken);

        if (existingOrderId is { } replayedOrderId)
        {
            await transaction.CommitAsync(cancellationToken);
            database.ChangeTracker.Clear();

            return new(
                ServiceOrderAdminOutcome.Ok,
                Value: await LoadAdminDetailAsync(
                    replayedOrderId,
                    cancellationToken),
                IsReplay: true);
        }

        var contact = await ResolveContactAsync(
            request.CustomerId,
            request.CustomerName,
            request.CustomerPhone,
            request.CustomerEmail,
            cancellationToken);

        if (contact.Outcome != ServiceOrderAdminOutcome.Ok)
        {
            return new(contact.Outcome, contact.Error);
        }

        var preparedItems = new List<PreparedItem>(request.Items.Count);

        for (var index = 0; index < request.Items.Count; index++)
        {
            var input = request.Items[index];
            var validation = ValidateLine(input.RequestedDetails, input.Quantity, input.AgreedUnitPrice);

            if (validation is not null)
            {
                return Invalid<ServiceOrderAdminDetail>($"Línea {index + 1}: {validation}");
            }

            if (input.ServiceId <= 0)
            {
                return Invalid<ServiceOrderAdminDetail>(
                    $"Línea {index + 1}: el servicio no es válido.");
            }

            var snapshot = await showcase.GetPublishedSnapshotAsync(
                input.ServiceId,
                cancellationToken);

            if (snapshot is null)
            {
                return new(
                    ServiceOrderAdminOutcome.NotFound,
                    $"Línea {index + 1}: el servicio no existe o ya no está publicado.");
            }

            var snapshotError = ValidateSnapshot(snapshot);
            if (snapshotError is not null)
            {
                return Invalid<ServiceOrderAdminDetail>(
                    $"Línea {index + 1}: {snapshotError}");
            }

            preparedItems.Add(new(
                snapshot,
                input.RequestedDetails.Trim(),
                input.Quantity,
                input.AgreedUnitPrice,
                index));
        }

        var visibleCode = await codes.NextAsync(cancellationToken);
        var order = new ServiceOrder
        {
            VisibleCode = visibleCode,
            Status = ServiceOrderStatuses.Received,
            CreateIdempotencyKey = request.IdempotencyKey,
            CustomerId = contact.Value!.CustomerId,
            CustomerNameSnapshot = contact.Value.Name,
            CustomerPhoneSnapshot = contact.Value.Phone,
            CustomerEmailSnapshot = contact.Value.Email,
            ReceivedNotes = CleanNullable(request.ReceivedNotes),
            ReceivedAt = receivedAt,
            PromisedAt = request.PromisedAt,
            CreatedByAdminName = actor.DisplayName,
            CreatedByAdminUserId = actor.AdminUserId,
            CreatedByAdminUserHomeNode = actor.HomeNode,
            LastStatusChangedAt = receivedAt,
            LastStatusChangedByName = actor.DisplayName,
            LastStatusChangedByAdminUserId = actor.AdminUserId,
            LastStatusChangedByAdminUserHomeNode = actor.HomeNode,
        };

        foreach (var item in preparedItems)
        {
            order.Items.Add(NewItem(order.ServiceOrderId, item));
        }

        order.StatusHistory.Add(new ServiceOrderStatusHistory
        {
            ServiceOrderId = order.ServiceOrderId,
            FromStatus = null,
            ToStatus = ServiceOrderStatuses.Received,
            OccurredAt = receivedAt,
            PerformedByName = actor.DisplayName,
            PerformedByAdminUserId = actor.AdminUserId,
            PerformedByAdminUserHomeNode = actor.HomeNode,
        });

        if (request.AssignToMe)
        {
            order.CurrentAssigneeName = actor.DisplayName;
            order.CurrentAssigneeAdminUserId = actor.AdminUserId;
            order.CurrentAssigneeAdminUserHomeNode = actor.HomeNode;

            order.AssignmentEvents.Add(new ServiceOrderAssignmentEvent
            {
                ServiceOrderId = order.ServiceOrderId,
                Action = "assigned",
                AssigneeName = actor.DisplayName,
                AssigneeAdminUserId = actor.AdminUserId,
                AssigneeAdminUserHomeNode = actor.HomeNode,
                PerformedByName = actor.DisplayName,
                PerformedByAdminUserId = actor.AdminUserId,
                PerformedByAdminUserHomeNode = actor.HomeNode,
                OccurredAt = receivedAt,
            });
        }

        database.ServiceOrders.Add(order);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        database.ChangeTracker.Clear();

        return new(
            ServiceOrderAdminOutcome.Ok,
            Value: await LoadAdminDetailAsync(order.ServiceOrderId, cancellationToken));
    }

    public async Task<ServiceOrderAdminOperation<ServiceOrderAdminDetail>> UpdateAsync(
        Guid serviceOrderId,
        UpdateServiceOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Invalid<ServiceOrderAdminDetail>("La orden necesita al menos una línea.");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var order = await database.ServiceOrders
            .FromSqlInterpolated($"""
                SELECT *
                  FROM service_orders.service_orders
                 WHERE service_order_id = {serviceOrderId}
                 FOR UPDATE
                """)
            .Include(value => value.Items)
            .SingleOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return new(ServiceOrderAdminOutcome.NotFound, "La orden de servicio no existe.");
        }

        if (order.UpdatedAt != request.ExpectedUpdatedAt)
        {
            return Conflict<ServiceOrderAdminDetail>(
                "La orden cambió desde la última lectura. Recárgala antes de guardar.");
        }

        if (!CanEditContent(order.Status))
        {
            return Conflict<ServiceOrderAdminDetail>(
                order.Status == ServiceOrderStatuses.Ready
                    ? "La orden está lista. Reábrela a in_progress antes de editar su contenido."
                    : $"La orden en estado '{order.Status}' ya no admite edición.");
        }

        if (request.PromisedAt is not null && request.PromisedAt < order.ReceivedAt)
        {
            return Invalid<ServiceOrderAdminDetail>(
                "La fecha prometida no puede ser anterior a la recepción.");
        }

        var contact = await ResolveContactAsync(
            request.CustomerId,
            request.CustomerName,
            request.CustomerPhone,
            request.CustomerEmail,
            cancellationToken);

        if (contact.Outcome != ServiceOrderAdminOutcome.Ok)
        {
            return new(contact.Outcome, contact.Error);
        }

        var existing = order.Items.ToDictionary(item => item.ServiceOrderItemId);
        var originalSortOrders = existing.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.SortOrder);
        var retained = new HashSet<Guid>();

        for (var index = 0; index < request.Items.Count; index++)
        {
            var input = request.Items[index];
            var validation = ValidateLine(input.RequestedDetails, input.Quantity, input.AgreedUnitPrice);

            if (validation is not null)
            {
                return Invalid<ServiceOrderAdminDetail>($"Línea {index + 1}: {validation}");
            }

            if (input.ServiceOrderItemId is { } existingId)
            {
                if (input.ServiceId is not null)
                {
                    return Invalid<ServiceOrderAdminDetail>(
                        $"Línea {index + 1}: una línea existente no cambia su servicio fotografiado.");
                }

                if (!retained.Add(existingId))
                {
                    return Invalid<ServiceOrderAdminDetail>(
                        $"Línea {index + 1}: la misma línea aparece más de una vez.");
                }

                if (!existing.TryGetValue(existingId, out var entity))
                {
                    return Conflict<ServiceOrderAdminDetail>(
                        $"Línea {index + 1}: esa línea ya no pertenece a la orden.");
                }

                entity.RequestedDetails = input.RequestedDetails.Trim();
                entity.Quantity = input.Quantity;
                entity.AgreedUnitPrice = input.AgreedUnitPrice;
                entity.SortOrder = index;
                continue;
            }

            if (input.ServiceId is not int serviceId || serviceId <= 0)
            {
                return Invalid<ServiceOrderAdminDetail>(
                    $"Línea {index + 1}: una línea nueva necesita un servicio válido.");
            }

            var snapshot = await showcase.GetPublishedSnapshotAsync(
                serviceId,
                cancellationToken);

            if (snapshot is null)
            {
                return new(
                    ServiceOrderAdminOutcome.NotFound,
                    $"Línea {index + 1}: el servicio no existe o ya no está publicado.");
            }

            var snapshotError = ValidateSnapshot(snapshot);
            if (snapshotError is not null)
            {
                return Invalid<ServiceOrderAdminDetail>(
                    $"Línea {index + 1}: {snapshotError}");
            }

            order.Items.Add(NewItem(
                order.ServiceOrderId,
                new PreparedItem(
                    snapshot,
                    input.RequestedDetails.Trim(),
                    input.Quantity,
                    input.AgreedUnitPrice,
                    index)));
        }

        if (retained.Count != existing.Count)
        {
            return Invalid<ServiceOrderAdminDetail>(
                "Una línea ya confirmada no se elimina. Puedes editarla, reordenarla o agregar líneas nuevas.");
        }

        var persistedOrderChanged = existing.Any(pair =>
            pair.Value.SortOrder != originalSortOrders[pair.Key]);

        if (persistedOrderChanged
            && !await StagePersistedSortOrdersAsync(
                serviceOrderId,
                existing,
                originalSortOrders,
                request.Items.Count,
                cancellationToken))
        {
            return Conflict<ServiceOrderAdminDetail>(
                "Las líneas cambiaron mientras se reordenaba la orden. Recárgala antes de guardar.");
        }

        order.CustomerId = contact.Value!.CustomerId;
        order.CustomerNameSnapshot = contact.Value.Name;
        order.CustomerPhoneSnapshot = contact.Value.Phone;
        order.CustomerEmailSnapshot = contact.Value.Email;
        order.ReceivedNotes = CleanNullable(request.ReceivedNotes);
        order.PromisedAt = request.PromisedAt;

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        database.ChangeTracker.Clear();

        return new(
            ServiceOrderAdminOutcome.Ok,
            Value: await LoadAdminDetailAsync(serviceOrderId, cancellationToken));
    }

    public async Task<ServiceOrderAdminOperation<ServiceOrderAdminDetail>> TakeAsync(
        Guid serviceOrderId,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken)
    {
        var actor = CurrentActor();
        if (actor is null)
        {
            return Invalid<ServiceOrderAdminDetail>(
                "La sesión administrativa no trae identidad completa.");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var order = await LockOrderAsync(serviceOrderId, cancellationToken);
        if (order is null)
        {
            return new(ServiceOrderAdminOutcome.NotFound, "La orden de servicio no existe.");
        }

        if (order.UpdatedAt != expectedUpdatedAt)
        {
            return Conflict<ServiceOrderAdminDetail>(
                "La asignación cambió desde la última lectura. Recarga la orden.");
        }

        if (!CanChangeAssignment(order.Status))
        {
            return Conflict<ServiceOrderAdminDetail>(
                "Una orden terminal no admite cambios de responsable.");
        }

        if (SameStaff(order, actor))
        {
            await transaction.CommitAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new(
                ServiceOrderAdminOutcome.Ok,
                Value: await LoadAdminDetailAsync(serviceOrderId, cancellationToken));
        }

        var now = clock.GetUtcNow();

        order.CurrentAssigneeName = actor.DisplayName;
        order.CurrentAssigneeAdminUserId = actor.AdminUserId;
        order.CurrentAssigneeAdminUserHomeNode = actor.HomeNode;

        database.AssignmentEvents.Add(new ServiceOrderAssignmentEvent
        {
            ServiceOrderId = order.ServiceOrderId,
            Action = "assigned",
            AssigneeName = actor.DisplayName,
            AssigneeAdminUserId = actor.AdminUserId,
            AssigneeAdminUserHomeNode = actor.HomeNode,
            PerformedByName = actor.DisplayName,
            PerformedByAdminUserId = actor.AdminUserId,
            PerformedByAdminUserHomeNode = actor.HomeNode,
            OccurredAt = now,
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();

        return new(
            ServiceOrderAdminOutcome.Ok,
            Value: await LoadAdminDetailAsync(serviceOrderId, cancellationToken));
    }

    public async Task<ServiceOrderAdminOperation<ServiceOrderAdminDetail>> UnassignAsync(
        Guid serviceOrderId,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken)
    {
        var actor = CurrentActor();
        if (actor is null)
        {
            return Invalid<ServiceOrderAdminDetail>(
                "La sesión administrativa no trae identidad completa.");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var order = await LockOrderAsync(serviceOrderId, cancellationToken);
        if (order is null)
        {
            return new(ServiceOrderAdminOutcome.NotFound, "La orden de servicio no existe.");
        }

        if (order.UpdatedAt != expectedUpdatedAt)
        {
            return Conflict<ServiceOrderAdminDetail>(
                "La asignación cambió desde la última lectura. Recarga la orden.");
        }

        if (!CanChangeAssignment(order.Status))
        {
            return Conflict<ServiceOrderAdminDetail>(
                "Una orden terminal no admite cambios de responsable.");
        }

        if (order.CurrentAssigneeAdminUserId is null
            || order.CurrentAssigneeName is null
            || order.CurrentAssigneeAdminUserHomeNode is null)
        {
            return Conflict<ServiceOrderAdminDetail>("La orden ya está sin responsable.");
        }

        if (!SameStaff(order, actor))
        {
            return Conflict<ServiceOrderAdminDetail>(
                "Solo puedes liberar una orden que esté asignada a tu propia cuenta.");
        }

        var now = clock.GetUtcNow();
        var previousName = order.CurrentAssigneeName;
        var previousId = order.CurrentAssigneeAdminUserId.Value;
        var previousHomeNode = order.CurrentAssigneeAdminUserHomeNode;

        order.CurrentAssigneeName = null;
        order.CurrentAssigneeAdminUserId = null;
        order.CurrentAssigneeAdminUserHomeNode = null;

        database.AssignmentEvents.Add(new ServiceOrderAssignmentEvent
        {
            ServiceOrderId = order.ServiceOrderId,
            Action = "unassigned",
            AssigneeName = previousName,
            AssigneeAdminUserId = previousId,
            AssigneeAdminUserHomeNode = previousHomeNode,
            PerformedByName = actor.DisplayName,
            PerformedByAdminUserId = actor.AdminUserId,
            PerformedByAdminUserHomeNode = actor.HomeNode,
            OccurredAt = now,
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();

        return new(
            ServiceOrderAdminOutcome.Ok,
            Value: await LoadAdminDetailAsync(serviceOrderId, cancellationToken));
    }

    public async Task<ServiceOrderAdminDetail?> GetAdminAsync(
        Guid serviceOrderId,
        CancellationToken cancellationToken)
        => await LoadAdminDetailAsync(serviceOrderId, cancellationToken);

    public async Task<PagedResult<ServiceOrderAdminSummary>> ListAdminAsync(
        string? status,
        int? assigneeAdminUserId,
        string? text,
        DateTimeOffset? receivedFrom,
        DateTimeOffset? receivedTo,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        IQueryable<ServiceOrder> query = database.ServiceOrders
            .AsNoTracking()
            .Include(order => order.Items);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(order => order.Status == status);
        }

        if (assigneeAdminUserId is not null)
        {
            query = query.Where(order =>
                order.CurrentAssigneeAdminUserId == assigneeAdminUserId);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = $"%{text.Trim()}%";
            query = query.Where(order =>
                EF.Functions.ILike(order.VisibleCode, pattern)
                || EF.Functions.ILike(order.CustomerNameSnapshot, pattern));
        }

        if (receivedFrom is not null)
        {
            query = query.Where(order => order.ReceivedAt >= receivedFrom);
        }

        if (receivedTo is not null)
        {
            query = query.Where(order => order.ReceivedAt <= receivedTo);
        }

        var total = await query.LongCountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(order => order.ReceivedAt)
            .ThenByDescending(order => order.VisibleCode)
            .Skip(page.Skip)
            .Take(page.Size)
            .ToListAsync(cancellationToken);

        return PagedResult<ServiceOrderAdminSummary>.From(
            page,
            rows.Select(MapSummary).ToList(),
            total);
    }

    public async Task<ServiceOrderTrackingSnapshot?> GetAsync(
        Guid serviceOrderId,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(serviceOrderId, cancellationToken);
        return order is null ? null : MapTracking(order);
    }

    public async Task<PagedResult<ServiceOrderTrackingSummary>> ListAsync(
        ServiceOrderQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<ServiceOrder> source = database.ServiceOrders.AsNoTracking();

        source = query.Scope switch
        {
            ServiceOrderScope.Open => source.Where(order =>
                order.Status != ServiceOrderStatuses.Completed
                && order.Status != ServiceOrderStatuses.Cancelled),

            ServiceOrderScope.Closed => source.Where(order =>
                order.Status == ServiceOrderStatuses.Completed
                || order.Status == ServiceOrderStatuses.Cancelled),

            _ => source
        };

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            source = source.Where(order => order.Status == query.Status);
        }

        var total = await source.LongCountAsync(cancellationToken);

        source = (query.Sort, query.Direction) switch
        {
            (ServiceOrderSort.ReceivedAt, ServiceOrderSortDirection.Ascending) =>
                source.OrderBy(order => order.ReceivedAt),

            (ServiceOrderSort.ReceivedAt, _) =>
                source.OrderByDescending(order => order.ReceivedAt),

            (ServiceOrderSort.PromisedAt, ServiceOrderSortDirection.Ascending) =>
                source.OrderBy(order => order.PromisedAt),

            (ServiceOrderSort.PromisedAt, _) =>
                source.OrderByDescending(order => order.PromisedAt),

            (ServiceOrderSort.UpdatedAt, ServiceOrderSortDirection.Ascending) =>
                source.OrderBy(order => order.UpdatedAt),

            (ServiceOrderSort.UpdatedAt, _) =>
                source.OrderByDescending(order => order.UpdatedAt),

            (ServiceOrderSort.VisibleCode, ServiceOrderSortDirection.Ascending) =>
                source.OrderBy(order => order.VisibleCode),

            _ => source.OrderByDescending(order => order.VisibleCode)
        };

        var rows = await source
            .Skip(query.Page.Skip)
            .Take(query.Page.Size)
            .ToListAsync(cancellationToken);

        return PagedResult<ServiceOrderTrackingSummary>.From(
            query.Page,
            rows.Select(order => new ServiceOrderTrackingSummary(
                order.ServiceOrderId,
                order.VisibleCode,
                order.CustomerNameSnapshot,
                order.Status,
                order.ReceivedAt,
                order.PromisedAt,
                Staff(
                    order.CurrentAssigneeName,
                    order.CurrentAssigneeAdminUserId,
                    order.CurrentAssigneeAdminUserHomeNode),
                order.UpdatedAt))
            .ToList(),
            total);
    }

    private async Task<ServiceOrder?> LockOrderAsync(
        Guid serviceOrderId,
        CancellationToken cancellationToken)
        => await database.ServiceOrders
            .FromSqlInterpolated($"""
                SELECT *
                  FROM service_orders.service_orders
                 WHERE service_order_id = {serviceOrderId}
                 FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<ServiceOrder?> LoadOrderAsync(
        Guid serviceOrderId,
        CancellationToken cancellationToken)
        => await database.ServiceOrders
            .AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.StatusHistory)
            .Include(order => order.AssignmentEvents)
            .SingleOrDefaultAsync(
                order => order.ServiceOrderId == serviceOrderId,
                cancellationToken);

    private async Task<ServiceOrderAdminDetail?> LoadAdminDetailAsync(
        Guid serviceOrderId,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(serviceOrderId, cancellationToken);
        return order is null ? null : MapDetail(order);
    }

    private async Task<ServiceOrderAdminOperation<ContactSnapshot>> ResolveContactAsync(
        Guid? customerId,
        string? manualName,
        string? manualPhone,
        string? manualEmail,
        CancellationToken cancellationToken)
    {
        if (customerId is { } selectedCustomer)
        {
            var reader = services.GetService<ICustomerSnapshotReader>();

            if (reader is null)
            {
                return Conflict<ContactSnapshot>(
                    "CRM no está activo. Usa el contacto manual para recibir la orden.");
            }

            var snapshot = await reader.GetForOrderAsync(
                selectedCustomer,
                cancellationToken);

            if (snapshot is null)
            {
                return new(
                    ServiceOrderAdminOutcome.NotFound,
                    "El cliente seleccionado no existe o ya no está disponible.");
            }

            return new(
                ServiceOrderAdminOutcome.Ok,
                Value: new ContactSnapshot(
                    snapshot.CustomerId,
                    snapshot.FullName.Trim(),
                    CleanNullable(snapshot.Phone),
                    CleanNullable(snapshot.Email)));
        }

        var name = manualName?.Trim();
        var phone = CleanNullable(manualPhone);
        var email = CleanNullable(manualEmail);

        if (string.IsNullOrWhiteSpace(name))
        {
            return Invalid<ContactSnapshot>("El contacto necesita un nombre.");
        }

        if (phone is null && email is null)
        {
            return Invalid<ContactSnapshot>(
                "El contacto necesita al menos teléfono o correo.");
        }

        return new(
            ServiceOrderAdminOutcome.Ok,
            Value: new ContactSnapshot(null, name, phone, email));
    }

    private ServiceOrderAdminDetail MapDetail(ServiceOrder order)
    {
        var items = order.Items
            .OrderBy(item => item.SortOrder)
            .Select(item => new ServiceOrderAdminItem(
                item.ServiceOrderItemId,
                item.ServiceSourceId,
                item.ServiceSourceNode,
                item.ServiceNameSnapshot,
                item.ServiceSlugSnapshot,
                item.ServiceShortDescriptionSnapshot,
                item.ServiceDescriptionSnapshot,
                item.ShowcasePriceSnapshot,
                item.SaleUnitSnapshot,
                item.MediaAssetIdSnapshot,
                item.ImageUrlSnapshot,
                item.ImageAltTextSnapshot,
                item.RequestedDetails,
                item.Quantity,
                item.AgreedUnitPrice,
                item.SortOrder))
            .ToList();

        var history = order.StatusHistory
            .OrderBy(entry => entry.OccurredAt)
            .ThenBy(entry => entry.CreatedAt)
            .Select(entry => new ServiceOrderStatusHistorySnapshot(
                entry.StatusHistoryId,
                entry.FromStatus,
                entry.ToStatus,
                entry.OccurredAt,
                Staff(
                    entry.PerformedByName,
                    entry.PerformedByAdminUserId,
                    entry.PerformedByAdminUserHomeNode),
                entry.OriginNode))
            .ToList();

        var assignments = order.AssignmentEvents
            .OrderBy(entry => entry.OccurredAt)
            .ThenBy(entry => entry.CreatedAt)
            .Select(entry => new ServiceOrderAssignmentSnapshot(
                entry.AssignmentEventId,
                entry.Action,
                new StaffSnapshot(
                    entry.AssigneeName,
                    entry.AssigneeAdminUserId,
                    entry.AssigneeAdminUserHomeNode),
                new StaffSnapshot(
                    entry.PerformedByName,
                    entry.PerformedByAdminUserId,
                    entry.PerformedByAdminUserHomeNode),
                entry.OccurredAt,
                entry.OriginNode))
            .ToList();

        var pending = order.Items.Any(item => item.AgreedUnitPrice is null);
        var total = pending
            ? (decimal?)null
            : order.Items.Sum(item => item.Quantity * item.AgreedUnitPrice!.Value);

        return new ServiceOrderAdminDetail(
            order.ServiceOrderId,
            order.VisibleCode,
            order.Status,
            order.CustomerId,
            order.CustomerNameSnapshot,
            order.CustomerPhoneSnapshot,
            order.CustomerEmailSnapshot,
            order.ReceivedNotes,
            order.ReceivedAt,
            order.PromisedAt,
            new StaffSnapshot(
                order.CreatedByAdminName,
                order.CreatedByAdminUserId,
                order.CreatedByAdminUserHomeNode),
            Staff(
                order.CurrentAssigneeName,
                order.CurrentAssigneeAdminUserId,
                order.CurrentAssigneeAdminUserHomeNode),
            items,
            history,
            assignments,
            pending,
            total,
            order.UpdatedAt);
    }

    private ServiceOrderTrackingSnapshot MapTracking(ServiceOrder order)
        => new(
            order.ServiceOrderId,
            order.VisibleCode,
            order.CustomerNameSnapshot,
            order.Status,
            order.ReceivedAt,
            order.PromisedAt,
            order.Items
                .OrderBy(item => item.SortOrder)
                .Select(item => new ServiceOrderWorkItemSnapshot(
                    item.ServiceOrderItemId,
                    item.ServiceNameSnapshot,
                    item.SaleUnitSnapshot,
                    item.Quantity,
                    item.RequestedDetails))
                .ToList(),
            Staff(
                order.CurrentAssigneeName,
                order.CurrentAssigneeAdminUserId,
                order.CurrentAssigneeAdminUserHomeNode),
            order.StatusHistory
                .OrderBy(entry => entry.OccurredAt)
                .ThenBy(entry => entry.CreatedAt)
                .Select(entry => new ServiceOrderStatusHistorySnapshot(
                    entry.StatusHistoryId,
                    entry.FromStatus,
                    entry.ToStatus,
                    entry.OccurredAt,
                    Staff(
                        entry.PerformedByName,
                        entry.PerformedByAdminUserId,
                        entry.PerformedByAdminUserHomeNode),
                    entry.OriginNode))
                .ToList(),
            order.UpdatedAt);

    private static ServiceOrderAdminSummary MapSummary(ServiceOrder order)
    {
        var pending = order.Items.Count > 0
            && order.Items.Any(item => item.AgreedUnitPrice is null);

        decimal? total = pending || order.Items.Count == 0
            ? null
            : order.Items.Sum(item => item.Quantity * item.AgreedUnitPrice!.Value);

        return new(
            order.ServiceOrderId,
            order.VisibleCode,
            order.CustomerNameSnapshot,
            order.Status,
            order.ReceivedAt,
            order.PromisedAt,
            Staff(
                order.CurrentAssigneeName,
                order.CurrentAssigneeAdminUserId,
                order.CurrentAssigneeAdminUserHomeNode),
            pending,
            total,
            order.UpdatedAt);
    }

    private ServiceOrderItem NewItem(Guid orderId, PreparedItem item)
        => new()
        {
            ServiceOrderId = orderId,
            ServiceSourceId = item.Snapshot.ServiceId,
            ServiceSourceNode = node.Code,
            ServiceNameSnapshot = item.Snapshot.Name,
            ServiceSlugSnapshot = item.Snapshot.Slug,
            ServiceShortDescriptionSnapshot = item.Snapshot.ShortDescription,
            ServiceDescriptionSnapshot = item.Snapshot.Description,
            ShowcasePriceSnapshot = item.Snapshot.Price,
            SaleUnitSnapshot = item.Snapshot.SaleUnit,
            MediaAssetIdSnapshot = item.Snapshot.MediaAssetId,
            ImageUrlSnapshot = item.Snapshot.ImageUrl,
            ImageAltTextSnapshot = item.Snapshot.ImageAltText,
            RequestedDetails = item.RequestedDetails,
            Quantity = item.Quantity,
            AgreedUnitPrice = item.AgreedUnitPrice,
            SortOrder = item.SortOrder,
        };

    private async Task<bool> StagePersistedSortOrdersAsync(
        Guid serviceOrderId,
        IReadOnlyDictionary<Guid, ServiceOrderItem> existing,
        IReadOnlyDictionary<Guid, int> originalSortOrders,
        int finalItemCount,
        CancellationToken cancellationToken)
    {
        var reserved = originalSortOrders.Values.ToHashSet();

        for (var index = 0; index < finalItemCount; index++)
        {
            reserved.Add(index);
        }

        var candidate = 0;

        foreach (var item in existing.Values.OrderBy(
                     value => originalSortOrders[value.ServiceOrderItemId]))
        {
            while (reserved.Contains(candidate))
            {
                if (candidate == int.MaxValue)
                {
                    throw new InvalidOperationException(
                        "No hay posiciones temporales disponibles para reordenar las líneas.");
                }

                candidate++;
            }

            var temporarySortOrder = candidate;
            reserved.Add(temporarySortOrder);

            if (candidate < int.MaxValue)
            {
                candidate++;
            }

            var affected = await database.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE service_orders.service_order_items
                   SET sort_order = {temporarySortOrder}
                 WHERE service_order_id = {serviceOrderId}
                   AND service_order_item_id = {item.ServiceOrderItemId};
                """,
                cancellationToken);

            if (affected != 1)
            {
                return false;
            }

            database.Entry(item)
                .Property(value => value.SortOrder)
                .OriginalValue = temporarySortOrder;
        }

        return true;
    }

    private Actor? CurrentActor()
    {
        var name = currentAdmin.DisplayName?.Trim();
        var homeNode = currentAdmin.HomeNode?.Trim();

        return currentAdmin.AdminUserId is > 0 and var id
            && !string.IsNullOrWhiteSpace(name)
            && !string.IsNullOrWhiteSpace(homeNode)
                ? new Actor(name, id, homeNode)
                : null;
    }

    private static bool SameStaff(ServiceOrder order, Actor actor)
        => order.CurrentAssigneeAdminUserId == actor.AdminUserId
           && string.Equals(
               order.CurrentAssigneeAdminUserHomeNode,
               actor.HomeNode,
               StringComparison.Ordinal)
           && string.Equals(
               order.CurrentAssigneeName,
               actor.DisplayName,
               StringComparison.Ordinal);

    private static bool CanEditContent(string status)
        => status == ServiceOrderStatuses.Received
           || status == ServiceOrderStatuses.InProgress;

    private static bool CanChangeAssignment(string status)
        => status == ServiceOrderStatuses.Received
           || status == ServiceOrderStatuses.InProgress
           || status == ServiceOrderStatuses.Ready;

    private static string? ValidateLine(
        string? requestedDetails,
        decimal quantity,
        decimal? agreedUnitPrice)
    {
        if (string.IsNullOrWhiteSpace(requestedDetails))
        {
            return "describe las características del encargo.";
        }

        if (quantity <= 0)
        {
            return "la cantidad debe ser mayor que cero.";
        }

        if (agreedUnitPrice < 0)
        {
            return "el precio acordado no puede ser negativo.";
        }

        return null;
    }

    private static string? ValidateSnapshot(ServiceSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Name))
        {
            return "el servicio publicado no tiene nombre utilizable.";
        }

        if (string.IsNullOrWhiteSpace(snapshot.Slug))
        {
            return "el servicio publicado no tiene slug utilizable.";
        }

        if (snapshot.ShortDescription is null && snapshot.Description is null)
        {
            return "el servicio publicado no tiene descripción fotografiable.";
        }

        if (snapshot.MediaAssetId is not null
            && string.IsNullOrWhiteSpace(snapshot.ImageAltText))
        {
            return "el medio publicado no tiene texto alternativo fotografiable.";
        }

        return null;
    }

    private static string? CleanNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StaffSnapshot? Staff(
        string? name,
        int? adminUserId,
        string? homeNode)
        => name is not null
           && adminUserId is not null
           && homeNode is not null
            ? new StaffSnapshot(name, adminUserId.Value, homeNode)
            : null;

    private static ServiceOrderAdminOperation<T> Invalid<T>(string error)
        => new(ServiceOrderAdminOutcome.Invalid, error);

    private static ServiceOrderAdminOperation<T> Conflict<T>(string error)
        => new(ServiceOrderAdminOutcome.Conflict, error);

    private sealed record Actor(
        string DisplayName,
        int AdminUserId,
        string HomeNode);

    private sealed record ContactSnapshot(
        Guid? CustomerId,
        string Name,
        string? Phone,
        string? Email);

    private sealed record PreparedItem(
        ServiceSnapshot Snapshot,
        string RequestedDetails,
        decimal Quantity,
        decimal? AgreedUnitPrice,
        int SortOrder);
}
