using Sillar.Modules.ServiceOrders.Domain;
using Sillar.Shared.Replication;

namespace Sillar.Modules.ServiceOrders.Tests;

public sealed class ReplicationClassificationTests
{
    [Fact]
    public void Four_replicated_entities_start_with_application_UUID_v7_and_series_is_local()
    {
        var order = new ServiceOrder
        {
            VisibleCode = "S-2026-0001",
            CustomerNameSnapshot = "Cliente",
            CreatedByAdminName = "Ana",
            CreatedByAdminUserHomeNode = "principal",
        };
        var item = new ServiceOrderItem
        {
            ServiceSourceNode = "principal",
            ServiceNameSnapshot = "Impresión",
            ServiceSlugSnapshot = "impresion",
            RequestedDetails = "A4",
        };
        var assignment = new ServiceOrderAssignmentEvent
        {
            Action = "assigned",
            AssigneeName = "Ana",
            AssigneeAdminUserHomeNode = "principal",
            PerformedByName = "Ana",
            PerformedByAdminUserHomeNode = "principal",
        };
        var history = new ServiceOrderStatusHistory { ToStatus = "received" };

        Assert.All(
            new[] { order.ServiceOrderId, item.ServiceOrderItemId, assignment.AssignmentEventId, history.StatusHistoryId },
            id => Assert.Equal('7', id.ToString("D")[14]));
        Assert.IsAssignableFrom<IReplicatedEntity>(order);
        Assert.IsAssignableFrom<IReplicatedEntity>(item);
        Assert.IsAssignableFrom<IReplicatedEntity>(assignment);
        Assert.IsAssignableFrom<IReplicatedEntity>(history);
        Assert.False(typeof(IReplicatedEntity).IsAssignableFrom(typeof(ServiceOrderSeries)));
    }
}
