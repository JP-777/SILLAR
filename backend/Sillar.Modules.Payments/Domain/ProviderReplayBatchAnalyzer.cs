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

        // Agregar una sola vez evita reconstruir y recorrer todas las
        // comparaciones por cada identidad. Las cardinalidades siguientes son
        // suficientes para conservar la precedencia P2 en tiempo lineal medio.
        var receptionFacts = new Dictionary<
            ReceptionIdentity,
            HashSet<ReceptionFact>>();
        var settlementProviders = new Dictionary<string, HashSet<string>>(
            StringComparer.Ordinal);
        var settlementEvents = new Dictionary<
            ProviderSettlementIdentity,
            HashSet<string>>();
        var knownExactEvidence = new HashSet<ExactEvidenceIdentity>();

        foreach (var evidence in knownEvidence)
        {
            Index(evidence, receptionFacts, settlementProviders, settlementEvents);
            knownExactEvidence.Add(ExactEvidenceIdentity.From(evidence));
        }

        foreach (var item in uniqueIncoming)
        {
            Index(
                item.Representative,
                receptionFacts,
                settlementProviders,
                settlementEvents);
        }

        var results = new List<ProviderReplayBatchItem>(uniqueIncoming.Length);

        foreach (var current in uniqueIncoming)
        {
            var evidence = current.Representative;
            var reception = new ReceptionIdentity(
                evidence.Provider,
                evidence.ProviderEventId);
            var settlement = new ProviderSettlementIdentity(
                evidence.Provider,
                evidence.ExternalSettlementId);
            var conflictingEvent = current.HasContradictoryBodyEvidence
                || receptionFacts[reception].Count > 1;
            var conflictingSettlement =
                settlementProviders[evidence.ExternalSettlementId].Count > 1;
            var duplicateEvent = current.Occurrences > 1
                || knownExactEvidence.Contains(ExactEvidenceIdentity.From(evidence));
            var relatedSettlement = settlementEvents[settlement].Count > 1;

            var decision = conflictingEvent
                ? ProviderReplayDecision.ConflictingEvent
                : conflictingSettlement
                    ? ProviderReplayDecision.ConflictingSettlement
                    : duplicateEvent
                        ? ProviderReplayDecision.DuplicateEvent
                        : relatedSettlement
                            ? ProviderReplayDecision.RelatedSettlement
                            : ProviderReplayDecision.NewEvent;

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

    private static void Index(
        VerifiedExternalPaymentEvidence evidence,
        IDictionary<ReceptionIdentity, HashSet<ReceptionFact>> receptionFacts,
        IDictionary<string, HashSet<string>> settlementProviders,
        IDictionary<ProviderSettlementIdentity, HashSet<string>> settlementEvents)
    {
        var reception = new ReceptionIdentity(
            evidence.Provider,
            evidence.ProviderEventId);
        if (!receptionFacts.TryGetValue(reception, out var facts))
        {
            facts = [];
            receptionFacts.Add(reception, facts);
        }

        facts.Add(new ReceptionFact(
            evidence.ExternalSettlementId,
            evidence.BodySha256));

        if (!settlementProviders.TryGetValue(
                evidence.ExternalSettlementId,
                out var providers))
        {
            providers = new HashSet<string>(StringComparer.Ordinal);
            settlementProviders.Add(evidence.ExternalSettlementId, providers);
        }

        providers.Add(evidence.Provider);

        var settlement = new ProviderSettlementIdentity(
            evidence.Provider,
            evidence.ExternalSettlementId);
        if (!settlementEvents.TryGetValue(settlement, out var events))
        {
            events = new HashSet<string>(StringComparer.Ordinal);
            settlementEvents.Add(settlement, events);
        }

        events.Add(evidence.ProviderEventId);
    }

    private sealed record ReceptionIdentity(
        string Provider,
        string ProviderEventId);

    private sealed record ReceptionFact(
        string ExternalSettlementId,
        string BodySha256);

    private sealed record ProviderSettlementIdentity(
        string Provider,
        string ExternalSettlementId);

    private sealed record ExactEvidenceIdentity(
        string Provider,
        string ProviderEventId,
        string ExternalSettlementId,
        string BodySha256)
    {
        internal static ExactEvidenceIdentity From(
            VerifiedExternalPaymentEvidence evidence)
            => new(
                evidence.Provider,
                evidence.ProviderEventId,
                evidence.ExternalSettlementId,
                evidence.BodySha256);
    }
}
