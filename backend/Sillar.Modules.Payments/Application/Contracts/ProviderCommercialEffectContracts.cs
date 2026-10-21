using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application.Contracts;

/// <summary>
/// Candidato técnico para un puerto comercial futuro. No es el contrato M03 y
/// deliberadamente no autoriza una aplicación: los hechos monetarios y la
/// autoridad escritora deberán formar parte del contrato ratificado de M03.
/// </summary>
internal sealed record ProviderCommercialEffectCandidate(
    Guid InboxEventId,
    PaymentIdempotencyKeys Identity,
    ProviderSettlementFacts Facts)
{
    internal static ProviderCommercialEffectCandidate From(
        Guid inboxEventId,
        VerifiedExternalPaymentEvidence evidence,
        ProviderSettlementFacts facts)
    {
        if (inboxEventId == Guid.Empty)
        {
            throw new ArgumentException("El candidato exige inbox.", nameof(inboxEventId));
        }

        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(facts);
        if (ProviderEventIdempotencyKey.From(evidence)
                != ProviderEventIdempotencyKey.From(facts.Evidence)
            || ExternalSettlementIdempotencyKey.From(evidence)
                != ExternalSettlementIdempotencyKey.From(facts.Evidence)
            || !StringComparer.Ordinal.Equals(
                evidence.BodySha256,
                facts.Evidence.BodySha256))
        {
            throw new ArgumentException(
                "Los hechos no corresponden a la evidencia reclamada.",
                nameof(facts));
        }

        return new(
            inboxEventId,
            PaymentIdempotencyKeys.From(evidence),
            facts);
    }
}

internal enum ProviderCommercialEffectOutcome
{
    Applied,
    AlreadyApplied,
    Conflict,
    DecisionRequired,
    Indeterminate
}

internal sealed record ProviderCommercialEffectResult
{
    private ProviderCommercialEffectResult(
        ProviderCommercialEffectOutcome outcome,
        string? reference,
        string? reason,
        bool? orderWasExpired,
        ProviderSettlementFacts? confirmedFacts)
    {
        Outcome = outcome;
        Reference = reference;
        Reason = reason;
        OrderWasExpired = orderWasExpired;
        ConfirmedFacts = confirmedFacts;
    }

    internal ProviderCommercialEffectOutcome Outcome { get; }
    internal string? Reference { get; }
    internal string? Reason { get; }
    internal bool? OrderWasExpired { get; }
    internal ProviderSettlementFacts? ConfirmedFacts { get; }

    internal static ProviderCommercialEffectResult Applied(
        string? reference,
        bool orderWasExpired)
        => Successful(
            ProviderCommercialEffectOutcome.Applied,
            reference,
            orderWasExpired,
            null);

    internal static ProviderCommercialEffectResult AlreadyApplied(
        string? reference,
        bool orderWasExpired,
        ProviderSettlementFacts confirmedFacts)
        => Successful(
            ProviderCommercialEffectOutcome.AlreadyApplied,
            reference,
            orderWasExpired,
            confirmedFacts);

    internal static ProviderCommercialEffectResult Conflict(string? reason)
        => WithoutApplication(ProviderCommercialEffectOutcome.Conflict, reason);

    internal static ProviderCommercialEffectResult DecisionRequired(string? reason)
        => WithoutApplication(
            ProviderCommercialEffectOutcome.DecisionRequired,
            reason);

    internal static ProviderCommercialEffectResult Indeterminate(string? reason)
        => WithoutApplication(
            ProviderCommercialEffectOutcome.Indeterminate,
            reason);

    private static ProviderCommercialEffectResult Successful(
        ProviderCommercialEffectOutcome outcome,
        string? reference,
        bool orderWasExpired,
        ProviderSettlementFacts? confirmedFacts)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException(
                "Un efecto acreditado exige referencia.",
                nameof(reference));
        }

        if (outcome == ProviderCommercialEffectOutcome.AlreadyApplied
            && confirmedFacts is null)
        {
            throw new ArgumentNullException(nameof(confirmedFacts));
        }

        return new(
            outcome,
            reference,
            null,
            orderWasExpired,
            confirmedFacts);
    }

    private static ProviderCommercialEffectResult WithoutApplication(
        ProviderCommercialEffectOutcome outcome,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Un efecto no acreditado exige razón.",
                nameof(reason));
        }

        return new(outcome, null, reason, null, null);
    }
}

/// <summary>
/// Puerto sin implementación productiva. El futuro adaptador M03 requerirá un
/// contrato autorizado antes de poder implementarlo.
/// </summary>
internal interface IProviderCommercialEffectPort
{
    ValueTask<ProviderCommercialEffectResult> TryApplyAsync(
        ProviderCommercialEffectCandidate candidate,
        CancellationToken cancellationToken);
}

internal enum ProviderCommercialRecoveryState
{
    Pending,
    Indeterminate,
    Applied,
    AlreadyApplied,
    Conflict,
    DecisionRequired
}

internal sealed record ProviderCommercialRecoverySnapshot(
    ProviderCommercialRecoveryState State,
    int Attempts,
    string? CommercialReference,
    string? LastReason,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? NextAttemptAt,
    bool? OrderWasExpired)
{
    internal static ProviderCommercialRecoverySnapshot Initial
        => new(
            ProviderCommercialRecoveryState.Pending,
            0,
            null,
            null,
            null,
            null,
            null);
}
