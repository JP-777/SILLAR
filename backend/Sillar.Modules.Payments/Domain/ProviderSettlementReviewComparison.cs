namespace Sillar.Modules.Payments.Domain;

/// <summary>Presencia de un expediente entre dos análisis puros.</summary>
public enum ProviderSettlementReviewDeltaKind
{
    Added,
    Removed,
    Changed
}

/// <summary>Dimensiones descriptivas modificadas; no expresa aprobación.</summary>
[Flags]
public enum ProviderSettlementReviewChangeDimension
{
    None = 0,
    Risk = 1,
    ReviewClassification = 2,
    EventIdentities = 4,
    IncomingOccurrences = 8,
    Evidence = 16,
    Conflicts = 32,
    EvidenceInventory = 64
}

/// <summary>Cambio exacto de un expediente proveedor/liquidación.</summary>
public sealed record ProviderSettlementReviewDelta(
    string Provider,
    string ExternalSettlementId,
    ProviderSettlementReviewDeltaKind Kind,
    ProviderSettlementReviewChangeDimension Dimensions,
    ProviderSettlementReviewCase? Before,
    ProviderSettlementReviewCase? After);

/// <summary>Comparación reproducible entre dos instantáneas integradas.</summary>
public sealed record ProviderSettlementReviewComparisonResult(
    string EarlierAnalysisSha256,
    string LaterAnalysisSha256,
    IReadOnlyList<ProviderSettlementReviewDelta> Changes,
    int AddedCases,
    int RemovedCases,
    int ChangedCases)
{
    public bool HasChanges => Changes.Count > 0;
}

/// <summary>
/// Concilia dos análisis sin asignar semántica de avance, aprobación o efecto a
/// los cambios encontrados.
/// </summary>
/// <remarks>
/// La comparación conserva los expedientes completos de ambos lados. Un cambio
/// hacia un carril distinto sigue siendo descriptivo y no autoriza pagos,
/// persistencia, reintentos ni llamadas a M03.
/// </remarks>
public static class ProviderSettlementReviewComparison
{
    public static ProviderSettlementReviewComparisonResult Compare(
        ProviderSettlementAnalysisResult earlier,
        ProviderSettlementAnalysisResult later)
    {
        ArgumentNullException.ThrowIfNull(earlier);
        ArgumentNullException.ThrowIfNull(later);

        var before = ProviderSettlementReviewCasebook.Create(earlier);
        var after = ProviderSettlementReviewCasebook.Create(later);
        var beforeCases = before.Cases.ToDictionary(
            item => new SettlementIdentity(
                item.Provider,
                item.ExternalSettlementId));
        var afterCases = after.Cases.ToDictionary(
            item => new SettlementIdentity(
                item.Provider,
                item.ExternalSettlementId));
        var beforeInventory = InventoryBySettlement(earlier);
        var afterInventory = InventoryBySettlement(later);
        var keys = beforeCases.Keys
            .Concat(afterCases.Keys)
            .Concat(beforeInventory.Keys)
            .Concat(afterInventory.Keys)
            .Distinct()
            .OrderBy(key => key.Provider, StringComparer.Ordinal)
            .ThenBy(key => key.ExternalSettlementId, StringComparer.Ordinal);
        var changes = new List<ProviderSettlementReviewDelta>();

        foreach (var key in keys)
        {
            beforeCases.TryGetValue(key, out var previous);
            afterCases.TryGetValue(key, out var current);
            beforeInventory.TryGetValue(key, out var previousInventory);
            afterInventory.TryGetValue(key, out var currentInventory);
            previousInventory ??= [];
            currentInventory ??= [];
            var inventoryChanged = !previousInventory.SequenceEqual(
                currentInventory);

            if (previous is null && current is null)
            {
                if (inventoryChanged)
                {
                    changes.Add(new ProviderSettlementReviewDelta(
                        key.Provider,
                        key.ExternalSettlementId,
                        ProviderSettlementReviewDeltaKind.Changed,
                        ProviderSettlementReviewChangeDimension.EvidenceInventory,
                        null,
                        null));
                }

                continue;
            }

            if (previous is null)
            {
                changes.Add(new ProviderSettlementReviewDelta(
                    key.Provider,
                    key.ExternalSettlementId,
                    ProviderSettlementReviewDeltaKind.Added,
                    inventoryChanged
                        ? ProviderSettlementReviewChangeDimension.EvidenceInventory
                        : ProviderSettlementReviewChangeDimension.None,
                    null,
                    current));
                continue;
            }

            if (current is null)
            {
                changes.Add(new ProviderSettlementReviewDelta(
                    key.Provider,
                    key.ExternalSettlementId,
                    ProviderSettlementReviewDeltaKind.Removed,
                    inventoryChanged
                        ? ProviderSettlementReviewChangeDimension.EvidenceInventory
                        : ProviderSettlementReviewChangeDimension.None,
                    previous,
                    null));
                continue;
            }

            var dimensions = GetChangedDimensions(previous, current);
            if (inventoryChanged)
            {
                dimensions |=
                    ProviderSettlementReviewChangeDimension.EvidenceInventory;
            }

            if (dimensions != ProviderSettlementReviewChangeDimension.None)
            {
                changes.Add(new ProviderSettlementReviewDelta(
                    key.Provider,
                    key.ExternalSettlementId,
                    ProviderSettlementReviewDeltaKind.Changed,
                    dimensions,
                    previous,
                    current));
            }
        }

        var ordered = changes.ToArray();
        return new ProviderSettlementReviewComparisonResult(
            before.SourceAnalysisSha256,
            after.SourceAnalysisSha256,
            Array.AsReadOnly(ordered),
            ordered.Count(item =>
                item.Kind == ProviderSettlementReviewDeltaKind.Added),
            ordered.Count(item =>
                item.Kind == ProviderSettlementReviewDeltaKind.Removed),
            ordered.Count(item =>
                item.Kind == ProviderSettlementReviewDeltaKind.Changed));
    }

    private static ProviderSettlementReviewChangeDimension GetChangedDimensions(
        ProviderSettlementReviewCase previous,
        ProviderSettlementReviewCase current)
    {
        var dimensions = ProviderSettlementReviewChangeDimension.None;

        if (previous.Risk != current.Risk)
        {
            dimensions |= ProviderSettlementReviewChangeDimension.Risk;
        }

        if (previous.Lane != current.Lane
            || previous.HoldReason != current.HoldReason
            || previous.Severity != current.Severity)
        {
            dimensions |=
                ProviderSettlementReviewChangeDimension.ReviewClassification;
        }

        if (previous.EventIdentities != current.EventIdentities
            || !EventIdentitySequence(previous).SequenceEqual(
                EventIdentitySequence(current)))
        {
            dimensions |=
                ProviderSettlementReviewChangeDimension.EventIdentities;
        }

        if (previous.IncomingOccurrences != current.IncomingOccurrences)
        {
            dimensions |=
                ProviderSettlementReviewChangeDimension.IncomingOccurrences;
        }

        if (!EvidenceSequence(previous).SequenceEqual(EvidenceSequence(current)))
        {
            dimensions |= ProviderSettlementReviewChangeDimension.Evidence;
        }

        if (!previous.ConflictKinds.SequenceEqual(current.ConflictKinds)
            || !ConflictSequence(previous).SequenceEqual(
                ConflictSequence(current)))
        {
            dimensions |= ProviderSettlementReviewChangeDimension.Conflicts;
        }

        return dimensions;
    }

    private static IReadOnlyDictionary<SettlementIdentity, InventorySnapshot[]>
        InventoryBySettlement(ProviderSettlementAnalysisResult analysis)
        => analysis.EvidenceInventory.Items
            .GroupBy(item => new SettlementIdentity(
                item.Provider,
                item.ExternalSettlementId))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => item.Population)
                    .ThenBy(item => item.ProviderEventId, StringComparer.Ordinal)
                    .ThenBy(item => item.BodySha256, StringComparer.Ordinal)
                    .ThenBy(item => item.VerifiedAt.UtcTicks)
                    .Select(item => new InventorySnapshot(
                        item.Population,
                        item.ProviderEventId,
                        item.BodySha256,
                        item.VerifiedAt.UtcTicks,
                        item.Occurrences))
                    .ToArray());

    private static IEnumerable<string> EventIdentitySequence(
        ProviderSettlementReviewCase item)
        => item.Events
            .OrderBy(
                entry => entry.Identity.Reception.ProviderEventId,
                StringComparer.Ordinal)
            .Select(entry => entry.Identity.Reception.ProviderEventId);

    private static IEnumerable<EvidenceSnapshot> EvidenceSequence(
        ProviderSettlementReviewCase item)
        => item.Events
            .OrderBy(
                entry => entry.Identity.Reception.ProviderEventId,
                StringComparer.Ordinal)
            .SelectMany(entry => entry.EvidenceObservations.Count == 0
                ? [new EvidenceSnapshot(
                    entry.Identity.Reception.ProviderEventId,
                    entry.IntakeDisposition,
                    entry.EvidenceDisposition,
                    entry.AttestationReference,
                    null,
                    null)]
                : entry.EvidenceObservations.Select(observation =>
                    new EvidenceSnapshot(
                        entry.Identity.Reception.ProviderEventId,
                        entry.IntakeDisposition,
                        entry.EvidenceDisposition,
                        entry.AttestationReference,
                        observation.Verdict,
                        observation.AttestationReference)));

    private static IEnumerable<ConflictSnapshot> ConflictSequence(
        ProviderSettlementReviewCase item)
        => item.Events
            .OrderBy(
                entry => entry.Identity.Reception.ProviderEventId,
                StringComparer.Ordinal)
            .SelectMany(entry => entry.ConflictKinds.Select(kind =>
                new ConflictSnapshot(
                    entry.Identity.Reception.ProviderEventId,
                    kind)));

    private sealed record SettlementIdentity(
        string Provider,
        string ExternalSettlementId);

    private sealed record EvidenceSnapshot(
        string ProviderEventId,
        ProviderIntakeDisposition IntakeDisposition,
        ProviderEvidenceReviewDisposition EvidenceDisposition,
        string? AttestationReference,
        ProviderEvidenceVerdict? ObservationVerdict,
        string? ObservationAttestationReference);

    private sealed record ConflictSnapshot(
        string ProviderEventId,
        ProviderEvidenceConflictKind Kind);

    private sealed record InventorySnapshot(
        ProviderEvidencePopulation Population,
        string ProviderEventId,
        string BodySha256,
        long VerifiedAtUtcTicks,
        int Occurrences);
}
