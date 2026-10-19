using System;
using System.Collections.Generic;
using System.Linq;

// P8 — consolidación de revisión puramente informativa sobre P7.
// Ninguna autenticación, pago, escritura, idempotencia durable o integración externa.
namespace Sillar.Modules.Payments.Domain;

public enum ProviderReviewAuditSeverity
{
    Hold,
    NeedsEvidence,
    PreviouslyKnown,
    SeparateDurableReview
}

public sealed record ProviderReviewAuditRow(
    string Provider,
    string ExternalSettlementId,
    ProviderReviewAuditSeverity Severity,
    ProviderSettlementReviewLane OriginalLane,
    ProviderSettlementReviewHoldReason OriginalHoldReason,
    int EventIdentities,
    long IncomingOccurrences);

public sealed record ProviderReviewAuditProviderSummary(
    string Provider,
    int TotalGroups,
    int HeldGroups,
    int EvidenceGroups,
    int KnownGroups,
    int SeparateDurableReviewGroups,
    long IncomingOccurrences);

public sealed record ProviderReviewAuditResult(
    IReadOnlyList<ProviderReviewAuditRow> Rows,
    IReadOnlyList<ProviderReviewAuditProviderSummary> ByProvider,
    int TotalGroups,
    int HeldGroups,
    int CrossProviderIdentityHolds,
    int EvidenceGroups,
    int KnownGroups,
    int SeparateDurableReviewGroups)
{
    // Esta proyección deliberadamente no expresa autorización, pago ni autenticación.
}

/// <summary>
/// Valida la coherencia de P7 y produce un resumen interno reproducible.
/// P8 no crea un carril "aprobado", no autoriza ninguna acción posterior y
/// falla cerrado ante identidades duplicadas o colisiones no retenidas.
/// </summary>
public static class ProviderSettlementReviewAudit
{
    public static ProviderReviewAuditResult Summarize(ProviderSettlementReviewPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(plan.Items);
        var source = plan.Items.ToArray();
        if (source.Any(x => x is null ||
                            string.IsNullOrWhiteSpace(x.Provider) ||
                            string.IsNullOrWhiteSpace(x.ExternalSettlementId) ||
                            x.EventIdentities <= 0 || x.IncomingOccurrences <= 0 ||
                            !Enum.IsDefined(x.Lane) || !Enum.IsDefined(x.HoldReason)))
            throw new ArgumentException("P7 contiene identidades, recuentos o estados inválidos.", nameof(plan));

        var duplicatePair = source.GroupBy(x => (x.Provider, x.ExternalSettlementId))
            .Any(g => g.Skip(1).Any());
        if (duplicatePair)
            throw new ArgumentException("P7 contiene identidades compuestas duplicadas.", nameof(plan));

        var collidedIds = source.GroupBy(x => x.ExternalSettlementId, StringComparer.Ordinal)
            .Where(g => g.Select(x => x.Provider).Distinct(StringComparer.Ordinal).Skip(1).Any())
            .Select(g => g.Key).ToHashSet(StringComparer.Ordinal);

        // Regla no delegable: ninguna clasificación posterior puede soltar HOLD
        // interproveedor, ni omitir la razón explícita de HOLD en una colisión.
        if (source.Any(x => collidedIds.Contains(x.ExternalSettlementId) &&
             (x.Lane != ProviderSettlementReviewLane.Hold ||
              x.HoldReason != ProviderSettlementReviewHoldReason.CrossProviderIdentityUnresolved)))
            throw new ArgumentException("P7 omitió HOLD interproveedor obligatorio.", nameof(plan));
        if (source.Any(x => x.Lane == ProviderSettlementReviewLane.Hold &&
                            x.HoldReason == ProviderSettlementReviewHoldReason.None))
            throw new ArgumentException("Una retención P7 carece de motivo.", nameof(plan));
        if (source.Any(x => x.Lane != ProviderSettlementReviewLane.Hold &&
                            x.HoldReason != ProviderSettlementReviewHoldReason.None))
            throw new ArgumentException("Motivo de HOLD asociado a un carril que no está retenido.", nameof(plan));

        int held = source.Count(x => x.Lane == ProviderSettlementReviewLane.Hold);
        int evidence = source.Count(x => x.Lane is ProviderSettlementReviewLane.RejectedEvidence or
                                                    ProviderSettlementReviewLane.AwaitIndependentEvidence);
        int known = source.Count(x => x.Lane == ProviderSettlementReviewLane.PreviouslyKnown);
        int separate = source.Count(x => x.Lane == ProviderSettlementReviewLane.RequiresSeparateDurableGate);
        if (plan.HoldCount != held || plan.EvidenceAttentionCount != evidence ||
            plan.KnownCount != known || plan.SeparateDurableGateReviewCount != separate ||
            held + evidence + known + separate != source.Length)
            throw new ArgumentException("Los totales P7 no coinciden con sus filas.", nameof(plan));

        var ordered = source.OrderBy(x => x.Provider, StringComparer.Ordinal)
                            .ThenBy(x => x.ExternalSettlementId, StringComparer.Ordinal)
                            .ToArray();
        var rows = ordered.Select(x => new ProviderReviewAuditRow(
            x.Provider, x.ExternalSettlementId,
            x.Lane switch
            {
                ProviderSettlementReviewLane.Hold => ProviderReviewAuditSeverity.Hold,
                ProviderSettlementReviewLane.RejectedEvidence or
                    ProviderSettlementReviewLane.AwaitIndependentEvidence => ProviderReviewAuditSeverity.NeedsEvidence,
                ProviderSettlementReviewLane.PreviouslyKnown => ProviderReviewAuditSeverity.PreviouslyKnown,
                ProviderSettlementReviewLane.RequiresSeparateDurableGate => ProviderReviewAuditSeverity.SeparateDurableReview,
                _ => throw new InvalidOperationException("Carril P7 no reconocido.")
            }, x.Lane, x.HoldReason, x.EventIdentities, x.IncomingOccurrences)).ToArray();

        var providers = ordered.GroupBy(x => x.Provider, StringComparer.Ordinal)
            .Select(g => new ProviderReviewAuditProviderSummary(
                g.Key, g.Count(),
                g.Count(x => x.Lane == ProviderSettlementReviewLane.Hold),
                g.Count(x => x.Lane is ProviderSettlementReviewLane.RejectedEvidence or
                                      ProviderSettlementReviewLane.AwaitIndependentEvidence),
                g.Count(x => x.Lane == ProviderSettlementReviewLane.PreviouslyKnown),
                g.Count(x => x.Lane == ProviderSettlementReviewLane.RequiresSeparateDurableGate),
                checked(g.Sum(x => x.IncomingOccurrences))))
            .OrderBy(x => x.Provider, StringComparer.Ordinal)
            .ToArray();

        return new ProviderReviewAuditResult(rows, providers, source.Length, held,
            source.Count(x => collidedIds.Contains(x.ExternalSettlementId)),
            evidence, known, separate);
    }
}
