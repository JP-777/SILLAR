using Sillar.Modules.Crm.Dtos;

namespace Sillar.Modules.Crm.Tests;

public sealed class ContactMessageContractTests
{
    [Theory]
    [InlineData(typeof(PublicContactAcceptedResponse))]
    [InlineData(typeof(AdminContactMessageListItemResponse))]
    [InlineData(typeof(AdminContactMessageDetailResponse))]
    public void Respuestas_de_contacto_no_exponen_secretos(
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

        Assert.DoesNotContain(
            names,
            name => name.Contains(
                "InternalNotes",
                StringComparison.OrdinalIgnoreCase));
    }
}
