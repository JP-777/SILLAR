using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Crm.Dtos;

namespace Sillar.Modules.Crm.Tests;

public sealed class CustomerProfileContractTests
{
    [Theory]
    [InlineData(typeof(CustomerProfileResponse))]
    [InlineData(typeof(CustomerAddressResponse))]
    [InlineData(typeof(CustomerOrderSnapshot))]
    [InlineData(typeof(CustomerOrderAddressSnapshot))]
    public void Contratos_publicos_no_exponen_campos_internos(
        Type type)
    {
        var names = type
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(
            names,
            name => name.Contains(
                "InternalNotes",
                StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            names,
            name => name.Contains(
                "Password",
                StringComparison.OrdinalIgnoreCase));
    }
}
