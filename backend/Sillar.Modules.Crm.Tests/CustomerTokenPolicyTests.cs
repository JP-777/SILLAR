using Sillar.Modules.Crm.Authentication;

namespace Sillar.Modules.Crm.Tests;

public sealed class CustomerTokenPolicyTests
{
    [Fact]
    public void Propósitos_coinciden_con_el_schema()
    {
        Assert.Equal("invitation", CustomerTokenPurpose.Invitation);
        Assert.Equal(
            "email_verification",
            CustomerTokenPurpose.EmailVerification);
        Assert.Equal(
            "password_reset",
            CustomerTokenPurpose.PasswordReset);
    }

    [Fact]
    public void Recuperacion_es_corta()
        => Assert.Equal(
            TimeSpan.FromMinutes(30),
            CustomerTokenPolicy.PasswordResetLifetime);

    [Fact]
    public void Verificacion_caduca()
        => Assert.Equal(
            TimeSpan.FromHours(24),
            CustomerTokenPolicy.EmailVerificationLifetime);

    [Fact]
    public void Invitacion_caduca()
        => Assert.Equal(
            TimeSpan.FromHours(72),
            CustomerTokenPolicy.InvitationLifetime);
}
