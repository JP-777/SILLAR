using Sillar.Modules.Crm.Dtos;

namespace Sillar.Modules.Crm.Tests;

public sealed class CustomerAdminContractTests
{
    [Theory]
    [InlineData(typeof(AdminCustomerListItemResponse))]
    [InlineData(typeof(AdminCustomerDetailResponse))]
    [InlineData(typeof(AdminCustomerAccessResponse))]
    [InlineData(typeof(AdminCustomerAddressResponse))]
    [InlineData(typeof(AdminCustomerInvitationResponse))]
    public void Respuestas_admin_no_exponen_secretos(
        Type type)
    {
        var names = type
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(
            names,
            name => name.Contains(
                "Password",
                StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            names,
            name => name.Contains(
                "Token",
                StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            names,
            name => name.Contains(
                "Hash",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Solo_la_ficha_admin_puede_exponer_notas_internas()
    {
        Assert.Contains(
            typeof(AdminCustomerDetailResponse).GetProperties(),
            property => property.Name == "InternalNotes");

        Assert.DoesNotContain(
            typeof(AdminCustomerListItemResponse).GetProperties(),
            property => property.Name == "InternalNotes");
    }
}
