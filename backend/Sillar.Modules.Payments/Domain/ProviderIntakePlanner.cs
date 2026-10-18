namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Recomendación semántica de recepción, sin efectos ni garantías durables.
/// </summary>
public enum ProviderIntakeDisposition
{
    HoldForConflict,
    AlreadyReceived,
    RelatesToKnownSettlement,
    CandidateRequiringIndependentAuthentication,
    RelatesToCandidateInBatch
}

/// <summary>Una identidad única y su disposición informativa.</summary>
/// <remarks>
/// Todas las disposiciones requieren autenticación independiente antes de un
/// futuro efecto. Ni siquiera un duplicado se considera autenticado por haber
/// sido clasificado.
/// </remarks>
public sealed record ProviderIntakePlanItem(
    PaymentIdempotencyKeys Identity,
    int Occurrences,
    ProviderIntakeDisposition Disposition)
{
    /// <summary>
    /// La planificación nunca sustituye la autenticación exigida antes de
    /// cualquier escritura o efecto futuro.
    /// </summary>
    public bool RequiresIndependentAuthentication => true;
}

/// <summary>Plan puro; no representa inbox, autenticación ni pago aplicado.</summary>
public sealed record ProviderIntakePlan(
    IReadOnlyList<ProviderIntakePlanItem> Items,
    bool HasConflicts,
    int CandidatesAwaitingAuthentication);

/// <summary>
/// Reduce un lote clasificado por P2/P3 a candidatos deterministas por proveedor
/// y liquidación.
/// </summary>
/// <remarks>
/// Consume los contratos normalizados de P2/P3, pero no autentica webhooks ni
/// convierte su clasificación en autorización. Tampoco escribe, reserva,
/// deduplica persistentemente ni afirma que M03 haya aplicado un pago.
/// </remarks>
public static class ProviderIntakePlanner
{
    /// <summary>
    /// Crea un plan con un máximo de un candidato por pareja
    /// <c>(provider, external_settlement_id)</c>.
    /// </summary>
    public static ProviderIntakePlan CreatePlan(
        IEnumerable<VerifiedExternalPaymentEvidence> incoming,
        IEnumerable<VerifiedExternalPaymentEvidence> known)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(known);

        var arrivals = incoming.ToArray();
        var history = known.ToArray();
        var batch = ProviderReplayBatchAnalyzer.Analyze(arrivals, history);
        var knownIdentities = history
            .Select(PaymentIdempotencyKeys.From)
            .ToHashSet();
        var result = new List<ProviderIntakePlanItem>(batch.Items.Count);

        var groups = batch.Items
            .GroupBy(item => new ProviderSettlementGroup(
                item.Identity.Reception.Provider,
                item.Identity.Settlement.Value))
            .OrderBy(group => group.Key.Provider, StringComparer.Ordinal)
            .ThenBy(group => group.Key.ExternalSettlementId, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var ordered = group
                .OrderBy(
                    item => item.Identity.Reception.ProviderEventId,
                    StringComparer.Ordinal)
                .ToArray();
            var groupHasConflict = ordered.Any(item => IsConflict(item.Decision));
            var knownSettlement = knownIdentities.Any(identity =>
                StringComparer.Ordinal.Equals(
                    identity.Reception.Provider,
                    group.Key.Provider)
                && StringComparer.Ordinal.Equals(
                    identity.Settlement.Value,
                    group.Key.ExternalSettlementId));
            var candidateChosen = false;

            foreach (var entry in ordered)
            {
                ProviderIntakeDisposition disposition;

                if (groupHasConflict)
                {
                    // El grupo completo queda retenido. Un duplicado histórico o
                    // del lote nunca puede esconder el conflicto de otro item.
                    disposition = ProviderIntakeDisposition.HoldForConflict;
                }
                else if (knownIdentities.Contains(entry.Identity))
                {
                    disposition = ProviderIntakeDisposition.AlreadyReceived;
                }
                else if (knownSettlement)
                {
                    disposition = ProviderIntakeDisposition.RelatesToKnownSettlement;
                }
                else if (!candidateChosen)
                {
                    disposition =
                        ProviderIntakeDisposition.CandidateRequiringIndependentAuthentication;
                    candidateChosen = true;
                }
                else
                {
                    disposition = ProviderIntakeDisposition.RelatesToCandidateInBatch;
                }

                result.Add(new ProviderIntakePlanItem(
                    entry.Identity,
                    entry.Occurrences,
                    disposition));
            }
        }

        var sorted = result
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

        return new ProviderIntakePlan(
            sorted,
            sorted.Any(item =>
                item.Disposition == ProviderIntakeDisposition.HoldForConflict),
            sorted.Count(item =>
                item.Disposition ==
                    ProviderIntakeDisposition.CandidateRequiringIndependentAuthentication));
    }

    private static bool IsConflict(ProviderReplayDecision decision)
        => decision is ProviderReplayDecision.ConflictingEvent
            or ProviderReplayDecision.ConflictingSettlement;

    private sealed record ProviderSettlementGroup(
        string Provider,
        string ExternalSettlementId);
}
