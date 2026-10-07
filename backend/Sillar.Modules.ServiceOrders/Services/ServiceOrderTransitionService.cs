using System.Data;
using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Data;
using Sillar.Modules.ServiceOrders.Domain;

namespace Sillar.Modules.ServiceOrders.Services;

/// <summary>
/// Operación autoritativa de M05b para cambiar estado y escribir su historia durable.
/// </summary>
public sealed class ServiceOrderTransitionService(
    ServiceOrdersDbContext database,
    ICurrentAdmin currentAdmin,
    TimeProvider clock) : IServiceOrderTransitions
{
    public async Task<ServiceOrderOperation<ServiceOrderTransitionResult>> TransitionAsync(
        Guid serviceOrderId,
        string expectedStatus,
        string targetStatus,
        CancellationToken cancellationToken)
    {
        var actor = CurrentActor();
        if (actor is null)
        {
            return new(
                ServiceOrderOutcome.Invalid,
                "La transición requiere una sesión administrativa con identidad completa.");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        // El bloqueo convierte leer/comparar/escribir en una sola decisión serializada.
        // Un segundo consumidor espera, relee el estado ya confirmado y recibe Conflict.
        var order = await database.ServiceOrders
            .FromSqlInterpolated($"""
                SELECT *
                  FROM service_orders.service_orders
                 WHERE service_order_id = {serviceOrderId}
                 FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return new(ServiceOrderOutcome.NotFound, "La orden de servicio no existe.");
        }

        if (!string.Equals(order.Status, expectedStatus, StringComparison.Ordinal))
        {
            return new(
                ServiceOrderOutcome.Conflict,
                "La orden cambió desde la última lectura. Recarga su estado antes de volver a intentarlo.");
        }

        if (targetStatus == ServiceOrderStatuses.Cancelled
            && !currentAdmin.IsInRole(AdminRole.Admin))
        {
            return new(
                ServiceOrderOutcome.Invalid,
                "Cancelar una orden exige rol admin o superior.");
        }

        var legal = ServiceOrderStatuses.LegalTransitions.Any(candidate =>
            candidate.FromStatus == expectedStatus && candidate.ToStatus == targetStatus);
        if (!legal)
        {
            return new(
                ServiceOrderOutcome.Invalid,
                $"La transición de '{expectedStatus}' a '{targetStatus}' no está permitida.");
        }

        var occurredAt = clock.GetUtcNow();
        var history = new ServiceOrderStatusHistory
        {
            ServiceOrderId = order.ServiceOrderId,
            FromStatus = order.Status,
            ToStatus = targetStatus,
            OccurredAt = occurredAt,
            PerformedByName = actor.DisplayName,
            PerformedByAdminUserId = actor.AdminUserId,
            PerformedByAdminUserHomeNode = actor.HomeNode,
        };

        order.Status = targetStatus;
        order.LastStatusChangedAt = occurredAt;
        order.LastStatusChangedByName = actor.DisplayName;
        order.LastStatusChangedByAdminUserId = actor.AdminUserId;
        order.LastStatusChangedByAdminUserHomeNode = actor.HomeNode;
        database.StatusHistory.Add(history);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var snapshot = new ServiceOrderStatusHistorySnapshot(
            history.StatusHistoryId,
            history.FromStatus,
            history.ToStatus,
            history.OccurredAt,
            new StaffSnapshot(actor.DisplayName, actor.AdminUserId, actor.HomeNode),
            history.OriginNode);

        return new(
            ServiceOrderOutcome.Ok,
            Value: new ServiceOrderTransitionResult(order.ServiceOrderId, order.Status, snapshot));
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

    private sealed record Actor(string DisplayName, int AdminUserId, string HomeNode);
}
