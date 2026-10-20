using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application.Contracts;

/// <summary>
/// Candidato técnico para un puerto comercial futuro. No es el contrato M03 y
/// no contiene todavía pedido, importe o moneda.
/// </summary>
internal sealed record ProviderCommercialEffectCandidate(
    Guid InboxEventId,
    PaymentIdempotencyKeys Identity)
{
    internal static ProviderCommercialEffectCandidate From(
        Guid inboxEventId,
        VerifiedExternalPaymentEvidence evidence)
    {
        if (inboxEventId == Guid.Empty)
        {
            throw new ArgumentException("El candidato exige inbox.", nameof(inboxEventId));
        }

        ArgumentNullException.ThrowIfNull(evidence);
        return new(inboxEventId, PaymentIdempotencyKeys.From(evidence));
    }
}

internal enum ProviderCommercialEffectOutcome
{
    Accredited,
    Rejected,
    Indeterminate
}

internal sealed record ProviderCommercialEffectResult
{
    private ProviderCommercialEffectResult(
        ProviderCommercialEffectOutcome outcome,
        string? reference,
        string? reason)
    {
        Outcome = outcome;
        Reference = reference;
        Reason = reason;
    }

    internal ProviderCommercialEffectOutcome Outcome { get; }
    internal string? Reference { get; }
    internal string? Reason { get; }

    internal static ProviderCommercialEffectResult Accredited(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException(
                "Un efecto acreditado exige referencia.",
                nameof(reference));
        }

        return new(ProviderCommercialEffectOutcome.Accredited, reference, null);
    }

    internal static ProviderCommercialEffectResult Rejected(string? reason)
        => WithoutAccreditation(ProviderCommercialEffectOutcome.Rejected, reason);

    internal static ProviderCommercialEffectResult Indeterminate(string? reason)
        => WithoutAccreditation(
            ProviderCommercialEffectOutcome.Indeterminate,
            reason);

    private static ProviderCommercialEffectResult WithoutAccreditation(
        ProviderCommercialEffectOutcome outcome,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Un efecto no acreditado exige razón.",
                nameof(reason));
        }

        return new(outcome, null, reason);
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
    Accredited,
    Rejected
}

internal sealed record ProviderCommercialRecoverySnapshot(
    ProviderCommercialRecoveryState State,
    int Attempts,
    string? AccreditedReference,
    string? LastReason,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? NextAttemptAt)
{
    internal static ProviderCommercialRecoverySnapshot Initial
        => new(
            ProviderCommercialRecoveryState.Pending,
            0,
            null,
            null,
            null,
            null);
}
