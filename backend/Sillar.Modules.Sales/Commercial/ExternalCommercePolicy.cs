using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Sales.Commercial;

/// <summary>
/// Lo que la política necesita saber de un pedido, sin arrastrar la entidad.
/// </summary>
/// <remarks>
/// <b>Es una fotografía, no un <c>Order</c>.</b> La política no debe poder navegar a
/// líneas, cliente ni pagos: lo que no está aquí no puede influir en la decisión, y
/// eso es lo que permite provocarla en milisegundos.
/// </remarks>
/// <param name="OrderId">El pedido.</param>
/// <param name="OrderCode">Su código visible.</param>
/// <param name="Status">Estado actual, del vocabulario de <see cref="OrderStatus"/>.</param>
/// <param name="TotalAmount">Total congelado tal como está guardado.</param>
/// <param name="ManuallySettledTotal">Suma de los pagos manuales ya registrados.</param>
/// <param name="IsActive">Si sigue vivo o está dado de baja lógica.</param>
public sealed record OrderCommercialSnapshot(
    Guid OrderId,
    string OrderCode,
    string Status,
    decimal TotalAmount,
    decimal ManuallySettledTotal,
    bool IsActive);

/// <summary>
/// Una propuesta de liquidación que ya se vio antes en este proceso.
/// </summary>
/// <remarks>
/// <b>No es un hecho aplicado.</b> Es la memoria de haber sido preguntado, que es lo
/// único que FASE A puede tener sin almacén.
/// </remarks>
public sealed record SettlementCandidateRecord(
    string Provider,
    string ProviderEventId,
    string? ExternalSettlementId,
    Guid OrderId,
    decimal Amount,
    string? Currency);

/// <summary>
/// Las decisiones del pago externo candidato, como reglas puras.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sin base, sin reloj y sin endpoints</b>, igual que
/// <see cref="Domain.OrderTransitionPolicy"/>: la guarda vive aquí y quien hace E/S
/// la consulta. Es lo que hace que estas reglas se puedan provocar sin PostgreSQL.
/// </para>
/// <para>
/// <b>Solo decide lo que ya está decidido.</b> Donde JP no ha ratificado, el
/// resultado es <c>PendingDecision</c> con el motivo que nombra <b>qué</b> falta
/// decidir. Un motivo genérico obligaría a adivinar cuál de las tres decisiones
/// abiertas es la que bloquea.
/// </para>
/// </remarks>
public static class ExternalCommercePolicy
{
    /// <summary>
    /// Evalúa la pagabilidad de un pedido para una liquidación externa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Las compuertas van en este orden y el orden es la regla:</b> existencia,
    /// baja lógica, importe, ciclo de vida y moneda. Invertir las dos últimas haría
    /// que un pedido cancelado se explicara por la moneda, que es cierto y es el
    /// motivo equivocado.
    /// </para>
    /// <para>
    /// <b>Cancelado y entregado no se deciden aquí.</b> M03 ya tiene escrito que un
    /// pago sobre ellos necesita una decisión —y la tiene escrita para el pago que
    /// registra una persona—, así que llevarla al pago de proveedor sería extender
    /// una ratificación a un caso que no cubre.
    /// </para>
    /// </remarks>
    /// <param name="orderId">El pedido consultado, para poder responder aunque no exista.</param>
    /// <param name="pedido">Su fotografía, o <c>null</c> si no existe.</param>
    public static PayableOrderCandidate EvaluarPagabilidad(
        Guid orderId,
        OrderCommercialSnapshot? pedido)
    {
        if (pedido is null)
        {
            return new(orderId, null, null,
                PayableOutcome.NotFound, PayableReason.UnknownOrder,
                CandidateSources.CurrentDatabase);
        }

        PayableOrderCandidate Responder(PayableOutcome desenlace, PayableReason motivo)
            => new(pedido.OrderId, pedido.TotalAmount, null, desenlace, motivo,
                CandidateSources.CurrentDatabase)
            {
                OrderCode = pedido.OrderCode,
                Status = pedido.Status,
                ManuallySettledTotal = pedido.ManuallySettledTotal,
            };

        if (!pedido.IsActive)
        {
            return Responder(PayableOutcome.NotEligible, PayableReason.InactiveOrder);
        }

        // No debería ocurrir —lo prohíbe ck_orders_total_amount_no_negativo— y por eso
        // mismo se comprueba: si ocurre, es un dato roto y no una pregunta comercial.
        if (pedido.TotalAmount < 0)
        {
            return Responder(PayableOutcome.NotEligible, PayableReason.InvalidAmount);
        }

        if (pedido.Status is OrderStatus.Cancelled or OrderStatus.Delivered)
        {
            return Responder(PayableOutcome.PendingDecision, PayableReason.OrderLifecycleUnratified);
        }

        // Lo ya cobrado a mano cubre el total. Que una liquidación externa se sume,
        // se rechace o reemplace es política, y no está ratificada.
        if (pedido.ManuallySettledTotal > 0
            && pedido.ManuallySettledTotal >= pedido.TotalAmount)
        {
            return Responder(PayableOutcome.PendingDecision, PayableReason.AlreadySettledManually);
        }

        // Última compuerta, y la que hoy detiene a todos los pedidos sanos: M03 no
        // almacena la moneda del pedido, así que no puede afirmarla como autoritativa.
        return Responder(PayableOutcome.PendingDecision, PayableReason.CurrencyAuthorityMissing);
    }

    /// <summary>
    /// Evalúa una propuesta de liquidación externa frente a lo ya visto.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La autenticación va antes que cualquier regla comercial.</b> Un evento sin
    /// evidencia verificada no llega a ser una pregunta de negocio, así que se rechaza
    /// aunque además le falte la moneda. Al revés, un evento no autenticado saldría
    /// como «pendiente de decisión» y parecería que espera a JP.
    /// </para>
    /// <para>
    /// <b>El reintento se compara por hechos, no por clave.</b> Devolver la primera
    /// respuesta porque coincide la identidad del evento es cómo un reintento
    /// incompatible pasa por duplicado inocuo.
    /// </para>
    /// <para>
    /// <b>Las comparaciones de texto son ordinales, a propósito.</b> Dos tokens de
    /// liquidación que solo difieren en mayúsculas son hechos distintos; una
    /// comparación que los funda convertiría un segundo pago real en un duplicado y
    /// no lo registraría nunca. Cuando esto llegue a tener columna, su colación
    /// tendrá que ser determinista por la misma razón.
    /// </para>
    /// </remarks>
    /// <param name="candidato">La propuesta.</param>
    /// <param name="previo">Lo ya visto con esa misma identidad de evento, si lo hay.</param>
    public static ExternalSettlementCandidateResult EvaluarLiquidacion(
        ExternalSettlementCandidateEvent candidato,
        SettlementCandidateRecord? previo)
    {
        if (candidato.OrderId == Guid.Empty
            || string.IsNullOrWhiteSpace(candidato.Provider)
            || string.IsNullOrWhiteSpace(candidato.ProviderEventId))
        {
            return Rechaza(ExternalSettlementReason.InvalidIdentity);
        }

        if (string.IsNullOrWhiteSpace(candidato.VerifiedEvidenceReference))
        {
            return Rechaza(ExternalSettlementReason.UnauthenticatedEvent);
        }

        if (candidato.Amount <= 0)
        {
            return Rechaza(ExternalSettlementReason.InvalidAmount);
        }

        if (string.IsNullOrWhiteSpace(candidato.Currency))
        {
            return Pendiente(ExternalSettlementReason.MissingCurrency);
        }

        if (previo is not null)
        {
            return EsEquivalente(candidato, previo)
                ? Pendiente(ExternalSettlementReason.EquivalentReplayWithoutDurableEffect)
                : Pendiente(ExternalSettlementReason.InconsistentReplay);
        }

        if (string.IsNullOrWhiteSpace(candidato.ExternalSettlementId))
        {
            return Pendiente(ExternalSettlementReason.DurableIdentityPolicyUnratified);
        }

        // Todo lo comprobable está comprobado. Lo que falta no es técnico.
        return Pendiente(ExternalSettlementReason.CommercialPolicyUnratified);
    }

    /// <summary>
    /// Si dos propuestas con la misma identidad de evento dicen lo mismo.
    /// </summary>
    /// <remarks>
    /// Compara los cuatro hechos que importan. Añadir un hecho al evento y olvidarlo
    /// aquí es cómo una divergencia se vuelve invisible.
    /// </remarks>
    private static bool EsEquivalente(
        ExternalSettlementCandidateEvent candidato,
        SettlementCandidateRecord previo)
        => candidato.OrderId == previo.OrderId
            && candidato.Amount == previo.Amount
            && string.Equals(candidato.Currency, previo.Currency, StringComparison.Ordinal)
            && string.Equals(
                candidato.ExternalSettlementId,
                previo.ExternalSettlementId,
                StringComparison.Ordinal);

    private static ExternalSettlementCandidateResult Rechaza(ExternalSettlementReason motivo)
        => new(ExternalSettlementOutcome.Rejected, motivo);

    /// <remarks>
    /// <b>Nunca lleva referencia de efecto durable</b>: en FASE A no hay ninguna, y un
    /// valor ahí afirmaría una transacción que no ocurrió.
    /// </remarks>
    private static ExternalSettlementCandidateResult Pendiente(ExternalSettlementReason motivo)
        => new(ExternalSettlementOutcome.PendingDecision, motivo);
}
