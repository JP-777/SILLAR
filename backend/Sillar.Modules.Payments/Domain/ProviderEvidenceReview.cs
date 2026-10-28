namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Declaración recibida desde una futura frontera externa de validación.
/// </summary>
/// <remarks>
/// Este valor no verifica firmas ni autentica webhooks. En particular,
/// <see cref="AttestedByVerifier"/> sólo puede provenir de un adaptador confiable
/// futuro y nunca del cuerpo declarado por el propio webhook.
/// </remarks>
public enum ProviderEvidenceVerdict
{
    Unverified,
    RejectedByVerifier,
    AttestedByVerifier
}

/// <summary>Observación externa opaca asociada a una identidad exacta.</summary>
public sealed record ProviderEvidenceObservation(
    PaymentIdempotencyKeys Identity,
    ProviderEvidenceVerdict Verdict,
    string? AttestationReference);

/// <summary>Clasificación informativa, sin cobro ni escritura de inbox.</summary>
public enum ProviderEvidenceReviewDisposition
{
    HoldReplayConflict,
    HoldInconsistentEvidence,
    RejectUntrustedEvidence,
    AwaitIndependentVerification,
    AlreadyReceivedAttested,
    KnownSettlementAttested,
    RelatedToCandidateAttested,
    CandidateForDurableInboxReview
}

/// <summary>Resultado de revisión para una identidad única del lote.</summary>
/// <remarks>
/// <see cref="Observations"/> conserva todas las declaraciones recibidas,
/// incluidas las contradictorias. Una atestación registrada aquí no sustituye
/// la autenticación independiente exigida antes de cualquier efecto.
/// </remarks>
public sealed record ProviderEvidenceReviewItem(
    PaymentIdempotencyKeys Identity,
    int Occurrences,
    ProviderEvidenceReviewDisposition Disposition,
    string? AttestationReference,
    IReadOnlyList<ProviderEvidenceObservation> Observations)
{
    public bool RequiresIndependentAuthentication => true;
}

/// <summary>Plan de revisión puro, sin efectos externos.</summary>
public sealed record ProviderEvidenceReviewPlan(
    IReadOnlyList<ProviderEvidenceReviewItem> Items,
    int CandidatesForDurableInboxReview,
    bool HasAnyHolds);

/// <summary>
/// Enlaza deterministamente el plan P4 con observaciones de una futura frontera
/// externa de confianza.
/// </summary>
/// <remarks>
/// Los conflictos P2/P3/P4 prevalecen y ninguna observación puede rebajarlos.
/// Un candidato sólo habilita revisión posterior: no autentica, no persiste, no
/// ejecuta idempotencia y no afirma un pago aplicado.
/// </remarks>
public static class ProviderEvidenceReview
{
    public static ProviderEvidenceReviewPlan Review(
        IEnumerable<VerifiedExternalPaymentEvidence> incoming,
        IEnumerable<VerifiedExternalPaymentEvidence> known,
        IEnumerable<ProviderEvidenceObservation> evidence)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(evidence);

        var arrivals = incoming.ToArray();
        var history = known.ToArray();
        var basePlan = ProviderIntakePlanner.CreatePlan(arrivals, history);

        return Review(basePlan, evidence);
    }

    /// <summary>
    /// Revisa una instantánea P4 ya materializada, después de validar que sus
    /// grupos y contadores conservan las reglas fail-closed del planificador.
    /// </summary>
    public static ProviderEvidenceReviewPlan Review(
        ProviderIntakePlan basePlan,
        IEnumerable<ProviderEvidenceObservation> evidence)
    {
        ArgumentNullException.ThrowIfNull(basePlan);
        ArgumentNullException.ThrowIfNull(evidence);

        var planItems = ValidateBasePlan(basePlan);
        var observations = evidence.ToArray();
        var identities = planItems
            .Select(item => item.Identity)
            .ToHashSet();

        foreach (var observation in observations)
        {
            if (observation is null || observation.Identity is null)
            {
                throw new ArgumentException(
                    "La evidencia no admite entradas ni identidades nulas.",
                    nameof(evidence));
            }

            if (!identities.Contains(observation.Identity))
            {
                throw new ArgumentException(
                    "La evidencia describe una identidad que no está en el lote.",
                    nameof(evidence));
            }

            if (!Enum.IsDefined(observation.Verdict))
            {
                throw new ArgumentException(
                    "El veredicto de evidencia es desconocido.",
                    nameof(evidence));
            }

            if (observation.Verdict == ProviderEvidenceVerdict.AttestedByVerifier
                && string.IsNullOrWhiteSpace(observation.AttestationReference))
            {
                throw new ArgumentException(
                    "Una atestación exige una referencia externa no vacía.",
                    nameof(evidence));
            }

            if (observation.Verdict != ProviderEvidenceVerdict.AttestedByVerifier
                && observation.AttestationReference is not null)
            {
                throw new ArgumentException(
                    "La evidencia no atestada no puede atribuirse una referencia.",
                    nameof(evidence));
            }
        }

        var observationsByIdentity = observations
            .GroupBy(observation => observation.Identity)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(observation => observation.Verdict)
                    .ThenBy(
                        observation => observation.AttestationReference ?? string.Empty,
                        StringComparer.Ordinal)
                    .ToArray());
        var results = new List<ProviderEvidenceReviewItem>(planItems.Length);

        foreach (var item in planItems)
        {
            observationsByIdentity.TryGetValue(item.Identity, out var itemObservations);
            itemObservations ??= [];

            var differingVerdicts = itemObservations
                .Select(observation => observation.Verdict)
                .Distinct()
                .Count() > 1;
            var differingReferences = itemObservations
                .Select(observation => observation.AttestationReference ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .Count() > 1;
            var inconsistent = differingVerdicts || differingReferences;
            var attested = itemObservations.Length > 0
                && itemObservations.All(observation =>
                    observation.Verdict == ProviderEvidenceVerdict.AttestedByVerifier);
            var rejected = itemObservations.Any(observation =>
                observation.Verdict == ProviderEvidenceVerdict.RejectedByVerifier);

            ProviderEvidenceReviewDisposition disposition;

            if (item.Disposition == ProviderIntakeDisposition.HoldForConflict)
            {
                disposition = ProviderEvidenceReviewDisposition.HoldReplayConflict;
            }
            else if (inconsistent)
            {
                disposition = ProviderEvidenceReviewDisposition.HoldInconsistentEvidence;
            }
            else if (rejected)
            {
                disposition = ProviderEvidenceReviewDisposition.RejectUntrustedEvidence;
            }
            else if (!attested)
            {
                disposition = ProviderEvidenceReviewDisposition.AwaitIndependentVerification;
            }
            else
            {
                disposition = item.Disposition switch
                {
                    ProviderIntakeDisposition.AlreadyReceived =>
                        ProviderEvidenceReviewDisposition.AlreadyReceivedAttested,
                    ProviderIntakeDisposition.RelatesToKnownSettlement =>
                        ProviderEvidenceReviewDisposition.KnownSettlementAttested,
                    ProviderIntakeDisposition.RelatesToCandidateInBatch =>
                        ProviderEvidenceReviewDisposition.RelatedToCandidateAttested,
                    ProviderIntakeDisposition.CandidateRequiringIndependentAuthentication =>
                        ProviderEvidenceReviewDisposition.CandidateForDurableInboxReview,
                    _ => ProviderEvidenceReviewDisposition.HoldReplayConflict
                };
            }

            results.Add(new ProviderEvidenceReviewItem(
                item.Identity,
                item.Occurrences,
                disposition,
                attested && !inconsistent
                    ? itemObservations[0].AttestationReference
                    : null,
                Array.AsReadOnly(itemObservations)));
        }

        return new ProviderEvidenceReviewPlan(
            results.AsReadOnly(),
            results.Count(item =>
                item.Disposition ==
                    ProviderEvidenceReviewDisposition.CandidateForDurableInboxReview),
            results.Any(item =>
                item.Disposition is ProviderEvidenceReviewDisposition.HoldReplayConflict
                    or ProviderEvidenceReviewDisposition.HoldInconsistentEvidence
                    or ProviderEvidenceReviewDisposition.RejectUntrustedEvidence));
    }

    private static ProviderIntakePlanItem[] ValidateBasePlan(
        ProviderIntakePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan.Items);
        var items = plan.Items.ToArray();

        if (items.Any(item => item is null
                || item.Identity is null
                || item.Identity.Reception is null
                || item.Identity.Settlement is null
                || string.IsNullOrWhiteSpace(item.Identity.Reception.Provider)
                || string.IsNullOrWhiteSpace(
                    item.Identity.Reception.ProviderEventId)
                || string.IsNullOrWhiteSpace(item.Identity.Settlement.Value)
                || item.Occurrences <= 0
                || !Enum.IsDefined(item.Disposition)))
        {
            throw new ArgumentException(
                "El plan P4 contiene una identidad o disposición inválida.",
                nameof(plan));
        }

        if (items
            .Select(item => item.Identity)
            .Distinct()
            .Count() != items.Length)
        {
            throw new ArgumentException(
                "El plan P4 repite una identidad externa.",
                nameof(plan));
        }

        var held = items.Count(item =>
            item.Disposition == ProviderIntakeDisposition.HoldForConflict);
        var candidates = items.Count(item => item.Disposition ==
            ProviderIntakeDisposition.CandidateRequiringIndependentAuthentication);

        if (plan.HasConflicts != (held > 0)
            || plan.CandidatesAwaitingAuthentication != candidates)
        {
            throw new ArgumentException(
                "Los contadores P4 no coinciden con sus elementos.",
                nameof(plan));
        }

        foreach (var group in items.GroupBy(item => new ProviderSettlementGroup(
                     item.Identity.Reception.Provider,
                     item.Identity.Settlement.Value)))
        {
            var groupItems = group.ToArray();
            var groupHeld = groupItems.Count(item =>
                item.Disposition == ProviderIntakeDisposition.HoldForConflict);
            var groupCandidates = groupItems.Count(item => item.Disposition ==
                ProviderIntakeDisposition.CandidateRequiringIndependentAuthentication);
            var relatedCandidates = groupItems.Count(item => item.Disposition ==
                ProviderIntakeDisposition.RelatesToCandidateInBatch);

            if ((groupHeld > 0 && groupHeld != groupItems.Length)
                || groupCandidates > 1
                || (groupCandidates == 1
                    && groupItems.Any(item => item.Disposition is not
                        ProviderIntakeDisposition.CandidateRequiringIndependentAuthentication
                        and not ProviderIntakeDisposition.RelatesToCandidateInBatch))
                || (groupCandidates == 0 && relatedCandidates > 0))
            {
                throw new ArgumentException(
                    "Un grupo P4 no conserva su clasificación determinista.",
                    nameof(plan));
            }
        }

        return items;
    }

    private sealed record ProviderSettlementGroup(
        string Provider,
        string ExternalSettlementId);
}
