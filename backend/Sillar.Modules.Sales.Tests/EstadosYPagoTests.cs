using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// Los siete estados visibles y los medios de pago admitidos.
/// </summary>
/// <remarks>
/// Sin base de datos: son decisiones de producto convertidas en listas, y lo que
/// se comprueba es que las listas siguen diciendo lo que se decidió. Si alguien
/// añade un estado o un medio de pago sin pasar por JP, estas pruebas se ponen
/// rojas antes de que llegue a una migración.
/// </remarks>
public sealed class EstadosYPagoTests
{
    [Fact]
    public void Los_estados_visibles_son_exactamente_siete()
    {
        Assert.Equal(7, OrderStatus.All.Length);
    }

    [Fact]
    public void Los_siete_estados_son_los_ratificados_el_26_de_septiembre()
    {
        Assert.Equal(
            [
                "pending_payment",
                "payment_to_verify",
                "preparing",
                "ready_for_pickup",
                "delivered",
                "expired",
                "cancelled"
            ],
            OrderStatus.All);
    }

    [Fact]
    public void La_secuencia_principal_tiene_cinco_estados_en_orden()
    {
        Assert.Equal(
            [
                OrderStatus.PendingPayment,
                OrderStatus.PaymentToVerify,
                OrderStatus.Preparing,
                OrderStatus.ReadyForPickup,
                OrderStatus.Delivered
            ],
            OrderStatus.MainSequence);
    }

    [Fact]
    public void Vencido_no_es_lo_mismo_que_cancelado()
    {
        // No es una comprobación trivial: es la decisión que separa «dejó de estar
        // garantizado» de «ya no existe». Fundirlos obligaría a elegir el peor
        // comportamiento para los dos, y el personal no podría cobrar un pago
        // tardío sobre un pedido que el sistema considera cancelado.
        Assert.NotEqual(OrderStatus.Expired, OrderStatus.Cancelled);
    }

    [Fact]
    public void Vencido_y_cancelado_estan_fuera_de_la_secuencia_principal()
    {
        Assert.DoesNotContain(OrderStatus.Expired, OrderStatus.MainSequence);
        Assert.DoesNotContain(OrderStatus.Cancelled, OrderStatus.MainSequence);
    }

    [Fact]
    public void Ningun_estado_esta_repetido()
    {
        Assert.Equal(OrderStatus.All.Length, OrderStatus.All.Distinct().Count());
    }

    [Fact]
    public void Los_estados_se_guardan_en_ingles_tecnico_y_no_en_espaniol()
    {
        // La frase visible la pone quien pinta. Guardar «Listo para recoger» en la
        // base congelaría el idioma para siempre, y es el mismo criterio que M01
        // aplica al precio: el contrato da el hecho, no la frase.
        Assert.All(OrderStatus.All, estado =>
        {
            Assert.Matches("^[a-z_]+$", estado);
            Assert.DoesNotContain(' ', estado);
        });
    }

    [Fact]
    public void El_unico_medio_de_pago_ratificado_es_yape()
    {
        // El efectivo que PENDIENTES.md menciona está escalado: ni autorizado ni
        // eliminado. Si aparece aquí sin que JP lo decida, esta prueba se pone roja
        // — que es exactamente para lo que está.
        Assert.Equal(["yape"], PaymentMethod.All);
    }

    [Fact]
    public void La_tarjeta_no_esta_entre_los_medios_de_pago()
    {
        // Llega con M11, en la fase 4, y no se adelanta nada de ella.
        Assert.DoesNotContain("tarjeta", PaymentMethod.All);
    }
}
