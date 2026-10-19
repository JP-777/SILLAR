using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Tests;

internal static class PaymentTestData
{
    internal static readonly DateTimeOffset VerifiedAt =
        new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    internal static VerifiedExternalPaymentEvidence Evidence(
        string provider = "provider-a",
        string eventId = "event-001",
        string settlementId = "settlement-001",
        char hashDigit = 'a',
        DateTimeOffset? verifiedAt = null)
        => VerifiedExternalPaymentEvidence.FromVerifiedWebhook(
            provider,
            eventId,
            settlementId,
            new string(hashDigit, 64),
            verifiedAt ?? VerifiedAt);

    internal static ProviderEvidenceObservation Attested(
        VerifiedExternalPaymentEvidence evidence,
        string reference = "verifier-ref-001")
        => new(
            PaymentIdempotencyKeys.From(evidence),
            ProviderEvidenceVerdict.AttestedByVerifier,
            reference);
}
