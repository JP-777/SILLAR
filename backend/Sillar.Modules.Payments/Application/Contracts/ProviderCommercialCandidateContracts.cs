using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application.Contracts;

/// <summary>
/// Correlación suministrada por una autoridad futura. M11 no deriva ni inventa
/// el pedido a partir de identificadores externos del proveedor.
/// </summary>
internal sealed record ProviderAuthorizedOrderCorrelation
{
    private ProviderAuthorizedOrderCorrelation(
        Guid orderId,
        PaymentIdempotencyKeys identity,
        string authorityReference,
        string? orderCode)
    {
        OrderId = orderId;
        Identity = identity;
        AuthorityReference = authorityReference;
        OrderCode = orderCode;
    }

    internal Guid OrderId { get; }
    internal PaymentIdempotencyKeys Identity { get; }
    internal string AuthorityReference { get; }
    internal string? OrderCode { get; }

    internal static ProviderAuthorizedOrderCorrelation Create(
        Guid orderId,
        PaymentIdempotencyKeys identity,
        string? authorityReference)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "La correlación autorizada exige OrderId.",
                nameof(orderId));
        }

        ArgumentNullException.ThrowIfNull(identity);
        if (string.IsNullOrWhiteSpace(authorityReference))
        {
            throw new ArgumentException(
                "La correlación exige referencia de autoridad.",
                nameof(authorityReference));
        }

        return new(orderId, identity, authorityReference, null);
    }

    internal static ProviderAuthorizedOrderCorrelation CreateResolvedOrder(
        Guid orderId,
        string? orderCode,
        PaymentIdempotencyKeys identity,
        string? authoritySource)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            throw new ArgumentException(
                "Una correlación resuelta exige código de pedido.",
                nameof(orderCode));
        }

        var correlation = Create(orderId, identity, authoritySource);
        return new(
            correlation.OrderId,
            correlation.Identity,
            correlation.AuthorityReference,
            orderCode);
    }
}

/// <summary>
/// Referencia tipada a una recepción autenticada ya conservada por M11.
/// Transporta la identidad exacta y no representa el cuerpo ni un pago.
/// </summary>
internal sealed record ProviderVerifiedEvidenceReference
{
    private ProviderVerifiedEvidenceReference(
        Guid receiptId,
        PaymentIdempotencyKeys identity,
        string bodySha256,
        DateTimeOffset verifiedAt,
        string value)
    {
        ReceiptId = receiptId;
        Identity = identity;
        BodySha256 = bodySha256;
        VerifiedAt = verifiedAt;
        Value = value;
    }

    internal Guid ReceiptId { get; }
    internal PaymentIdempotencyKeys Identity { get; }
    internal string BodySha256 { get; }
    internal DateTimeOffset VerifiedAt { get; }
    internal string Value { get; }

    internal static ProviderVerifiedEvidenceReference Create(
        Guid receiptId,
        VerifiedExternalPaymentEvidence evidence,
        string? value)
    {
        if (receiptId == Guid.Empty)
        {
            throw new ArgumentException(
                "La referencia exige una recepción persistida.",
                nameof(receiptId));
        }

        ArgumentNullException.ThrowIfNull(evidence);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "La referencia de evidencia autenticada es obligatoria.",
                nameof(value));
        }

        return new(
            receiptId,
            PaymentIdempotencyKeys.From(evidence),
            evidence.BodySha256,
            evidence.VerifiedAt,
            value);
    }

    internal bool Matches(VerifiedExternalPaymentEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return Identity == PaymentIdempotencyKeys.From(evidence)
            && StringComparer.Ordinal.Equals(BodySha256, evidence.BodySha256)
            && VerifiedAt == evidence.VerifiedAt;
    }
}

internal interface IProviderAuthorizedOrderCorrelationSource
{
    ValueTask<ProviderAuthorizedOrderCorrelation?> LoadAsync(
        Guid inboxEventId,
        PaymentIdempotencyKeys identity,
        CancellationToken cancellationToken);
}

internal interface IProviderVerifiedEvidenceReferenceSource
{
    ValueTask<IReadOnlyList<ProviderVerifiedEvidenceReference>> LoadAsync(
        Guid inboxEventId,
        CancellationToken cancellationToken);
}

internal enum ProviderCommercialCandidatePreparationOutcome
{
    Ready,
    DecisionRequired
}

internal sealed record ProviderCommercialCandidatePreparation
{
    private ProviderCommercialCandidatePreparation(
        ProviderCommercialCandidatePreparationOutcome outcome,
        ProviderCommercialEffectCandidate? candidate,
        string? reason)
    {
        Outcome = outcome;
        Candidate = candidate;
        Reason = reason;
    }

    internal ProviderCommercialCandidatePreparationOutcome Outcome { get; }
    internal ProviderCommercialEffectCandidate? Candidate { get; }
    internal string? Reason { get; }

    internal static ProviderCommercialCandidatePreparation Ready(
        ProviderCommercialEffectCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new(
            ProviderCommercialCandidatePreparationOutcome.Ready,
            candidate,
            null);
    }

    internal static ProviderCommercialCandidatePreparation DecisionRequired(
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "La preparación bloqueada exige razón.",
                nameof(reason));
        }

        return new(
            ProviderCommercialCandidatePreparationOutcome.DecisionRequired,
            null,
            reason);
    }
}

internal interface IProviderCommercialCandidateSource
{
    ValueTask<ProviderCommercialCandidatePreparation> PrepareAsync(
        Guid inboxEventId,
        CancellationToken cancellationToken);
}
