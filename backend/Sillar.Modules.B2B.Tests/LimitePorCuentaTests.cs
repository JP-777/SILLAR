using Sillar.Modules.B2B.Solicitudes;

namespace Sillar.Modules.B2B.Tests;

/// <summary>Plan de pruebas §2 — contra el parámetro, no contra un número.</summary>
public sealed class LimitePorCuentaTests
{
    private static readonly LimiteDeSolicitudes Limite = new(3, TimeSpan.FromMinutes(10));
    private static readonly DateTimeOffset T0 = new(2026, 9, 29, 12, 0, 0, TimeSpan.FromHours(-5));

    private static (LimitePorCuenta, Guid) Nuevo() => (new LimitePorCuenta(Limite), Guid.NewGuid());

    [Fact]
    public void La_solicitud_N_dentro_de_la_ventana_pasa()
    {
        var (limite, cuenta) = Nuevo();
        for (var i = 0; i < Limite.Maximo; i++)
        {
            Assert.True(limite.Intentar(cuenta, T0.AddSeconds(i)).Permitida, $"la {i + 1}.ª no pasó");
        }
    }

    [Fact]
    public void La_solicitud_N_mas_1_se_rechaza_y_dice_cuando_volver()
    {
        var (limite, cuenta) = Nuevo();
        for (var i = 0; i < Limite.Maximo; i++) limite.Intentar(cuenta, T0.AddMinutes(i));

        var decision = limite.Intentar(cuenta, T0.AddMinutes(Limite.Maximo));

        Assert.False(decision.Permitida);
        Assert.Equal(Limite.Ventana - TimeSpan.FromMinutes(Limite.Maximo), decision.EsperarHasta);
    }

    [Fact]
    public void Pasada_la_ventana_la_cuenta_vuelve_a_poder()
    {
        var (limite, cuenta) = Nuevo();
        for (var i = 0; i < Limite.Maximo; i++) limite.Intentar(cuenta, T0);

        Assert.True(limite.Intentar(cuenta, T0 + Limite.Ventana).Permitida);
    }

    /// <summary>La que caza una copia literal del precedente por IP.</summary>
    [Fact]
    public void Una_cuenta_agotada_no_bloquea_a_otra()
    {
        var (limite, agotada) = Nuevo();
        for (var i = 0; i < Limite.Maximo; i++) limite.Intentar(agotada, T0);

        Assert.False(limite.Intentar(agotada, T0).Permitida);
        Assert.True(limite.Intentar(Guid.NewGuid(), T0).Permitida);
    }
}
