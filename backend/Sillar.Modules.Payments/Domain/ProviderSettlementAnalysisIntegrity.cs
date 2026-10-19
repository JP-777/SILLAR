using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Huella del inventario, conflictos, evidencia revisada y clasificación final
/// de una instantánea integrada.
/// </summary>
/// <remarks>
/// Es un identificador reproducible de contenido. No sustituye una firma, una
/// autenticación, almacenamiento durable ni el SHA-256 del cuerpo del webhook.
/// </remarks>
public sealed record ProviderSettlementAnalysisFingerprint(
    string Format,
    string ContentSha256,
    string ReviewAuditSha256,
    int DistinctEvidenceItems,
    int TraceItems,
    int ConflictCases);

/// <summary>
/// Verifica la cobertura del inventario y calcula una huella canónica de toda
/// la información preservada por la canalización P11.
/// </summary>
public static class ProviderSettlementAnalysisIntegrity
{
    public const string CurrentFormat = "m11-provider-settlement-analysis/v1";

    public static ProviderSettlementAnalysisFingerprint Create(
        ProviderSettlementAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(analysis.EvidenceInventory);
        ArgumentNullException.ThrowIfNull(analysis.EvidenceInventory.Items);
        ArgumentNullException.ThrowIfNull(analysis.EvidenceReconciliation);
        ArgumentNullException.ThrowIfNull(
            analysis.EvidenceReconciliation.Receptions);
        ArgumentNullException.ThrowIfNull(
            analysis.EvidenceReconciliation.Settlements);
        ArgumentNullException.ThrowIfNull(analysis.ConflictReport);
        ArgumentNullException.ThrowIfNull(analysis.ConflictReport.Cases);
        ArgumentNullException.ThrowIfNull(analysis.Trace);
        ArgumentNullException.ThrowIfNull(analysis.Audit);
        ArgumentNullException.ThrowIfNull(analysis.Fingerprint);

        var reviewFingerprint =
            ProviderSettlementReviewFingerprint.Create(analysis.Audit);
        if (reviewFingerprint != analysis.Fingerprint)
        {
            throw new ArgumentException(
                "La huella P8 no corresponde al análisis integrado.",
                nameof(analysis));
        }

        var inventory = ValidateInventory(analysis.EvidenceInventory);
        ValidateReconciliation(
            analysis.EvidenceInventory,
            analysis.EvidenceReconciliation);
        var trace = ValidateTraceCoverage(analysis.Trace, inventory);
        var conflicts = ValidateConflictCoverage(
            analysis.ConflictReport,
            inventory,
            analysis.EvidenceReconciliation);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, CurrentFormat);
        AppendString(hash, reviewFingerprint.ContentSha256);
        AppendInventory(hash, inventory);
        AppendConflicts(hash, conflicts);
        AppendTrace(hash, trace);

        return new ProviderSettlementAnalysisFingerprint(
            CurrentFormat,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            reviewFingerprint.ContentSha256,
            inventory.Length,
            trace.Length,
            conflicts.Length);
    }

    private static void ValidateReconciliation(
        ProviderEvidenceInventoryResult inventory,
        ProviderEvidenceReconciliationResult supplied)
    {
        var expected = ProviderEvidenceReconciliation.Create(inventory);
        if (supplied.ReceptionConflicts != expected.ReceptionConflicts
            || supplied.CrossProviderHolds != expected.CrossProviderHolds
            || !supplied.Receptions.SequenceEqual(expected.Receptions)
            || !supplied.Settlements.SequenceEqual(expected.Settlements))
        {
            throw new ArgumentException(
                "La conciliación no corresponde al inventario integrado.",
                nameof(supplied));
        }
    }

    private static ProviderEvidenceInventoryItem[] ValidateInventory(
        ProviderEvidenceInventoryResult inventory)
    {
        var items = inventory.Items.ToArray();
        if (items.Any(item => item is null
                || !Enum.IsDefined(item.Population)
                || string.IsNullOrWhiteSpace(item.Provider)
                || string.IsNullOrWhiteSpace(item.ProviderEventId)
                || string.IsNullOrWhiteSpace(item.ExternalSettlementId)
                || !IsSha256(item.BodySha256)
                || item.VerifiedAt == default
                || item.Occurrences <= 0))
        {
            throw new ArgumentException(
                "El inventario contiene evidencia inválida.",
                nameof(inventory));
        }

        var ordered = items
            .OrderBy(item => item.Population)
            .ThenBy(item => item.Provider, StringComparer.Ordinal)
            .ThenBy(item => item.ProviderEventId, StringComparer.Ordinal)
            .ThenBy(item => item.ExternalSettlementId, StringComparer.Ordinal)
            .ThenBy(item => item.BodySha256, StringComparer.Ordinal)
            .ThenBy(item => item.VerifiedAt.UtcTicks)
            .ToArray();
        var unique = ordered
            .Select(item => new InventoryIdentity(
                item.Population,
                item.Provider,
                item.ProviderEventId,
                item.ExternalSettlementId,
                item.BodySha256,
                item.VerifiedAt.UtcTicks))
            .Distinct()
            .Count();
        var incoming = checked(ordered
            .Where(item => item.Population == ProviderEvidencePopulation.Incoming)
            .Sum(item => item.Occurrences));
        var known = checked(ordered
            .Where(item => item.Population ==
                ProviderEvidencePopulation.PreviouslyKnown)
            .Sum(item => item.Occurrences));

        if (unique != ordered.Length
            || inventory.DistinctEvidenceItems != ordered.Length
            || inventory.IncomingOccurrences != incoming
            || inventory.KnownOccurrences != known)
        {
            throw new ArgumentException(
                "Los contadores del inventario no coinciden con su contenido.",
                nameof(inventory));
        }

        return ordered;
    }

    private static ProviderSettlementAnalysisTraceItem[] ValidateTraceCoverage(
        IReadOnlyList<ProviderSettlementAnalysisTraceItem> source,
        IReadOnlyCollection<ProviderEvidenceInventoryItem> inventory)
    {
        var trace = source.ToArray();
        if (trace.Any(item => item is null
                || item.Identity is null
                || item.Identity.Reception is null
                || item.Identity.Settlement is null
                || item.IncomingOccurrences <= 0
                || item.EvidenceObservations is null
                || item.ConflictKinds is null))
        {
            throw new ArgumentException(
                "La traza integrada contiene elementos inválidos.",
                nameof(source));
        }

        var incoming = inventory
            .Where(item =>
                item.Population == ProviderEvidencePopulation.Incoming)
            .GroupBy(item => new EventSettlementIdentity(
                item.Provider,
                item.ProviderEventId,
                item.ExternalSettlementId))
            .ToDictionary(
                group => group.Key,
                group => checked(group.Sum(item => item.Occurrences)));
        var traced = trace.ToDictionary(
            item => new EventSettlementIdentity(
                item.Identity.Reception.Provider,
                item.Identity.Reception.ProviderEventId,
                item.Identity.Settlement.Value));

        if (incoming.Count != traced.Count
            || incoming.Any(pair => !traced.TryGetValue(pair.Key, out var item)
                || item.IncomingOccurrences != pair.Value))
        {
            throw new ArgumentException(
                "La traza no cubre exactamente la evidencia entrante.",
                nameof(source));
        }

        foreach (var item in trace)
        {
            if (item.EvidenceObservations.Any(observation =>
                    observation is null
                    || observation.Identity != item.Identity)
                || item.ConflictKinds.Any(kind => !Enum.IsDefined(kind)))
            {
                throw new ArgumentException(
                    "La traza contiene evidencia o conflictos ajenos a su identidad.",
                    nameof(source));
            }
        }

        return trace
            .OrderBy(item => item.Identity.Reception.Provider,
                StringComparer.Ordinal)
            .ThenBy(item => item.Identity.Reception.ProviderEventId,
                StringComparer.Ordinal)
            .ThenBy(item => item.Identity.Settlement.Value,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static ProviderEvidenceConflictCase[] ValidateConflictCoverage(
        ProviderEvidenceConflictReportResult report,
        IReadOnlyCollection<ProviderEvidenceInventoryItem> inventory,
        ProviderEvidenceReconciliationResult reconciliation)
    {
        if (report.IncomingOccurrencesExamined !=
                inventory.Where(item => item.Population ==
                    ProviderEvidencePopulation.Incoming)
                    .Sum(item => item.Occurrences)
            || report.KnownOccurrencesExamined !=
                inventory.Where(item => item.Population ==
                    ProviderEvidencePopulation.PreviouslyKnown)
                    .Sum(item => item.Occurrences))
        {
            throw new ArgumentException(
                "El reporte de conflictos no cubre el inventario completo.",
                nameof(report));
        }

        var inventoryCounts = inventory.ToDictionary(
            item => new InventoryIdentity(
                item.Population,
                item.Provider,
                item.ProviderEventId,
                item.ExternalSettlementId,
                item.BodySha256,
                item.VerifiedAt.UtcTicks),
            item => item.Occurrences);
        var cases = report.Cases.ToArray();

        if (cases.Any(item => item is null
                || !Enum.IsDefined(item.Kind)
                || string.IsNullOrWhiteSpace(item.IdentityValue)
                || item.Evidence is null
                || item.Evidence.Count == 0
                || item.Evidence.Any(evidence => evidence is null
                    || !Enum.IsDefined(evidence.Population)
                    || string.IsNullOrWhiteSpace(evidence.Provider)
                    || string.IsNullOrWhiteSpace(evidence.ProviderEventId)
                    || string.IsNullOrWhiteSpace(evidence.ExternalSettlementId)
                    || !IsSha256(evidence.BodySha256)
                    || evidence.VerifiedAt == default)))
        {
            throw new ArgumentException(
                "El reporte contiene un conflicto inválido.",
                nameof(report));
        }

        var expectedCases = reconciliation.Receptions
            .SelectMany(item => ExpectedReceptionConflicts(item))
            .Concat(reconciliation.Settlements
                .Where(item => item.Scope ==
                    ProviderSettlementEvidenceScope.CrossProviderIdentityUnresolved)
                .Select(item => new ConflictIdentity(
                    ProviderEvidenceConflictKind
                        .CrossProviderSettlementIdentityUnresolved,
                    null,
                    item.ExternalSettlementId)))
            .ToHashSet();
        var suppliedCases = cases
            .Select(item => new ConflictIdentity(
                item.Kind,
                item.Provider,
                item.IdentityValue))
            .ToArray();

        if (suppliedCases.Distinct().Count() != suppliedCases.Length
            || !expectedCases.SetEquals(suppliedCases))
        {
            throw new ArgumentException(
                "El reporte no representa exhaustivamente la conciliación.",
                nameof(report));
        }

        foreach (var conflict in cases)
        {
            var used = conflict.Evidence
                .GroupBy(item => new InventoryIdentity(
                    item.Population,
                    item.Provider,
                    item.ProviderEventId,
                    item.ExternalSettlementId,
                    item.BodySha256,
                    item.VerifiedAt.UtcTicks))
                .ToDictionary(group => group.Key, group => group.Count());
            var expectedEvidence = inventory
                .Where(item => ConflictIncludes(conflict, item))
                .ToDictionary(
                    item => new InventoryIdentity(
                        item.Population,
                        item.Provider,
                        item.ProviderEventId,
                        item.ExternalSettlementId,
                        item.BodySha256,
                        item.VerifiedAt.UtcTicks),
                    item => item.Occurrences);

            if (used.Count != expectedEvidence.Count
                || used.Any(pair =>
                    !inventoryCounts.ContainsKey(pair.Key)
                    || !expectedEvidence.TryGetValue(
                        pair.Key,
                        out var expectedOccurrences)
                    || pair.Value != expectedOccurrences))
            {
                throw new ArgumentException(
                    "Un conflicto no conserva toda su evidencia del inventario.",
                    nameof(report));
            }
        }

        return cases
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Provider ?? string.Empty,
                StringComparer.Ordinal)
            .ThenBy(item => item.IdentityValue, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ConflictIncludes(
        ProviderEvidenceConflictCase conflict,
        ProviderEvidenceInventoryItem evidence)
        => conflict.Kind ==
            ProviderEvidenceConflictKind.CrossProviderSettlementIdentityUnresolved
            ? StringComparer.Ordinal.Equals(
                evidence.ExternalSettlementId,
                conflict.IdentityValue)
            : StringComparer.Ordinal.Equals(evidence.Provider, conflict.Provider)
                && StringComparer.Ordinal.Equals(
                    evidence.ProviderEventId,
                    conflict.IdentityValue);

    private static IEnumerable<ConflictIdentity> ExpectedReceptionConflicts(
        ProviderReceptionEvidenceSummary reception)
    {
        if (reception.Issues.HasFlag(
            ProviderReceptionEvidenceIssue.MultipleSettlements))
        {
            yield return new ConflictIdentity(
                ProviderEvidenceConflictKind.ReceptionMapsToMultipleSettlements,
                reception.Provider,
                reception.ProviderEventId);
        }

        if (reception.Issues.HasFlag(
            ProviderReceptionEvidenceIssue.ContradictoryBodies))
        {
            yield return new ConflictIdentity(
                ProviderEvidenceConflictKind.ReceptionHasContradictoryBodies,
                reception.Provider,
                reception.ProviderEventId);
        }
    }

    private static void AppendInventory(
        IncrementalHash hash,
        IReadOnlyCollection<ProviderEvidenceInventoryItem> inventory)
    {
        AppendInt32(hash, inventory.Count);
        foreach (var item in inventory)
        {
            AppendInt32(hash, (int)item.Population);
            AppendString(hash, item.Provider);
            AppendString(hash, item.ProviderEventId);
            AppendString(hash, item.ExternalSettlementId);
            AppendString(hash, item.BodySha256);
            AppendInt64(hash, item.VerifiedAt.UtcTicks);
            AppendInt32(hash, item.Occurrences);
        }
    }

    private static void AppendConflicts(
        IncrementalHash hash,
        IReadOnlyCollection<ProviderEvidenceConflictCase> conflicts)
    {
        AppendInt32(hash, conflicts.Count);
        foreach (var conflict in conflicts)
        {
            AppendInt32(hash, (int)conflict.Kind);
            AppendNullableString(hash, conflict.Provider);
            AppendString(hash, conflict.IdentityValue);
            var evidence = conflict.Evidence
                .OrderBy(item => item.Population)
                .ThenBy(item => item.Provider, StringComparer.Ordinal)
                .ThenBy(item => item.ProviderEventId, StringComparer.Ordinal)
                .ThenBy(item => item.ExternalSettlementId, StringComparer.Ordinal)
                .ThenBy(item => item.BodySha256, StringComparer.Ordinal)
                .ThenBy(item => item.VerifiedAt.UtcTicks)
                .ToArray();
            AppendInt32(hash, evidence.Length);

            foreach (var item in evidence)
            {
                AppendInt32(hash, (int)item.Population);
                AppendString(hash, item.Provider);
                AppendString(hash, item.ProviderEventId);
                AppendString(hash, item.ExternalSettlementId);
                AppendString(hash, item.BodySha256);
                AppendInt64(hash, item.VerifiedAt.UtcTicks);
            }
        }
    }

    private static void AppendTrace(
        IncrementalHash hash,
        IReadOnlyCollection<ProviderSettlementAnalysisTraceItem> trace)
    {
        AppendInt32(hash, trace.Count);
        foreach (var item in trace)
        {
            AppendString(hash, item.Identity.Reception.Provider);
            AppendString(hash, item.Identity.Reception.ProviderEventId);
            AppendString(hash, item.Identity.Settlement.Value);
            AppendInt32(hash, item.IncomingOccurrences);
            AppendInt32(hash, (int)item.IntakeDisposition);
            AppendInt32(hash, (int)item.EvidenceDisposition);
            AppendNullableString(hash, item.AttestationReference);
            AppendInt32(hash, (int)item.SettlementRisk);
            AppendInt32(hash, (int)item.ReviewLane);
            AppendInt32(hash, (int)item.HoldReason);
            AppendInt32(hash, (int)item.AuditSeverity);

            var observations = item.EvidenceObservations
                .OrderBy(observation => observation.Verdict)
                .ThenBy(observation =>
                    observation.AttestationReference ?? string.Empty,
                    StringComparer.Ordinal)
                .ToArray();
            AppendInt32(hash, observations.Length);
            foreach (var observation in observations)
            {
                AppendInt32(hash, (int)observation.Verdict);
                AppendNullableString(hash, observation.AttestationReference);
            }

            var conflictKinds = item.ConflictKinds.OrderBy(kind => kind).ToArray();
            AppendInt32(hash, conflictKinds.Length);
            foreach (var kind in conflictKinds)
            {
                AppendInt32(hash, (int)kind);
            }
        }
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    private static void AppendNullableString(
        IncrementalHash hash,
        string? value)
    {
        if (value is null)
        {
            AppendInt32(hash, -1);
            return;
        }

        AppendString(hash, value);
    }

    private static void AppendString(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private sealed record EventSettlementIdentity(
        string Provider,
        string ProviderEventId,
        string ExternalSettlementId);

    private sealed record ConflictIdentity(
        ProviderEvidenceConflictKind Kind,
        string? Provider,
        string IdentityValue);

    private sealed record InventoryIdentity(
        ProviderEvidencePopulation Population,
        string Provider,
        string ProviderEventId,
        string ExternalSettlementId,
        string BodySha256,
        long VerifiedAtUtcTicks);
}
