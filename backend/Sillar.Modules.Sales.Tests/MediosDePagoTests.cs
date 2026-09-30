using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Sales.Tests;

/// <summary>Los medios de pago de v1, cerrados por JP.</summary>
public sealed class MediosDePagoTests
{
    [Fact]
    public void V1_admite_yape_y_efectivo()
    {
        Assert.Equal(["yape", "efectivo"], PaymentMethod.All);
    }

    [Fact]
    public void Yape_es_un_medio_valido()
    {
        Assert.Contains(PaymentMethod.Yape, PaymentMethod.All);
        Assert.Equal("yape", PaymentMethod.Yape);
    }

    [Fact]
    public void El_efectivo_es_un_medio_valido()
    {
        Assert.Contains(PaymentMethod.Cash, PaymentMethod.All);
        Assert.Equal("efectivo", PaymentMethod.Cash);
    }

    [Fact]
    public void La_tarjeta_no_se_admite_en_M03_v1()
    {
        // Llega con M11 en la fase 4 y no se adelanta nada de ella.
        Assert.DoesNotContain("tarjeta", PaymentMethod.All);
        Assert.DoesNotContain("card", PaymentMethod.All);
    }

    [Fact]
    public void Cualquier_otro_medio_queda_fuera()
    {
        foreach (var ajeno in new[] { "tarjeta", "transferencia", "plin", "yape ", "Efectivo", "" })
        {
            Assert.DoesNotContain(ajeno, PaymentMethod.All);
        }
    }

    [Fact]
    public void Los_valores_van_en_minusculas_y_sin_espacios()
    {
        // El CHECK de la migración se escribe desde esta lista: un valor con espacio o
        // mayúscula produciría un CHECK que rechaza lo que el código envía.
        Assert.All(PaymentMethod.All, m => Assert.Matches("^[a-z]+$", m));
    }
}
