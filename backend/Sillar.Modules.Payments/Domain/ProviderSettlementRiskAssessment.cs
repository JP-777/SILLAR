namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Señal normalizada proyectada exhaustivamente desde una disposición P5.
/// </summary>
/// <remarks>
/// Ninguna señal autentica webhooks, persiste inbox, acredita idempotencia
/// durable ni representa un pago aplicado.
/// </remarks>
public enum ProviderRiskSignal
{
    ReplayConflict,
    EvidenceConflict,
    RejectedEvidence,
    UnverifiedEvidence,
    AlreadyReceived,
    KnownSettlement,
    RelatedToCandidate,
    CandidateForDurableReview
}

/// <summary>
/// Alcance de la identidad de liquidación entre proveedores.
/// </summary>
/// <remarks>
/// Mientras no exista autoridad canónica, M11 conserva
/// <see cref="Unresolved"/> y retiene cualquier colisión interproveedor.
/// </remarks>
public enum ProviderSettlementIdentityScope
{
    Unresolved,
    GlobalUnique,
    ScopedByProvider
}

/// <summary>Proyección de un ítem P5, sin efectos ni nuevas decisiones.</summary>
public sealed record ProviderRiskProjection(
    string Provider,
    string ExternalSettlementId,
    string EventId,
    int Occurrences,
    ProviderRiskSignal Signal);

/// <summary>Riesgo agregado de un grupo proveedor/liquidación.</summary>
public enum ProviderSettlementRisk
{
    HoldReplayConflict,
    HoldEvidenceConflict,
    HoldIdentityScopeUnresolved,
    HoldMixedKnownAndCandidate,
    HoldMultipleCandidates,
    HoldOrphanRelatedEvent,
    RejectUntrustedEvidence,
    AwaitIndependentVerification,
    PreviouslyKnownOrReceived,
    CandidateForIndependentDurableGate
}

public sealed record ProviderSettlementRiskItem(
    string Provider,
    string ExternalSettlementId,
    int EventIdentities,
    long IncomingOccurrences,
    ProviderSettlementRisk Risk);

public sealed record ProviderSettlementRiskSummary(
    ProviderSettlementIdentityScope IdentityScope,
    IReadOnlyList<ProviderSettlementRiskItem> Groups,
    int CandidateGroups,
    int HeldGroups);

/// <summary>
/// Agrupa riesgos desde el plan real P5 sin alterar sus contratos.
/// </summary>
/// <remarks>
/// La evaluación es pura y mantiene en HOLD los conflictos de replay, la
/// evidencia contradictoria y las colisiones interproveedor de alcance incierto.
/// Un candidato sólo puede avanzar a otra revisión independiente futura.
/// </remarks>
public static class ProviderSettlementRiskAssessment
{
    /// <summary>
    /// Alcance vigente. No puede ampliarse por parámetro mientras falte una
    /// autoridad canónica sobre identidades interproveedor.
    /// </summary>
    public static ProviderSettlementIdentityScope IdentityScope
        => ProviderSettlementIdentityScope.Unresolved;

    /// <summary>Resume los riesgos de un plan P5 ya construido.</summary>
    public static ProviderSettlementRiskSummary Summarize(
        ProviderEvidenceReviewPlan reviewed)
    {
        ArgumentNullException.ThrowIfNull(reviewed);
        ArgumentNullException.ThrowIfNull(reviewed.Items);

        var entries = reviewed.Items
            .Select(Project)
            .ToArray();
        var collisions = entries
            .GroupBy(
                entry => entry.ExternalSettlementId,
                StringComparer.Ordinal)
            .Where(group => group
                .Select(entry => entry.Provider)
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Any())
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var groups = entries
            .GroupBy(entry => new ProviderSettlementGroup(
                entry.Provider,
                entry.ExternalSettlementId))
            .Select(group => AssessGroup(group, collisions))
            .OrderBy(item => item.Provider, StringComparer.Ordinal)
            .ThenBy(item => item.ExternalSettlementId, StringComparer.Ordinal)
            .ToArray();

        return new ProviderSettlementRiskSummary(
            IdentityScope,
            groups,
            groups.Count(item =>
                item.Risk == ProviderSettlementRisk.CandidateForIndependentDurableGate),
            groups.Count(item =>
                item.Risk is not ProviderSettlementRisk.PreviouslyKnownOrReceived
                    and not ProviderSettlementRisk.CandidateForIndependentDurableGate));
    }

    private static ProviderRiskProjection Project(
        ProviderEvidenceReviewItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(item.Identity);
        ArgumentNullException.ThrowIfNull(item.Identity.Reception);
        ArgumentNullException.ThrowIfNull(item.Identity.Settlement);

        if (item.Occurrences <= 0
            || string.IsNullOrWhiteSpace(item.Identity.Reception.Provider)
            || string.IsNullOrWhiteSpace(item.Identity.Reception.ProviderEventId)
            || string.IsNullOrWhiteSpace(item.Identity.Settlement.Value)
            || !Enum.IsDefined(item.Disposition))
        {
            throw new ArgumentException(
                "El plan P5 contiene una identidad o disposición inválida.",
                nameof(item));
        }

        var signal = item.Disposition switch
        {
            ProviderEvidenceReviewDisposition.HoldReplayConflict =>
                ProviderRiskSignal.ReplayConflict,
            ProviderEvidenceReviewDisposition.HoldInconsistentEvidence =>
                ProviderRiskSignal.EvidenceConflict,
            ProviderEvidenceReviewDisposition.RejectUntrustedEvidence =>
                ProviderRiskSignal.RejectedEvidence,
            ProviderEvidenceReviewDisposition.AwaitIndependentVerification =>
                ProviderRiskSignal.UnverifiedEvidence,
            ProviderEvidenceReviewDisposition.AlreadyReceivedAttested =>
                ProviderRiskSignal.AlreadyReceived,
            ProviderEvidenceReviewDisposition.KnownSettlementAttested =>
                ProviderRiskSignal.KnownSettlement,
            ProviderEvidenceReviewDisposition.RelatedToCandidateAttested =>
                ProviderRiskSignal.RelatedToCandidate,
            ProviderEvidenceReviewDisposition.CandidateForDurableInboxReview =>
                ProviderRiskSignal.CandidateForDurableReview,
            _ => throw new ArgumentOutOfRangeException(
                nameof(item),
                item.Disposition,
                "La disposición P5 no tiene una proyección P6.")
        };

        return new ProviderRiskProjection(
            item.Identity.Reception.Provider,
            item.Identity.Settlement.Value,
            item.Identity.Reception.ProviderEventId,
            item.Occurrences,
            signal);
    }

    private static ProviderSettlementRiskItem AssessGroup(
        IGrouping<ProviderSettlementGroup, ProviderRiskProjection> group,
        IReadOnlySet<string> collisions)
    {
        var items = group.ToArray();
        var candidateCount = items.Count(item =>
            item.Signal == ProviderRiskSignal.CandidateForDurableReview);
        var hasKnown = items.Any(item =>
            item.Signal is ProviderRiskSignal.AlreadyReceived
                or ProviderRiskSignal.KnownSettlement);
        var hasRelated = items.Any(item =>
            item.Signal == ProviderRiskSignal.RelatedToCandidate);
        var hasReplayConflict = items.Any(item =>
            item.Signal == ProviderRiskSignal.ReplayConflict);
        var hasEvidenceConflict = items.Any(item =>
            item.Signal == ProviderRiskSignal.EvidenceConflict);
        var contradictoryEvent = items
            .GroupBy(item => item.EventId, StringComparer.Ordinal)
            .Any(eventGroup => eventGroup
                .Select(item => item.Signal)
                .Distinct()
                .Skip(1)
                .Any());
        var crossProviderCollision = collisions.Contains(
            group.Key.ExternalSettlementId);

        ProviderSettlementRisk risk;

        if (hasReplayConflict)
        {
            risk = ProviderSettlementRisk.HoldReplayConflict;
        }
        else if (hasEvidenceConflict || contradictoryEvent)
        {
            risk = ProviderSettlementRisk.HoldEvidenceConflict;
        }
        else if (crossProviderCollision
            && IdentityScope == ProviderSettlementIdentityScope.Unresolved)
        {
            risk = ProviderSettlementRisk.HoldIdentityScopeUnresolved;
        }
        else if (items.Any(item =>
            item.Signal == ProviderRiskSignal.RejectedEvidence))
        {
            risk = ProviderSettlementRisk.RejectUntrustedEvidence;
        }
        else if (items.Any(item =>
            item.Signal == ProviderRiskSignal.UnverifiedEvidence))
        {
            risk = ProviderSettlementRisk.AwaitIndependentVerification;
        }
        else if (candidateCount > 1)
        {
            risk = ProviderSettlementRisk.HoldMultipleCandidates;
        }
        else if (candidateCount > 0 && hasKnown)
        {
            risk = ProviderSettlementRisk.HoldMixedKnownAndCandidate;
        }
        else if (candidateCount == 0 && hasRelated)
        {
            risk = ProviderSettlementRisk.HoldOrphanRelatedEvent;
        }
        else if (candidateCount == 1)
        {
            risk = ProviderSettlementRisk.CandidateForIndependentDurableGate;
        }
        else
        {
            risk = ProviderSettlementRisk.PreviouslyKnownOrReceived;
        }

        return new ProviderSettlementRiskItem(
            group.Key.Provider,
            group.Key.ExternalSettlementId,
            items
                .Select(item => item.EventId)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            items.Sum(item => (long)item.Occurrences),
            risk);
    }

    private sealed record ProviderSettlementGroup(
        string Provider,
        string ExternalSettlementId);
}
