using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Sales.Domain;

/// <summary>
/// El resultado de intentar una transición de estado.
/// </summary>
/// <param name="Permitida">Si la transición puede ocurrir.</param>
/// <param name="EstadoDestino">
/// El estado al que pasa el pedido. <c>null</c> cuando <b>no cambia de estado</b>, que
/// no es lo mismo que rechazar: un pago tardío sobre un pedido que ya no se puede
/// atender <b>se registra</b> y el pedido <b>se queda donde está</b>.
/// </param>
/// <param name="DejaPendienteOperativo">
/// Si queda algo que una persona tiene que resolver. Nunca se inventa una transición
/// para representarlo.
/// </param>
/// <param name="Impedimento">
/// Por qué no se permite, en una frase que dice qué lo impide. <c>null</c> si se
/// permite.
/// </param>
public sealed record OrderTransition(
    bool Permitida,
    string? EstadoDestino,
    bool DejaPendienteOperativo,
    string? Impedimento)
{
    internal static OrderTransition Permite(string destino)
        => new(true, destino, false, null);

    internal static OrderTransition PermiteSinCambiarEstado(bool pendiente)
        => new(true, null, pendiente, null);

    internal static OrderTransition Rechaza(string impedimento)
        => new(false, null, false, impedimento);
}

/// <summary>
/// Las transiciones de estado ratificadas por JP, como decisiones puras.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sin base de datos, sin reloj y sin endpoints.</b> Son reglas de producto, y
/// tenerlas aquí permite provocarlas en milisegundos — que es lo que hace que alguien
/// las provoque. La operación que un día las ejecute <b>consulta esta política</b>; la
/// guarda vive aquí, no en quien llama.
/// </para>
/// <para>
/// <b>Solo contiene lo ratificado.</b> Donde JP no ha decidido, no hay método: no se
/// deduce una transición comercial de una enum.
/// </para>
/// </remarks>
public static class OrderTransitionPolicy
{
    /// <summary>
    /// Registrar un pago sobre un pedido, incluido uno vencido.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El pago SIEMPRE se registra.</b> Esta política no decide si el pago se
    /// guarda —se guarda— sino qué le pasa al <b>estado del pedido</b> después. Son
    /// dos cosas distintas y fundirlas es el defecto que M04 ya corrigió una vez.
    /// </para>
    /// <para>
    /// <b>Vencido con mercancía pasa a Preparando, sin escala en «Pago por
    /// verificar».</b> Y el motivo no es un atajo: <b>la persona que registra el pago
    /// ya lo verificó</b>. Mandarlo a «pago por verificar» pediría que alguien
    /// verifique lo que acaba de verificar, y dejaría al cliente viendo un estado que
    /// no describe lo que ocurrió.
    /// </para>
    /// <para>
    /// <b>Vencido sin mercancía no cambia de estado</b> y <b>no se inventa un octavo
    /// estado</b> para ello. Queda un pendiente operativo: alguien tiene que llamar al
    /// cliente. Fabricar una transición automática aquí sería afirmar que el sistema
    /// resolvió algo que no resolvió.
    /// </para>
    /// </remarks>
    /// <param name="estadoActual">Estado del pedido antes del pago.</param>
    /// <param name="quien">
    /// Quién lo registra. <b>Obligatorio:</b> un pago no lo registra el sistema.
    /// </param>
    /// <param name="hayMercancia">Si el pedido todavía puede atenderse.</param>
    public static OrderTransition RegistrarPago(
        string estadoActual,
        StaffAttribution? quien,
        bool hayMercancia)
    {
        if (quien is null)
        {
            return OrderTransition.Rechaza(
                "Un pago lo registra siempre una persona: no hay pago del sistema.");
        }

        if (estadoActual is OrderStatus.Cancelled)
        {
            return OrderTransition.Rechaza(
                "Este pedido está cancelado. Antes de cobrarlo hay que decidir qué se " +
                "hace con la cancelación.");
        }

        if (estadoActual is OrderStatus.Delivered)
        {
            return OrderTransition.Rechaza(
                "Este pedido ya se entregó. Un pago posterior necesita una decisión que " +
                "todavía no está tomada.");
        }

        // Vencido: el caso que la decisión del 27/09 §8 cierra.
        if (estadoActual is OrderStatus.Expired)
        {
            return hayMercancia
                ? OrderTransition.Permite(OrderStatus.Preparing)
                : OrderTransition.PermiteSinCambiarEstado(pendiente: true);
        }

        // Pendiente de pago o pago por verificar: la persona ya lo verificó.
        if (estadoActual is OrderStatus.PendingPayment or OrderStatus.PaymentToVerify)
        {
            return hayMercancia
                ? OrderTransition.Permite(OrderStatus.Preparing)
                : OrderTransition.PermiteSinCambiarEstado(pendiente: true);
        }

        // Preparando o listo para recoger: el pago se registra y el estado ya es el
        // que corresponde. No hay nada que mover.
        return OrderTransition.PermiteSinCambiarEstado(pendiente: false);
    }

    /// <summary>
    /// Cancelar un pedido.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Solo el personal cancela, y exige motivo.</b> El cliente no cancela
    /// directamente en v1.
    /// </para>
    /// <para>
    /// <b>Un pedido entregado no se cancela.</b> Ya salió de la tienda: lo que
    /// corresponda entonces es una devolución, que es deuda con su propio disparador y
    /// no una cancelación.
    /// </para>
    /// <para>
    /// <b>Y el vencimiento nunca llega aquí.</b> Vencer no es cancelar: son dos
    /// estados distintos y esta política no ofrece ningún camino del uno al otro sin
    /// una persona y un motivo.
    /// </para>
    /// </remarks>
    /// <param name="estadoActual">Estado del pedido.</param>
    /// <param name="quien">
    /// Quién cancela. <b>Obligatorio:</b> no hay cancelación del sistema.
    /// </param>
    /// <param name="motivo">Por qué se cancela. <b>Obligatorio.</b></param>
    public static OrderTransition Cancelar(
        string estadoActual,
        StaffAttribution? quien,
        string? motivo)
    {
        if (quien is null)
        {
            return OrderTransition.Rechaza(
                "Solo el personal cancela un pedido, y hay que saber quién lo hizo: " +
                "no hay cancelación automática ni del cliente.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return OrderTransition.Rechaza(
                "Cancelar un pedido exige un motivo. Sin él, dentro de un mes nadie " +
                "sabrá por qué se canceló, y el cliente que pregunte no tendrá respuesta.");
        }

        if (estadoActual is OrderStatus.Delivered)
        {
            return OrderTransition.Rechaza(
                "Este pedido ya se entregó y no se puede cancelar. Lo que corresponda " +
                "ahora no es una cancelación.");
        }

        if (estadoActual is OrderStatus.Cancelled)
        {
            return OrderTransition.Rechaza("Este pedido ya estaba cancelado.");
        }

        return OrderTransition.Permite(OrderStatus.Cancelled);
    }

    /// <summary>
    /// Vencer el plazo para pagar. Lo provoca el tiempo, no una persona.
    /// </summary>
    /// <remarks>
    /// <b>Solo actúa sobre «Pendiente de pago».</b> Un pedido que ya tiene pago por
    /// verificar, o que está preparándose, no vence: lo que vence es la espera de un
    /// pago que no llegó. Y <b>nunca conduce a Cancelado</b>: el pedido sigue
    /// existiendo, deja de estar garantizado, y no se cancela por este hecho.
    /// </remarks>
    /// <param name="estadoActual">Estado del pedido.</param>
    public static OrderTransition VencerPlazo(string estadoActual)
        => estadoActual is OrderStatus.PendingPayment
            ? OrderTransition.Permite(OrderStatus.Expired)
            : OrderTransition.PermiteSinCambiarEstado(pendiente: false);
}
