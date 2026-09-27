using Sillar.Modules.Crm.Dtos;

namespace Sillar.Modules.Crm.Tests;

public sealed class CustomerPasswordExposureTests
{
    [Fact]
    public void Criterio13_los_contratos_de_respuesta_no_exponen_password_ni_hash()
    {
        var assembly = typeof(CustomerRegistrationResponse).Assembly;

        var responseTypes = assembly
            .GetTypes()
            .Where(type =>
                type.IsPublic
                && type.Namespace == "Sillar.Modules.Crm.Dtos"
                && type.Name.EndsWith(
                    "Response",
                    StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(responseTypes);

        foreach (var type in responseTypes)
        {
            var names = type
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();

            Assert.DoesNotContain(
                names,
                name =>
                    name.Contains(
                        "Password",
                        StringComparison.OrdinalIgnoreCase)
                    || name.Contains(
                        "Hash",
                        StringComparison.OrdinalIgnoreCase));
        }
    }
}
