namespace Sillar.Modules.Tracking.Tests;

public sealed class ModuleContractTests
{
    [Fact]
    public void Module_declares_only_ratified_dependencies()
    {
        var module = new TrackingModule();

        Assert.Equal("tracking", module.Code);
        Assert.Equal(["core", "service_orders"], module.HardDependencies);
        Assert.Empty(module.SoftDependencies);
    }

    [Fact]
    public void Tracking_does_not_reference_the_concrete_ServiceOrders_assembly()
    {
        var references = typeof(TrackingModule)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("Sillar.Modules.ServiceOrders", references);
    }
}
