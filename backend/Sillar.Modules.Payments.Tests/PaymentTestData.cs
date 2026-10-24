using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Tests;

internal static class PaymentTestData
{
    internal static readonly DateTimeOffset VerifiedAt =
        new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    internal static readonly Guid OrderId =
        Guid.Parse("018f1f76-6e4d-7f8e-9a10-111213141516");

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

    internal static ProviderSettlementFacts Facts(
        VerifiedExternalPaymentEvidence? evidence = null,
        decimal amount = 125m,
        string currencyCode = "PEN")
        => ProviderSettlementFacts.Create(
            evidence ?? Evidence(),
            amount,
            currencyCode,
            VerifiedAt.AddMinutes(1));

    internal static ProviderCommercialEffectCandidate CommercialCandidate(
        Guid? inboxEventId = null,
        VerifiedExternalPaymentEvidence? evidence = null,
        ProviderSettlementFacts? facts = null,
        Guid? orderId = null,
        string evidenceReference = "verifier-ref-001")
    {
        evidence ??= Evidence();
        facts ??= Facts(evidence);
        var identity = PaymentIdempotencyKeys.From(evidence);
        return ProviderCommercialEffectCandidate.From(
            inboxEventId ?? Guid.CreateVersion7(),
            evidence,
            facts,
            ProviderAuthorizedOrderCorrelation.Create(
                orderId ?? OrderId,
                identity,
                "authorized-order-correlation-001"),
            ProviderVerifiedEvidenceReference.Create(
                Guid.CreateVersion7(),
                evidence,
                evidenceReference));
    }
}
