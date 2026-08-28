using Sillar.Modules.Crm.Authentication;

namespace Sillar.Modules.Crm.Tests;

public sealed class CustomerRegistrationPolicyTests
{
    [Fact]
    public void Password_menor_de_12_se_rechaza()
    {
        var result = CustomerPasswordPolicy.Check(
            "corta",
            "cliente@ejemplo.pe",
            "Cliente Prueba");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Password_comun_se_rechaza()
    {
        var result = CustomerPasswordPolicy.Check(
            "contrasena123",
            "cliente@ejemplo.pe",
            "Cliente Prueba");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Password_con_identidad_se_rechaza()
    {
        var result = CustomerPasswordPolicy.Check(
            "cliente-super-segura",
            "cliente@ejemplo.pe",
            "Persona Prueba");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Password_larga_sin_identidad_se_acepta()
    {
        var result = CustomerPasswordPolicy.Check(
            "farol-montana-rio-829",
            "cliente@ejemplo.pe",
            "Persona Prueba");

        Assert.True(result.IsValid);
    }
}
