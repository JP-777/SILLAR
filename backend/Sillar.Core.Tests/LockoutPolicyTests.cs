using Sillar.Core.Authentication;

namespace Sillar.Core.Tests;

/// <summary>Bloqueo tras intentos fallidos.</summary>
public class LockoutPolicyTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 8, 14, 10, 0, 0, TimeSpan.FromHours(-5));

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void Menos_de_cinco_fallos_no_bloquean(int fallos)
    {
        Assert.Null(LockoutPolicy.LockedUntil(fallos, Ahora));
    }

    [Fact]
    public void El_quinto_fallo_bloquea_quince_minutos()
    {
        Assert.Equal(Ahora.AddMinutes(15), LockoutPolicy.LockedUntil(5, Ahora));
    }

    [Fact]
    public void Los_fallos_siguientes_reinician_los_quince_minutos()
    {
        Assert.Equal(Ahora.AddMinutes(15), LockoutPolicy.LockedUntil(9, Ahora));
    }

    [Fact]
    public void Una_cuenta_sin_bloqueo_no_esta_bloqueada()
    {
        Assert.False(LockoutPolicy.IsLocked(null, Ahora));
    }

    [Fact]
    public void El_bloqueo_termina_solo_al_pasar_el_plazo()
    {
        var hasta = Ahora.AddMinutes(15);

        Assert.True(LockoutPolicy.IsLocked(hasta, Ahora.AddMinutes(14)));
        Assert.False(LockoutPolicy.IsLocked(hasta, Ahora.AddMinutes(16)));
    }
}
