namespace Sillar.Modules.Payments.Domain;

/// <summary>Resultado semántico del cotejo de una recepción externa.</summary>
/// <remarks>
/// Ningún resultado afirma que exista un inbox durable o que M03 haya aplicado
/// un pago. Sólo describe la relación entre identidades externas conocidas.
/// </remarks>
public enum ProviderReplayDecision
{
    NewEvent,
    DuplicateEvent,
    RelatedSettlement,
    ConflictingEvent,
    ConflictingSettlement
}

/// <summary>
/// Coteja evidencia externa verificada sin sustituir las futuras barreras
/// transaccionales de M11 y M03.
/// </summary>
/// <remarks>
/// Reutiliza las dos identidades de P1. Los valores externos son opacos y se
/// comparan ordinalmente; no se les atribuye formato, datos personales ni una
/// garantía de procesamiento exactly-once.
/// </remarks>
public static class ProviderReplayClassifier
{
    /// <summary>
    /// Examina toda la evidencia conocida y clasifica la recepción entrante.
    /// Cualquier conflicto prevalece sobre duplicados y relaciones, sin depender
    /// del orden de enumeración.
    /// </summary>
    public static ProviderReplayDecision Classify(
        VerifiedExternalPaymentEvidence incoming,
        IEnumerable<VerifiedExternalPaymentEvidence> previouslyReceived)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(previouslyReceived);

        var incomingReception = ProviderEventIdempotencyKey.From(incoming);
        var incomingSettlement = ExternalSettlementIdempotencyKey.From(incoming);

        var conflictingEvent = false;
        var conflictingSettlement = false;
        var duplicateEvent = false;
        var relatedSettlement = false;

        foreach (var previous in previouslyReceived)
        {
            ArgumentNullException.ThrowIfNull(previous);

            var previousReception = ProviderEventIdempotencyKey.From(previous);
            var previousSettlement = ExternalSettlementIdempotencyKey.From(previous);
            var sameProvider = StringComparer.Ordinal.Equals(
                previousReception.Provider,
                incomingReception.Provider);
            var sameSettlement = previousSettlement == incomingSettlement;

            // La misma liquidación bajo proveedores diferentes es una colisión
            // conservadora. No se presupone que uno de los proveedores sea dueño.
            if (!sameProvider)
            {
                conflictingSettlement |= sameSettlement;
                continue;
            }

            var sameEvent = previousReception == incomingReception;

            if (sameEvent)
            {
                var sameBodyEvidence = StringComparer.Ordinal.Equals(
                    previous.BodySha256,
                    incoming.BodySha256);

                if (sameSettlement && sameBodyEvidence)
                {
                    duplicateEvent = true;
                }
                else
                {
                    // Reutilizar la misma identidad de recepción con otra
                    // liquidación o con evidencia corporal distinta no es una
                    // reentrega equivalente y debe fallar cerrado.
                    conflictingEvent = true;
                }
            }
            else if (sameSettlement)
            {
                // Relación entre eventos, no prueba de un pago aplicado.
                relatedSettlement = true;
            }
        }

        // No retornar dentro del recorrido: un duplicado temprano no debe ocultar
        // un conflicto situado después en la secuencia.
        if (conflictingEvent)
        {
            return ProviderReplayDecision.ConflictingEvent;
        }

        if (conflictingSettlement)
        {
            return ProviderReplayDecision.ConflictingSettlement;
        }

        if (duplicateEvent)
        {
            return ProviderReplayDecision.DuplicateEvent;
        }

        return relatedSettlement
            ? ProviderReplayDecision.RelatedSettlement
            : ProviderReplayDecision.NewEvent;
    }
}
