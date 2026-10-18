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
        var observations = evidence.ToArray();
        var basePlan = ProviderIntakePlanner.CreatePlan(arrivals, history);
        var identities = basePlan.Items
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
        var results = new List<ProviderEvidenceReviewItem>(basePlan.Items.Count);

        foreach (var item in basePlan.Items)
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
}
