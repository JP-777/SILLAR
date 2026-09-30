using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Domain;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// Las transiciones ratificadas por JP, como reglas comprobables.
/// </summary>
/// <remarks>
/// Ninguna toca la base: son decisiones de producto y se prueban en milisegundos.
/// Solo se comprueba lo ratificado — donde JP no ha decidido, no hay prueba, porque
/// una prueba escribiría la regla en vez de comprobarla.
/// </remarks>
public sealed class TransicionesTests
{
    private static readonly StaffAttribution Ana = new("Ana Quispe", 7, "principal");

    // ================================================================
    // Cancelación.
    // ================================================================

    [Fact]
    public void Un_pedido_entregado_no_se_puede_cancelar()
    {
        var r = OrderTransitionPolicy.Cancelar(OrderStatus.Delivered, Ana, "el cliente cambió de opinión");

        Assert.False(r.Permitida);
        Assert.Contains("ya se entregó", r.Impedimento!, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_cancelacion_sin_motivo_no_se_permite()
    {
        foreach (var vacio in new string?[] { null, "", "   " })
        {
            var r = OrderTransitionPolicy.Cancelar(OrderStatus.Preparing, Ana, vacio);

            Assert.False(r.Permitida);
            Assert.Contains("motivo", r.Impedimento!, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void El_vencimiento_automatico_nunca_conduce_a_cancelado()
    {
        // Ni por la vía del vencimiento…
        var vencer = OrderTransitionPolicy.VencerPlazo(OrderStatus.PendingPayment);
        Assert.True(vencer.Permitida);
        Assert.Equal(OrderStatus.Expired, vencer.EstadoDestino);
        Assert.NotEqual(OrderStatus.Cancelled, vencer.EstadoDestino);

        // …ni cancelando sin persona, que es lo que sería una cancelación automática.
        var sinPersona = OrderTransitionPolicy.Cancelar(OrderStatus.Expired, quien: null, motivo: "venció");
        Assert.False(sinPersona.Permitida);
    }

    [Fact]
    public void El_cliente_no_cancela_directamente()
    {
        // Sin atribución de personal no hay cancelación: es la misma comprobación que
        // impide una cancelación del sistema, y por eso protege de las dos.
        var r = OrderTransitionPolicy.Cancelar(OrderStatus.PendingPayment, quien: null, motivo: "ya no lo quiero");

        Assert.False(r.Permitida);
        Assert.Contains("Solo el personal", r.Impedimento!, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_cancelacion_del_personal_con_motivo_si_se_permite()
    {
        var r = OrderTransitionPolicy.Cancelar(OrderStatus.Preparing, Ana, "el cliente no recogió en dos semanas");

        Assert.True(r.Permitida);
        Assert.Equal(OrderStatus.Cancelled, r.EstadoDestino);
    }

    [Fact]
    public void Un_pedido_vencido_si_se_puede_cancelar_con_motivo()
    {
        // Vencido no es Cancelado, pero tampoco impide cancelar: son estados distintos
        // y lo que se prohíbe es que el vencimiento cancele por sí solo.
        var r = OrderTransitionPolicy.Cancelar(OrderStatus.Expired, Ana, "el cliente desistió");

        Assert.True(r.Permitida);
        Assert.Equal(OrderStatus.Cancelled, r.EstadoDestino);
    }

    // ================================================================
    // Pago tardío.
    // ================================================================

    [Fact]
    public void Un_pago_tardio_con_mercancia_lleva_el_pedido_de_Vencido_a_Preparando()
    {
        var r = OrderTransitionPolicy.RegistrarPago(OrderStatus.Expired, Ana, hayMercancia: true);

        Assert.True(r.Permitida);
        Assert.Equal(OrderStatus.Preparing, r.EstadoDestino);
    }

    [Fact]
    public void Un_pago_tardio_no_pasa_por_Pago_por_verificar()
    {
        // La persona que registra el pago ya lo verificó: mandarlo a «pago por
        // verificar» pediría verificar lo que acaba de verificarse.
        var r = OrderTransitionPolicy.RegistrarPago(OrderStatus.Expired, Ana, hayMercancia: true);

        Assert.NotEqual(OrderStatus.PaymentToVerify, r.EstadoDestino);
    }

    [Fact]
    public void Un_pago_tardio_sin_mercancia_se_registra_y_deja_pendiente_operativo()
    {
        var r = OrderTransitionPolicy.RegistrarPago(OrderStatus.Expired, Ana, hayMercancia: false);

        Assert.True(r.Permitida);                  // el pago se registra
        Assert.Null(r.EstadoDestino);              // y el pedido no se mueve
        Assert.True(r.DejaPendienteOperativo);     // alguien tiene que llamar
    }

    [Fact]
    public void Un_pago_sin_mercancia_no_inventa_ningun_octavo_estado()
    {
        var r = OrderTransitionPolicy.RegistrarPago(OrderStatus.Expired, Ana, hayMercancia: false);

        // Si algún día alguien "resuelve" esto añadiendo un estado, esta prueba lo dice.
        Assert.Null(r.EstadoDestino);
        Assert.Equal(7, OrderStatus.All.Length);
    }

    [Fact]
    public void Un_pago_no_lo_registra_el_sistema()
    {
        var r = OrderTransitionPolicy.RegistrarPago(OrderStatus.Expired, quien: null, hayMercancia: true);

        Assert.False(r.Permitida);
        Assert.Contains("siempre una persona", r.Impedimento!, StringComparison.Ordinal);
    }

    [Fact]
    public void Ninguna_transicion_permitida_sale_de_los_siete_estados()
    {
        // Barrido: cualquier destino que la política proponga tiene que ser uno de los
        // siete. Es la red que caza un octavo estado por cualquier vía.
        var casos = new List<OrderTransition>();

        foreach (var estado in OrderStatus.All)
        {
            casos.Add(OrderTransitionPolicy.VencerPlazo(estado));
            casos.Add(OrderTransitionPolicy.RegistrarPago(estado, Ana, hayMercancia: true));
            casos.Add(OrderTransitionPolicy.RegistrarPago(estado, Ana, hayMercancia: false));
            casos.Add(OrderTransitionPolicy.Cancelar(estado, Ana, "motivo cualquiera"));
        }

        foreach (var destino in casos.Where(c => c.EstadoDestino is not null).Select(c => c.EstadoDestino!))
        {
            Assert.Contains(destino, OrderStatus.All);
        }
    }

    // ================================================================
    // Vencimiento.
    // ================================================================

    [Fact]
    public void Solo_vence_un_pedido_pendiente_de_pago()
    {
        Assert.Equal(OrderStatus.Expired, OrderTransitionPolicy.VencerPlazo(OrderStatus.PendingPayment).EstadoDestino);

        foreach (var otro in OrderStatus.All.Where(e => e != OrderStatus.PendingPayment))
        {
            var r = OrderTransitionPolicy.VencerPlazo(otro);
            Assert.True(r.Permitida);
            Assert.Null(r.EstadoDestino);
            Assert.False(r.DejaPendienteOperativo);
        }
    }
}
