using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application.Contracts;

internal sealed record ProviderReceptionRequest
{
    private ProviderReceptionRequest(
        string provider,
        string providerEventId,
        string externalSettlementId,
        ReadOnlyMemory<byte> exactBody,
        DateTimeOffset receivedAt)
    {
        Provider = provider;
        ProviderEventId = providerEventId;
        ExternalSettlementId = externalSettlementId;
        ExactBody = exactBody.ToArray();
        ReceivedAt = receivedAt;
    }

    internal string Provider { get; }
    internal string ProviderEventId { get; }
    internal string ExternalSettlementId { get; }
    internal ReadOnlyMemory<byte> ExactBody { get; }
    internal DateTimeOffset ReceivedAt { get; }

    internal static ProviderReceptionRequest Create(
        string? provider,
        string? providerEventId,
        string? externalSettlementId,
        ReadOnlyMemory<byte> exactBody,
        DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(provider)
            || string.IsNullOrWhiteSpace(providerEventId)
            || string.IsNullOrWhiteSpace(externalSettlementId)
            || exactBody.IsEmpty
            || receivedAt == default)
        {
            throw new ArgumentException(
                "La recepción exige identidades opacas, body e instante.");
        }

        return new(
            provider,
            providerEventId,
            externalSettlementId,
            exactBody,
            receivedAt);
    }
}

internal enum ProviderVerificationOutcome
{
    Authenticated,
    Rejected,
    Indeterminate
}

internal sealed record ProviderVerificationResult
{
    private ProviderVerificationResult(
        ProviderVerificationOutcome outcome,
        DateTimeOffset? verifiedAt,
        string? attestationReference,
        string? reason)
    {
        Outcome = outcome;
        VerifiedAt = verifiedAt;
        AttestationReference = attestationReference;
        Reason = reason;
    }

    internal ProviderVerificationOutcome Outcome { get; }
    internal DateTimeOffset? VerifiedAt { get; }
    internal string? AttestationReference { get; }
    internal string? Reason { get; }

    internal static ProviderVerificationResult Authenticated(
        DateTimeOffset verifiedAt,
        string? attestationReference)
    {
        if (verifiedAt == default
            || string.IsNullOrWhiteSpace(attestationReference))
        {
            throw new ArgumentException(
                "La verificación autenticada exige instante y referencia.");
        }

        return new(
            ProviderVerificationOutcome.Authenticated,
            verifiedAt,
            attestationReference,
            null);
    }

    internal static ProviderVerificationResult Rejected(string? reason)
        => ResolvedWithoutAuthentication(
            ProviderVerificationOutcome.Rejected,
            reason);

    internal static ProviderVerificationResult Indeterminate(string? reason)
        => ResolvedWithoutAuthentication(
            ProviderVerificationOutcome.Indeterminate,
            reason);

    private static ProviderVerificationResult ResolvedWithoutAuthentication(
        ProviderVerificationOutcome outcome,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Un resultado no autenticado exige razón.",
                nameof(reason));
        }

        return new(outcome, null, null, reason);
    }
}

internal interface IProviderEvidenceVerifier
{
    ValueTask<ProviderVerificationResult> VerifyAsync(
        ProviderReceptionRequest request,
        CancellationToken cancellationToken);
}

internal interface IProviderReceptionService
{
    ValueTask<ProviderReceptionResult> ReceiveAsync(
        ProviderReceptionRequest request,
        CancellationToken cancellationToken = default);
}

internal enum ProviderReceptionState
{
    Rejected,
    Indeterminate,
    Held,
    Duplicate,
    PendingDurableReview
}

internal sealed record ProviderReceptionRecord(
    Guid ReceiptId,
    string Provider,
    string ProviderEventId,
    string ExternalSettlementId,
    string BodySha256,
    DateTimeOffset ReceivedAt,
    ProviderVerificationOutcome VerificationOutcome,
    string? AttestationReference,
    string? VerificationReason,
    ProviderReceptionState State,
    VerifiedExternalPaymentEvidence? VerifiedEvidence,
    string? AnalysisSha256);

internal interface IProviderReceptionLedger
{
    ValueTask<IReadOnlyList<VerifiedExternalPaymentEvidence>>
        LoadKnownVerifiedEvidenceAsync(CancellationToken cancellationToken);

    ValueTask RecordAsync(
        ProviderReceptionRecord record,
        CancellationToken cancellationToken);
}

internal sealed record ProviderReceptionResult(
    ProviderReceptionRecord Record,
    ProviderSettlementAnalysisResult? Analysis);
