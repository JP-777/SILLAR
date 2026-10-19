namespace Sillar.Modules.Payments.Domain;

/// <summary>Una identidad externa única, agrupada dentro del lote.</summary>
/// <remarks>
/// <see cref="Occurrences"/> informa cuántas veces apareció la identidad exacta;
/// no representa cobros aplicados ni una garantía durable de deduplicación.
/// </remarks>
public sealed record ProviderReplayBatchItem(
    PaymentIdempotencyKeys Identity,
    int Occurrences,
    ProviderReplayDecision Decision);

/// <summary>Diagnóstico puro del lote, sin escrituras ni efectos externos.</summary>
public sealed record ProviderReplayBatchResult(
    IReadOnlyList<ProviderReplayBatchItem> Items,
    bool HasConflicts);

/// <summary>
/// Clasifica deterministamente un lote de evidencia externa ya verificada.
/// </summary>
/// <remarks>
/// Coteja cada identidad única contra todo lo conocido y todas las demás
/// identidades distintas del lote. No escribe inbox ni ledger, no autentica
/// webhooks y no afirma que un pago haya sido aplicado.
/// </remarks>
public static class ProviderReplayBatchAnalyzer
{
    /// <summary>
    /// Enumera una vez cada entrada, agrupa las identidades exactas y devuelve
    /// resultados ordenados ordinalmente, independientes del orden de llegada.
    /// </summary>
    public static ProviderReplayBatchResult Analyze(
        IEnumerable<VerifiedExternalPaymentEvidence> incoming,
        IEnumerable<VerifiedExternalPaymentEvidence> known)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(known);

        var incomingEvidence = incoming.ToArray();
        var knownEvidence = known.ToArray();

        foreach (var evidence in incomingEvidence)
        {
            ArgumentNullException.ThrowIfNull(evidence);
        }

        foreach (var evidence in knownEvidence)
        {
            ArgumentNullException.ThrowIfNull(evidence);
        }

        var uniqueIncoming = incomingEvidence
            .GroupBy(PaymentIdempotencyKeys.From)
            .Select(group => new UniqueIncomingEvidence(
                group.Key,
                group.First(),
                group.Count(),
                group
                    .Select(evidence => evidence.BodySha256)
                    .Distinct(StringComparer.Ordinal)
                    .Skip(1)
                    .Any()))
            .OrderBy(
                item => item.Identity.Reception.Provider,
                StringComparer.Ordinal)
            .ThenBy(
                item => item.Identity.Reception.ProviderEventId,
                StringComparer.Ordinal)
            .ThenBy(
                item => item.Identity.Settlement.Value,
                StringComparer.Ordinal)
            .ToArray();

        var results = new List<ProviderReplayBatchItem>(uniqueIncoming.Length);

        for (var index = 0; index < uniqueIncoming.Length; index++)
        {
            var current = uniqueIncoming[index];
            var comparisons = new List<VerifiedExternalPaymentEvidence>(
                knownEvidence.Length + uniqueIncoming.Length);

            comparisons.AddRange(knownEvidence);

            for (var otherIndex = 0; otherIndex < uniqueIncoming.Length; otherIndex++)
            {
                if (otherIndex != index)
                {
                    comparisons.Add(uniqueIncoming[otherIndex].Representative);
                }
            }

            // Una repetición exacta dentro del lote es un duplicado semántico.
            // Se añade sólo una copia: la multiplicidad completa ya se informa y
            // P2 examinará además todos los conflictos antes de decidir.
            if (current.Occurrences > 1)
            {
                comparisons.Add(current.Representative);
            }

            var decision = ProviderReplayClassifier.Classify(
                current.Representative,
                comparisons);

            // El agrupamiento por claves de idempotencia no puede ocultar que
            // una misma identidad llegó con cuerpos autenticados diferentes.
            // El conflicto prevalece incluso si también hubo copias exactas.
            if (current.HasContradictoryBodyEvidence)
            {
                decision = ProviderReplayDecision.ConflictingEvent;
            }

            results.Add(new ProviderReplayBatchItem(
                current.Identity,
                current.Occurrences,
                decision));
        }

        var hasConflicts = results.Any(item =>
            item.Decision is ProviderReplayDecision.ConflictingEvent
                or ProviderReplayDecision.ConflictingSettlement);

        return new ProviderReplayBatchResult(results.AsReadOnly(), hasConflicts);
    }

    private sealed record UniqueIncomingEvidence(
        PaymentIdempotencyKeys Identity,
        VerifiedExternalPaymentEvidence Representative,
        int Occurrences,
        bool HasContradictoryBodyEvidence);
}
