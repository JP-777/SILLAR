namespace Sillar.Modules.Payments.Domain;

/// <summary>Inconsistencias observables en una identidad de recepción.</summary>
[Flags]
public enum ProviderReceptionEvidenceIssue
{
    None = 0,
    MultipleSettlements = 1,
    ContradictoryBodies = 2
}

/// <summary>Resumen reconciliado de una pareja proveedor/evento.</summary>
public sealed record ProviderReceptionEvidenceSummary(
    string Provider,
    string ProviderEventId,
    int DistinctSettlements,
    int DistinctBodyHashes,
    int IncomingOccurrences,
    int KnownOccurrences,
    ProviderReceptionEvidenceIssue Issues);

/// <summary>Alcance observado para una identidad de liquidación.</summary>
public enum ProviderSettlementEvidenceScope
{
    SingleProvider,
    CrossProviderIdentityUnresolved
}

/// <summary>Resumen reconciliado de una liquidación externa.</summary>
public sealed record ProviderSettlementEvidenceSummary(
    string ExternalSettlementId,
    int DistinctProviders,
    int DistinctProviderEvents,
    int DistinctBodyHashes,
    int IncomingOccurrences,
    int KnownOccurrences,
    ProviderSettlementEvidenceScope Scope);

/// <summary>Índices completos para conciliar evidencia P1.</summary>
public sealed record ProviderEvidenceReconciliationResult(
    IReadOnlyList<ProviderReceptionEvidenceSummary> Receptions,
    IReadOnlyList<ProviderSettlementEvidenceSummary> Settlements,
    int ReceptionConflicts,
    int CrossProviderHolds);

/// <summary>
/// Reconcilia el inventario completo por recepción y por liquidación, incluidas
/// las identidades que no tienen conflicto.
/// </summary>
/// <remarks>
/// El alcance interproveedor permanece no resuelto y se informa como HOLD. Los
/// resúmenes no autentican evidencia ni habilitan efectos posteriores.
/// </remarks>
public static class ProviderEvidenceReconciliation
{
    public static ProviderEvidenceReconciliationResult Create(
        ProviderEvidenceInventoryResult inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(inventory.Items);
        var items = inventory.Items.ToArray();

        ValidateInventory(inventory, items);

        var receptions = items
            .GroupBy(item => new ReceptionIdentity(
                item.Provider,
                item.ProviderEventId))
            .Select(group =>
            {
                var settlements = group
                    .Select(item => item.ExternalSettlementId)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                var bodies = group
                    .Select(item => item.BodySha256)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                var issues = ProviderReceptionEvidenceIssue.None;

                if (settlements > 1)
                {
                    issues |= ProviderReceptionEvidenceIssue.MultipleSettlements;
                }

                if (bodies > 1)
                {
                    issues |= ProviderReceptionEvidenceIssue.ContradictoryBodies;
                }

                return new ProviderReceptionEvidenceSummary(
                    group.Key.Provider,
                    group.Key.ProviderEventId,
                    settlements,
                    bodies,
                    Count(group, ProviderEvidencePopulation.Incoming),
                    Count(group, ProviderEvidencePopulation.PreviouslyKnown),
                    issues);
            })
            .OrderBy(item => item.Provider, StringComparer.Ordinal)
            .ThenBy(item => item.ProviderEventId, StringComparer.Ordinal)
            .ToArray();

        var settlements = items
            .GroupBy(
                item => item.ExternalSettlementId,
                StringComparer.Ordinal)
            .Select(group =>
            {
                var providers = group
                    .Select(item => item.Provider)
                    .Distinct(StringComparer.Ordinal)
                    .Count();

                return new ProviderSettlementEvidenceSummary(
                    group.Key,
                    providers,
                    group
                        .Select(item => new ReceptionIdentity(
                            item.Provider,
                            item.ProviderEventId))
                        .Distinct()
                        .Count(),
                    group
                        .Select(item => item.BodySha256)
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                    Count(group, ProviderEvidencePopulation.Incoming),
                    Count(group, ProviderEvidencePopulation.PreviouslyKnown),
                    providers > 1
                        ? ProviderSettlementEvidenceScope
                            .CrossProviderIdentityUnresolved
                        : ProviderSettlementEvidenceScope.SingleProvider);
            })
            .OrderBy(item => item.ExternalSettlementId, StringComparer.Ordinal)
            .ToArray();

        return new ProviderEvidenceReconciliationResult(
            Array.AsReadOnly(receptions),
            Array.AsReadOnly(settlements),
            receptions.Count(item =>
                item.Issues != ProviderReceptionEvidenceIssue.None),
            settlements.Count(item => item.Scope ==
                ProviderSettlementEvidenceScope.CrossProviderIdentityUnresolved));
    }

    private static void ValidateInventory(
        ProviderEvidenceInventoryResult inventory,
        IReadOnlyCollection<ProviderEvidenceInventoryItem> items)
    {
        if (items.Any(item => item is null
                || !Enum.IsDefined(item.Population)
                || string.IsNullOrWhiteSpace(item.Provider)
                || string.IsNullOrWhiteSpace(item.ProviderEventId)
                || string.IsNullOrWhiteSpace(item.ExternalSettlementId)
                || string.IsNullOrWhiteSpace(item.BodySha256)
                || item.BodySha256.Length != 64
                || !item.BodySha256.All(char.IsAsciiHexDigit)
                || item.VerifiedAt == default
                || item.Occurrences <= 0)
            || items
                .Select(item => new EvidenceIdentity(
                    item.Population,
                    item.Provider,
                    item.ProviderEventId,
                    item.ExternalSettlementId,
                    item.BodySha256,
                    item.VerifiedAt.UtcTicks))
                .Distinct()
                .Count() != items.Count
            || inventory.DistinctEvidenceItems != items.Count
            || inventory.IncomingOccurrences !=
                Count(items, ProviderEvidencePopulation.Incoming)
            || inventory.KnownOccurrences !=
                Count(items, ProviderEvidencePopulation.PreviouslyKnown))
        {
            throw new ArgumentException(
                "El inventario no es coherente para conciliación.",
                nameof(inventory));
        }
    }

    private static int Count(
        IEnumerable<ProviderEvidenceInventoryItem> items,
        ProviderEvidencePopulation population)
        => checked(items
            .Where(item => item.Population == population)
            .Sum(item => item.Occurrences));

    private sealed record ReceptionIdentity(
        string Provider,
        string ProviderEventId);

    private sealed record EvidenceIdentity(
        ProviderEvidencePopulation Population,
        string Provider,
        string ProviderEventId,
        string ExternalSettlementId,
        string BodySha256,
        long VerifiedAtUtcTicks);
}
