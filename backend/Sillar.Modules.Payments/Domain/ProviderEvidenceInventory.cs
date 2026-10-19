namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Ocurrencia normalizada de evidencia P1, sin cuerpo crudo.
/// </summary>
/// <remarks>
/// <see cref="Occurrences"/> sólo cuenta repeticiones exactas dentro de la
/// instantánea analizada. No representa recepción durable ni deduplicación.
/// </remarks>
public sealed record ProviderEvidenceInventoryItem(
    ProviderEvidencePopulation Population,
    string Provider,
    string ProviderEventId,
    string ExternalSettlementId,
    string BodySha256,
    DateTimeOffset VerifiedAt,
    int Occurrences);

/// <summary>Inventario completo y determinista de evidencia normalizada.</summary>
public sealed record ProviderEvidenceInventoryResult(
    IReadOnlyList<ProviderEvidenceInventoryItem> Items,
    int DistinctEvidenceItems,
    int IncomingOccurrences,
    int KnownOccurrences);

/// <summary>
/// Materializa toda la evidencia entrante y conocida para que la trazabilidad
/// no dependa únicamente de los casos conflictivos.
/// </summary>
public static class ProviderEvidenceInventory
{
    public static ProviderEvidenceInventoryResult Create(
        IEnumerable<VerifiedExternalPaymentEvidence> incoming,
        IEnumerable<VerifiedExternalPaymentEvidence> known)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(known);

        var arrivals = Materialize(
            incoming,
            ProviderEvidencePopulation.Incoming,
            nameof(incoming));
        var history = Materialize(
            known,
            ProviderEvidencePopulation.PreviouslyKnown,
            nameof(known));
        var grouped = arrivals
            .Concat(history)
            .GroupBy(item => new EvidenceIdentity(
                item.Population,
                item.Provider,
                item.ProviderEventId,
                item.ExternalSettlementId,
                item.BodySha256,
                item.VerifiedAt))
            .Select(group => new ProviderEvidenceInventoryItem(
                group.Key.Population,
                group.Key.Provider,
                group.Key.ProviderEventId,
                group.Key.ExternalSettlementId,
                group.Key.BodySha256,
                group.Key.VerifiedAt,
                group.Count()))
            .OrderBy(item => item.Population)
            .ThenBy(item => item.Provider, StringComparer.Ordinal)
            .ThenBy(item => item.ProviderEventId, StringComparer.Ordinal)
            .ThenBy(item => item.ExternalSettlementId, StringComparer.Ordinal)
            .ThenBy(item => item.BodySha256, StringComparer.Ordinal)
            .ThenBy(item => item.VerifiedAt)
            .ToArray();

        return new ProviderEvidenceInventoryResult(
            Array.AsReadOnly(grouped),
            grouped.Length,
            arrivals.Length,
            history.Length);
    }

    private static ProviderEvidenceOccurrence[] Materialize(
        IEnumerable<VerifiedExternalPaymentEvidence> source,
        ProviderEvidencePopulation population,
        string parameterName)
    {
        var materialized = source.ToArray();
        if (materialized.Any(item => item is null))
        {
            throw new ArgumentException(
                "La evidencia no admite entradas nulas.",
                parameterName);
        }

        return materialized
            .Select(item => new ProviderEvidenceOccurrence(
                population,
                item.Provider,
                item.ProviderEventId,
                item.ExternalSettlementId,
                item.BodySha256,
                item.VerifiedAt.ToUniversalTime()))
            .ToArray();
    }

    private sealed record ProviderEvidenceOccurrence(
        ProviderEvidencePopulation Population,
        string Provider,
        string ProviderEventId,
        string ExternalSettlementId,
        string BodySha256,
        DateTimeOffset VerifiedAt);

    private sealed record EvidenceIdentity(
        ProviderEvidencePopulation Population,
        string Provider,
        string ProviderEventId,
        string ExternalSettlementId,
        string BodySha256,
        DateTimeOffset VerifiedAt);
}
