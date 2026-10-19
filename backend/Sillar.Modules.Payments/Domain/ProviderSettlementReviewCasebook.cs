namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Expediente descriptivo de una liquidación y todos sus eventos entrantes.
/// </summary>
/// <remarks>
/// Un expediente reúne trazabilidad para revisión. Ningún campo autoriza una
/// escritura, acredita idempotencia durable o representa un pago aplicado.
/// </remarks>
public sealed record ProviderSettlementReviewCase(
    string Provider,
    string ExternalSettlementId,
    ProviderSettlementRisk Risk,
    ProviderSettlementReviewLane Lane,
    ProviderSettlementReviewHoldReason HoldReason,
    ProviderReviewAuditSeverity Severity,
    int EventIdentities,
    long IncomingOccurrences,
    IReadOnlyList<ProviderEvidenceConflictKind> ConflictKinds,
    IReadOnlyList<ProviderSettlementAnalysisTraceItem> Events)
{
    public bool RequiresIndependentAuthentication => true;

    public bool IsHeld => Lane == ProviderSettlementReviewLane.Hold;
}

/// <summary>Libro de expedientes puro y reproducible.</summary>
public sealed record ProviderSettlementReviewCasebookResult(
    IReadOnlyList<ProviderSettlementReviewCase> Cases,
    int HeldCases,
    int EvidenceAttentionCases,
    int KnownCases,
    int SeparateDurableReviewCases,
    string SourceAuditSha256,
    string SourceAnalysisSha256);

/// <summary>
/// Construye expedientes completos desde el resultado integrado y comprueba
/// que la traza, los agregados y la huella pertenecen a la misma instantánea.
/// </summary>
public static class ProviderSettlementReviewCasebook
{
    public static ProviderSettlementReviewCasebookResult Create(
        ProviderSettlementAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(analysis.Trace);
        ArgumentNullException.ThrowIfNull(analysis.RiskSummary);
        ArgumentNullException.ThrowIfNull(analysis.RiskSummary.Groups);
        ArgumentNullException.ThrowIfNull(analysis.ReviewPlan);
        ArgumentNullException.ThrowIfNull(analysis.ReviewPlan.Items);
        ArgumentNullException.ThrowIfNull(analysis.Audit);
        ArgumentNullException.ThrowIfNull(analysis.Audit.Rows);
        ArgumentNullException.ThrowIfNull(analysis.Fingerprint);

        var expectedFingerprint =
            ProviderSettlementReviewFingerprint.Create(analysis.Audit);
        if (analysis.Fingerprint != expectedFingerprint)
        {
            throw new ArgumentException(
                "La huella no corresponde a la auditoría integrada.",
                nameof(analysis));
        }
        var analysisFingerprint =
            ProviderSettlementAnalysisIntegrity.Create(analysis);

        var risks = analysis.RiskSummary.Groups.ToDictionary(
            item => new SettlementIdentity(
                item.Provider,
                item.ExternalSettlementId));
        var reviews = analysis.ReviewPlan.Items.ToDictionary(
            item => new SettlementIdentity(
                item.Provider,
                item.ExternalSettlementId));
        var audits = analysis.Audit.Rows.ToDictionary(
            item => new SettlementIdentity(
                item.Provider,
                item.ExternalSettlementId));
        var traceGroups = analysis.Trace
            .GroupBy(item => new SettlementIdentity(
                item.Identity.Reception.Provider,
                item.Identity.Settlement.Value))
            .ToDictionary(group => group.Key, group => group.ToArray());

        if (risks.Count != reviews.Count
            || risks.Count != audits.Count
            || risks.Count != traceGroups.Count
            || risks.Keys.Any(key => !reviews.ContainsKey(key)
                || !audits.ContainsKey(key)
                || !traceGroups.ContainsKey(key)))
        {
            throw new ArgumentException(
                "Las etapas integradas no contienen los mismos grupos.",
                nameof(analysis));
        }

        var cases = new List<ProviderSettlementReviewCase>(risks.Count);

        foreach (var (identity, risk) in risks)
        {
            var review = reviews[identity];
            var audit = audits[identity];
            var events = traceGroups[identity]
                .OrderBy(
                    item => item.Identity.Reception.ProviderEventId,
                    StringComparer.Ordinal)
                .ToArray();
            var eventIdentities = events
                .Select(item => item.Identity.Reception.ProviderEventId)
                .Distinct(StringComparer.Ordinal)
                .Count();
            var occurrences = checked(events.Sum(item =>
                (long)item.IncomingOccurrences));
            var conflictKinds = events
                .SelectMany(item => item.ConflictKinds)
                .Distinct()
                .OrderBy(kind => kind)
                .ToArray();

            if (eventIdentities != risk.EventIdentities
                || occurrences != risk.IncomingOccurrences
                || events.Any(item => item.SettlementRisk != risk.Risk
                    || item.ReviewLane != review.Lane
                    || item.HoldReason != review.HoldReason
                    || item.AuditSeverity != audit.Severity))
            {
                throw new ArgumentException(
                    "La traza no coincide con su agregado P6–P8.",
                    nameof(analysis));
            }

            if (conflictKinds.Length > 0
                && review.Lane != ProviderSettlementReviewLane.Hold)
            {
                throw new ArgumentException(
                    "Un expediente conflictivo no permanece en HOLD.",
                    nameof(analysis));
            }

            if (conflictKinds.Contains(
                    ProviderEvidenceConflictKind.CrossProviderSettlementIdentityUnresolved)
                && review.HoldReason !=
                    ProviderSettlementReviewHoldReason.CrossProviderIdentityUnresolved)
            {
                throw new ArgumentException(
                    "Un expediente interproveedor perdió su HOLD específico.",
                    nameof(analysis));
            }

            cases.Add(new ProviderSettlementReviewCase(
                identity.Provider,
                identity.ExternalSettlementId,
                risk.Risk,
                review.Lane,
                review.HoldReason,
                audit.Severity,
                eventIdentities,
                occurrences,
                Array.AsReadOnly(conflictKinds),
                Array.AsReadOnly(events)));
        }

        var ordered = cases
            .OrderBy(item => item.Provider, StringComparer.Ordinal)
            .ThenBy(item => item.ExternalSettlementId, StringComparer.Ordinal)
            .ToArray();
        var held = ordered.Count(item =>
            item.Lane == ProviderSettlementReviewLane.Hold);
        var evidence = ordered.Count(item => item.Lane is
            ProviderSettlementReviewLane.RejectedEvidence
                or ProviderSettlementReviewLane.AwaitIndependentEvidence);
        var known = ordered.Count(item =>
            item.Lane == ProviderSettlementReviewLane.PreviouslyKnown);
        var separate = ordered.Count(item => item.Lane ==
            ProviderSettlementReviewLane.RequiresSeparateDurableGate);

        if (held != analysis.Audit.HeldGroups
            || evidence != analysis.Audit.EvidenceGroups
            || known != analysis.Audit.KnownGroups
            || separate != analysis.Audit.SeparateDurableReviewGroups
            || held + evidence + known + separate != ordered.Length)
        {
            throw new ArgumentException(
                "Los expedientes no reproducen los totales P8.",
                nameof(analysis));
        }

        return new ProviderSettlementReviewCasebookResult(
            Array.AsReadOnly(ordered),
            held,
            evidence,
            known,
            separate,
            expectedFingerprint.ContentSha256,
            analysisFingerprint.ContentSha256);
    }

    private sealed record SettlementIdentity(
        string Provider,
        string ExternalSettlementId);
}
