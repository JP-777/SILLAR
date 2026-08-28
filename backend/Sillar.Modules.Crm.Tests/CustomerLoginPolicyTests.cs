using Sillar.Modules.Crm.Authentication;

namespace Sillar.Modules.Crm.Tests;

public sealed class CustomerLoginPolicyTests
{
    [Fact]
    public void BCrypt_cliente_nunca_baja_de_factor_12()
        => Assert.True(CustomerPasswordHasher.WorkFactor >= 12);

    [Fact]
    public void Cuenta_no_se_bloquea_y_la_espera_crece()
    {
        Assert.Equal(TimeSpan.Zero,
            CustomerLoginThrottlePolicy.AccountDelay(1));

        Assert.Equal(TimeSpan.Zero,
            CustomerLoginThrottlePolicy.AccountDelay(2));

        Assert.Equal(TimeSpan.FromMilliseconds(250),
            CustomerLoginThrottlePolicy.AccountDelay(3));

        Assert.Equal(TimeSpan.FromSeconds(8),
            CustomerLoginThrottlePolicy.AccountDelay(50));
    }

    [Fact]
    public void Ip_tiene_un_margen_mucho_mas_generoso()
    {
        Assert.Equal(TimeSpan.Zero,
            CustomerLoginThrottlePolicy.IpDelay(20));

        Assert.Equal(TimeSpan.FromMilliseconds(250),
            CustomerLoginThrottlePolicy.IpDelay(21));

        Assert.Equal(TimeSpan.FromSeconds(8),
            CustomerLoginThrottlePolicy.IpDelay(100));
    }

    [Fact]
    public void Sesion_cliente_tiene_politica_propia()
        => Assert.Equal(
            TimeSpan.FromDays(7),
            CustomerSessionPolicy.Lifetime);
}
