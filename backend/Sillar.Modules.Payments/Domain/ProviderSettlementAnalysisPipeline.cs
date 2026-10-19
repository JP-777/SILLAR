namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Trazabilidad completa de una identidad entrante a través de P4–P8.
/// </summary>
/// <remarks>
/// La traza conserva observaciones y clasificaciones; no es una recepción
/// durable, una autenticación ni una instrucción para aplicar un pago.
/// </remarks>
public sealed record ProviderSettlementAnalysisTraceItem(
    PaymentIdempotencyKeys Identity,
    int IncomingOccurrences,
    ProviderIntakeDisposition IntakeDisposition,
    ProviderEvidenceReviewDisposition EvidenceDisposition,
    string? AttestationReference,
    IReadOnlyList<ProviderEvidenceObservation> EvidenceObservations,
    IReadOnlyList<ProviderEvidenceConflictKind> ConflictKinds,
    ProviderSettlementRisk SettlementRisk,
    ProviderSettlementReviewLane ReviewLane,
    ProviderSettlementReviewHoldReason HoldReason,
    ProviderReviewAuditSeverity AuditSeverity)
{
    public bool RequiresIndependentAuthentication => true;

    public bool IsHeld => ReviewLane == ProviderSettlementReviewLane.Hold;
}

/// <summary>
/// Resultado integrado de una instantánea pura del análisis M11.
/// </summary>
public sealed record ProviderSettlementAnalysisResult(
    ProviderEvidenceInventoryResult EvidenceInventory,
    ProviderEvidenceReconciliationResult EvidenceReconciliation,
    ProviderEvidenceConflictReportResult ConflictReport,
    ProviderIntakePlan IntakePlan,
    ProviderEvidenceReviewPlan EvidenceReview,
    ProviderSettlementRiskSummary RiskSummary,
    ProviderSettlementReviewPlan ReviewPlan,
    ProviderReviewAuditResult Audit,
    ProviderReviewAuditFingerprint Fingerprint,
    IReadOnlyList<ProviderSettlementAnalysisTraceItem> Trace);

/// <summary>
/// Ejecuta de manera determinista la cadena de análisis ya aprobada sobre una
/// única materialización de entradas y evidencia conocida.
/// </summary>
/// <remarks>
/// Esta canalización no invoca SDK, no verifica firmas, no escribe inbox, no
/// coordina transacciones y no llama a M03. Un candidato continúa requiriendo
/// autenticación y una barrera durable separada antes de cualquier efecto.
/// </remarks>
public static class ProviderSettlementAnalysisPipeline
{
    public static ProviderSettlementAnalysisResult Analyze(
        IEnumerable<VerifiedExternalPaymentEvidence> incoming,
        IEnumerable<VerifiedExternalPaymentEvidence> known,
        IEnumerable<ProviderEvidenceObservation> evidence)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(evidence);

        var arrivals = incoming.ToArray();
        var history = known.ToArray();
        var observations = evidence.ToArray();

        var evidenceInventory = ProviderEvidenceInventory.Create(
            arrivals,
            history);
        var evidenceReconciliation = ProviderEvidenceReconciliation.Create(
            evidenceInventory);
        var conflictReport = ProviderEvidenceConflictReport.Analyze(
            arrivals,
            history);
        if (conflictReport.IncomingOccurrencesExamined !=
                evidenceInventory.IncomingOccurrences
            || conflictReport.KnownOccurrencesExamined !=
                evidenceInventory.KnownOccurrences)
        {
            throw new InvalidOperationException(
                "El inventario y el reporte no examinaron la misma evidencia.");
        }

        var intakePlan = ProviderIntakePlanner.CreatePlan(arrivals, history);
        var evidenceReview = ProviderEvidenceReview.Review(
            intakePlan,
            observations);
        var riskSummary = ProviderSettlementRiskAssessment.Summarize(
            evidenceReview);
        var reviewPlan = ProviderSettlementReviewQueue.Create(riskSummary);
        var crossProviderIncomingSettlements = conflictReport.Cases
            .Where(item => item.Kind == ProviderEvidenceConflictKind
                .CrossProviderSettlementIdentityUnresolved
                && item.Evidence.Any(evidenceItem => evidenceItem.Population ==
                    ProviderEvidencePopulation.Incoming))
            .Select(item => item.IdentityValue)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        reviewPlan = ProviderSettlementReviewQueue.PreserveCrossProviderHolds(
            reviewPlan,
            crossProviderIncomingSettlements);
        var audit = ProviderSettlementReviewAudit.Summarize(reviewPlan);
        var fingerprint = ProviderSettlementReviewFingerprint.Create(audit);
        var trace = BuildTrace(
            intakePlan,
            evidenceReview,
            riskSummary,
            reviewPlan,
            audit,
            conflictReport);

        return new ProviderSettlementAnalysisResult(
            evidenceInventory,
            evidenceReconciliation,
            conflictReport,
            intakePlan,
            evidenceReview,
            riskSummary,
            reviewPlan,
            audit,
            fingerprint,
            trace);
    }

    private static IReadOnlyList<ProviderSettlementAnalysisTraceItem> BuildTrace(
        ProviderIntakePlan intakePlan,
        ProviderEvidenceReviewPlan evidenceReview,
        ProviderSettlementRiskSummary riskSummary,
        ProviderSettlementReviewPlan reviewPlan,
        ProviderReviewAuditResult audit,
        ProviderEvidenceConflictReportResult conflictReport)
    {
        var intakeByIdentity = intakePlan.Items.ToDictionary(
            item => item.Identity);
        var riskBySettlement = riskSummary.Groups.ToDictionary(
            item => new SettlementIdentity(
                item.Provider,
                item.ExternalSettlementId));
        var reviewBySettlement = reviewPlan.Items.ToDictionary(
            item => new SettlementIdentity(
                item.Provider,
                item.ExternalSettlementId));
        var auditBySettlement = audit.Rows.ToDictionary(
            item => new SettlementIdentity(
                item.Provider,
                item.ExternalSettlementId));
        var trace = new List<ProviderSettlementAnalysisTraceItem>(
            evidenceReview.Items.Count);

        foreach (var reviewed in evidenceReview.Items)
        {
            if (!intakeByIdentity.TryGetValue(reviewed.Identity, out var intake))
            {
                throw new InvalidOperationException(
                    "P5 contiene una identidad ausente en P4.");
            }

            var settlementIdentity = new SettlementIdentity(
                reviewed.Identity.Reception.Provider,
                reviewed.Identity.Settlement.Value);

            if (!riskBySettlement.TryGetValue(settlementIdentity, out var risk)
                || !reviewBySettlement.TryGetValue(
                    settlementIdentity,
                    out var review)
                || !auditBySettlement.TryGetValue(
                    settlementIdentity,
                    out var auditRow))
            {
                throw new InvalidOperationException(
                    "Una identidad P5 carece de proyección completa P6–P8.");
            }

            var conflictKinds = GetConflictKinds(
                reviewed.Identity,
                conflictReport);

            if (conflictKinds.Count > 0
                && review.Lane != ProviderSettlementReviewLane.Hold)
            {
                throw new InvalidOperationException(
                    "Una contradicción aplicable a evidencia entrante no quedó en HOLD.");
            }

            if (conflictKinds.Contains(
                    ProviderEvidenceConflictKind.CrossProviderSettlementIdentityUnresolved)
                && review.HoldReason !=
                    ProviderSettlementReviewHoldReason.CrossProviderIdentityUnresolved)
            {
                throw new InvalidOperationException(
                    "Una colisión interproveedor perdió su motivo de HOLD.");
            }

            trace.Add(new ProviderSettlementAnalysisTraceItem(
                reviewed.Identity,
                reviewed.Occurrences,
                intake.Disposition,
                reviewed.Disposition,
                reviewed.AttestationReference,
                reviewed.Observations,
                conflictKinds,
                risk.Risk,
                review.Lane,
                review.HoldReason,
                auditRow.Severity));
        }

        if (trace.Count != intakePlan.Items.Count)
        {
            throw new InvalidOperationException(
                "La traza integrada no cubre todas las identidades P4.");
        }

        var ordered = trace
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

        return Array.AsReadOnly(ordered);
    }

    private static IReadOnlyList<ProviderEvidenceConflictKind> GetConflictKinds(
        PaymentIdempotencyKeys identity,
        ProviderEvidenceConflictReportResult report)
    {
        var kinds = report.Cases
            .Where(conflict => AppliesToIncomingIdentity(conflict, identity))
            .Select(conflict => conflict.Kind)
            .Distinct()
            .OrderBy(kind => kind)
            .ToArray();

        return Array.AsReadOnly(kinds);
    }

    private static bool AppliesToIncomingIdentity(
        ProviderEvidenceConflictCase conflict,
        PaymentIdempotencyKeys identity)
    {
        var isSettlementConflict = conflict.Kind ==
            ProviderEvidenceConflictKind.CrossProviderSettlementIdentityUnresolved;
        var identityMatches = isSettlementConflict
            ? StringComparer.Ordinal.Equals(
                conflict.IdentityValue,
                identity.Settlement.Value)
            : StringComparer.Ordinal.Equals(
                    conflict.Provider,
                    identity.Reception.Provider)
                && StringComparer.Ordinal.Equals(
                    conflict.IdentityValue,
                    identity.Reception.ProviderEventId);

        return identityMatches && conflict.Evidence.Any(item =>
            item.Population == ProviderEvidencePopulation.Incoming
            && StringComparer.Ordinal.Equals(
                item.Provider,
                identity.Reception.Provider)
            && StringComparer.Ordinal.Equals(
                item.ProviderEventId,
                identity.Reception.ProviderEventId)
            && StringComparer.Ordinal.Equals(
                item.ExternalSettlementId,
                identity.Settlement.Value));
    }

    private sealed record SettlementIdentity(
        string Provider,
        string ExternalSettlementId);
}
