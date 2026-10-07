using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Shared.Paging;

namespace Sillar.Modules.ServiceOrders.Tests;

public sealed class ContractsAndModuleTests
{
    [Fact]
    public void Module_declares_only_ratified_hard_and_soft_dependencies()
    {
        var module = new ServiceOrdersModule();
        Assert.Equal(["core", "services"], module.HardDependencies);
        Assert.Equal(["crm"], module.SoftDependencies);
        Assert.Equal("service_orders", module.Code);
    }

    [Fact]
    public void Public_contract_owns_exactly_the_ratified_state_vocabulary()
        => Assert.Equal(
            ["received", "in_progress", "ready", "completed", "cancelled"],
            ServiceOrderStatuses.All.Select(state => state.Code));

    [Fact]
    public void Public_contract_exposes_exactly_the_ratified_legal_transitions()
        => Assert.Equal(
            [
                "received->in_progress", "received->cancelled",
                "in_progress->ready", "in_progress->cancelled",
                "ready->completed", "ready->in_progress", "ready->cancelled",
            ],
            ServiceOrderStatuses.LegalTransitions.Select(value => $"{value.FromStatus}->{value.ToStatus}"));

    [Fact]
    public void Public_contract_resolves_C3_through_C7_without_EF_or_error_text_protocol()
    {
        var list = typeof(IServiceOrderTrackingSource).GetMethod(nameof(IServiceOrderTrackingSource.ListAsync))!;
        Assert.Equal(typeof(ServiceOrderQuery), list.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(Task<PagedResult<ServiceOrderTrackingSummary>>), list.ReturnType);

        Assert.NotNull(typeof(ServiceOrderTrackingSummary).GetProperty(nameof(ServiceOrderTrackingSummary.UpdatedAt)));
        Assert.NotNull(typeof(ServiceOrderTrackingSnapshot).GetProperty(nameof(ServiceOrderTrackingSnapshot.UpdatedAt)));

        var transition = typeof(IServiceOrderTransitions).GetMethod(nameof(IServiceOrderTransitions.TransitionAsync))!;
        Assert.Equal(
            ["serviceOrderId", "expectedStatus", "targetStatus", "cancellationToken"],
            transition.GetParameters().Select(parameter => parameter.Name));
        Assert.Equal(
            typeof(Task<ServiceOrderOperation<ServiceOrderTransitionResult>>),
            transition.ReturnType);
        Assert.Equal(
            ["Ok", "NotFound", "Invalid", "Conflict"],
            Enum.GetNames<ServiceOrderOutcome>());
    }
}
