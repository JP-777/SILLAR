namespace Sillar.Modules.Payments.Domain;

/// <summary>Carril informativo de revisión para una agrupación P6.</summary>
public enum ProviderSettlementReviewLane
{
    Hold,
    RejectedEvidence,
    AwaitIndependentEvidence,
    PreviouslyKnown,
    RequiresSeparateDurableGate
}

/// <summary>Motivo de retención sin efectos ni permisos implícitos.</summary>
public enum ProviderSettlementReviewHoldReason
{
    None,
    CrossProviderIdentityUnresolved,
    ConflictOrOtherHold,
    UnknownSourceRisk
}

/// <summary>Vista de solo lectura de un grupo P6.</summary>
/// <remarks>
/// <see cref="SourceRisk"/> conserva la clasificación original. Ningún campo
/// autoriza pagos, afirma autenticación o representa recepción durable.
/// </remarks>
public sealed record ProviderSettlementReviewItem(
    string Provider,
    string ExternalSettlementId,
    ProviderSettlementRisk SourceRisk,
    ProviderSettlementReviewLane Lane,
    ProviderSettlementReviewHoldReason HoldReason,
    int EventIdentities,
    long IncomingOccurrences);

/// <summary>Cola pura y determinista; no ejecuta trabajo ni efectos.</summary>
public sealed record ProviderSettlementReviewPlan(
    IReadOnlyList<ProviderSettlementReviewItem> Items,
    int HoldCount,
    int EvidenceAttentionCount,
    int KnownCount,
    int SeparateDurableGateReviewCount,
    IReadOnlyList<string> CrossProviderSettlementIds);

/// <summary>
/// Clasifica el resumen real P6 en carriles de revisión informativos.
/// </summary>
/// <remarks>
/// Toda colisión del mismo identificador de liquidación entre proveedores
/// diferentes queda en HOLD, independientemente del ámbito declarado por el
/// resumen. Un carril de revisión durable separado no habilita persistencia,
/// idempotencia, autenticación ni aplicación de cobros.
/// </remarks>
public static class ProviderSettlementReviewQueue
{
    /// <summary>Crea una cola desde el resumen P6 completo y sin filtrar.</summary>
    public static ProviderSettlementReviewPlan Create(
        ProviderSettlementRiskSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(summary.Groups);

        if (summary.CandidateGroups < 0 || summary.HeldGroups < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(summary),
                "Los contadores P6 no pueden ser negativos.");
        }

        if (!Enum.IsDefined(summary.IdentityScope))
        {
            throw new ArgumentException(
                "El alcance de identidad P6 es desconocido.",
                nameof(summary));
        }

        var groups = summary.Groups.ToArray();

        foreach (var group in groups)
        {
            if (group is null
                || string.IsNullOrWhiteSpace(group.Provider)
                || string.IsNullOrWhiteSpace(group.ExternalSettlementId)
                || group.EventIdentities <= 0
                || group.IncomingOccurrences <= 0)
            {
                throw new ArgumentException(
                    "El resumen P6 contiene una agrupación inválida.",
                    nameof(summary));
            }
        }

        var uniqueGroups = groups
            .Select(group => new ProviderSettlementReviewGroupKey(
                group.Provider,
                group.ExternalSettlementId))
            .Distinct()
            .Count();

        if (uniqueGroups != groups.Length)
        {
            throw new ArgumentException(
                "El resumen P6 repite una identidad de liquidación.",
                nameof(summary));
        }

        var sourceCandidates = groups.Count(group =>
            group.Risk == ProviderSettlementRisk.CandidateForIndependentDurableGate);
        var sourceHeld = groups.Count(group =>
            group.Risk is not ProviderSettlementRisk.PreviouslyKnownOrReceived
                and not ProviderSettlementRisk.CandidateForIndependentDurableGate);

        if (summary.CandidateGroups != sourceCandidates
            || summary.HeldGroups != sourceHeld)
        {
            throw new ArgumentException(
                "Los contadores del resumen no coinciden con sus grupos P6.",
                nameof(summary));
        }

        var crossProviderSettlements = groups
            .GroupBy(
                group => group.ExternalSettlementId,
                StringComparer.Ordinal)
            .Where(group => group
                .Select(item => item.Provider)
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Any())
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var result = groups
            .Select(group => Classify(group, crossProviderSettlements))
            .OrderBy(item => item.Lane)
            .ThenBy(item => item.Provider, StringComparer.Ordinal)
            .ThenBy(item => item.ExternalSettlementId, StringComparer.Ordinal)
            .ToArray();

        return new ProviderSettlementReviewPlan(
            result,
            result.Count(item =>
                item.Lane == ProviderSettlementReviewLane.Hold),
            result.Count(item =>
                item.Lane is ProviderSettlementReviewLane.RejectedEvidence
                    or ProviderSettlementReviewLane.AwaitIndependentEvidence),
            result.Count(item =>
                item.Lane == ProviderSettlementReviewLane.PreviouslyKnown),
            result.Count(item =>
                item.Lane == ProviderSettlementReviewLane.RequiresSeparateDurableGate),
            crossProviderSettlements
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
    }

    /// <summary>
    /// Conserva colisiones interproveedor detectadas contra evidencia histórica
    /// que no aparece como un segundo grupo dentro del resumen P6 entrante.
    /// </summary>
    public static ProviderSettlementReviewPlan PreserveCrossProviderHolds(
        ProviderSettlementReviewPlan plan,
        IEnumerable<string> externalSettlementIds)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(plan.Items);
        ArgumentNullException.ThrowIfNull(plan.CrossProviderSettlementIds);
        ArgumentNullException.ThrowIfNull(externalSettlementIds);

        var items = plan.Items.ToArray();
        var additions = externalSettlementIds.ToArray();
        if (additions.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Una identidad interproveedor no puede estar vacía.",
                nameof(externalSettlementIds));
        }

        var availableSettlements = items
            .Select(item => item.ExternalSettlementId)
            .ToHashSet(StringComparer.Ordinal);
        if (additions.Any(value => !availableSettlements.Contains(value)))
        {
            throw new ArgumentException(
                "Una colisión interproveedor no pertenece al lote entrante.",
                nameof(externalSettlementIds));
        }

        var collisions = plan.CrossProviderSettlementIds
            .Concat(additions)
            .ToHashSet(StringComparer.Ordinal);
        var updated = items
            .Select(item => collisions.Contains(item.ExternalSettlementId)
                ? item with
                {
                    Lane = ProviderSettlementReviewLane.Hold,
                    HoldReason = ProviderSettlementReviewHoldReason
                        .CrossProviderIdentityUnresolved
                }
                : item)
            .OrderBy(item => item.Lane)
            .ThenBy(item => item.Provider, StringComparer.Ordinal)
            .ThenBy(item => item.ExternalSettlementId, StringComparer.Ordinal)
            .ToArray();

        return new ProviderSettlementReviewPlan(
            updated,
            updated.Count(item =>
                item.Lane == ProviderSettlementReviewLane.Hold),
            updated.Count(item => item.Lane is
                ProviderSettlementReviewLane.RejectedEvidence
                    or ProviderSettlementReviewLane.AwaitIndependentEvidence),
            updated.Count(item =>
                item.Lane == ProviderSettlementReviewLane.PreviouslyKnown),
            updated.Count(item => item.Lane ==
                ProviderSettlementReviewLane.RequiresSeparateDurableGate),
            collisions.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static ProviderSettlementReviewItem Classify(
        ProviderSettlementRiskItem group,
        IReadOnlySet<string> crossProviderSettlements)
    {
        var (lane, reason) = ClassifyRisk(group.Risk);

        // Defensa independiente del ámbito informado: hasta que exista autoridad
        // canónica, toda coincidencia interproveedor permanece retenida.
        if (crossProviderSettlements.Contains(group.ExternalSettlementId))
        {
            lane = ProviderSettlementReviewLane.Hold;
            reason = ProviderSettlementReviewHoldReason.CrossProviderIdentityUnresolved;
        }

        return new ProviderSettlementReviewItem(
            group.Provider,
            group.ExternalSettlementId,
            group.Risk,
            lane,
            reason,
            group.EventIdentities,
            group.IncomingOccurrences);
    }

    private static (
        ProviderSettlementReviewLane Lane,
        ProviderSettlementReviewHoldReason Reason)
        ClassifyRisk(ProviderSettlementRisk risk)
        => risk switch
        {
            ProviderSettlementRisk.HoldReplayConflict
                or ProviderSettlementRisk.HoldEvidenceConflict
                or ProviderSettlementRisk.HoldIdentityScopeUnresolved
                or ProviderSettlementRisk.HoldMixedKnownAndCandidate
                or ProviderSettlementRisk.HoldMultipleCandidates
                or ProviderSettlementRisk.HoldOrphanRelatedEvent =>
                (ProviderSettlementReviewLane.Hold,
                    ProviderSettlementReviewHoldReason.ConflictOrOtherHold),
            ProviderSettlementRisk.RejectUntrustedEvidence =>
                (ProviderSettlementReviewLane.RejectedEvidence,
                    ProviderSettlementReviewHoldReason.None),
            ProviderSettlementRisk.AwaitIndependentVerification =>
                (ProviderSettlementReviewLane.AwaitIndependentEvidence,
                    ProviderSettlementReviewHoldReason.None),
            ProviderSettlementRisk.PreviouslyKnownOrReceived =>
                (ProviderSettlementReviewLane.PreviouslyKnown,
                    ProviderSettlementReviewHoldReason.None),
            ProviderSettlementRisk.CandidateForIndependentDurableGate =>
                (ProviderSettlementReviewLane.RequiresSeparateDurableGate,
                    ProviderSettlementReviewHoldReason.None),
            _ => (ProviderSettlementReviewLane.Hold,
                ProviderSettlementReviewHoldReason.UnknownSourceRisk)
        };

    private sealed record ProviderSettlementReviewGroupKey(
        string Provider,
        string ExternalSettlementId);
}
