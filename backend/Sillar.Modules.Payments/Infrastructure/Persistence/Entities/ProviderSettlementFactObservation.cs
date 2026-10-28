using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

/// <summary>
/// Hechos monetarios observados para una recepción concreta. Es append-only:
/// una recepción conserva sus hechos incluso cuando contradicen observaciones
/// anteriores y fuerzan HOLD.
/// </summary>
internal sealed class ProviderSettlementFactObservation
{
    private ProviderSettlementFactObservation()
    {
    }

    internal Guid ReceiptId { get; private set; }
    internal string Provider { get; private set; } = null!;
    internal string ProviderEventId { get; private set; } = null!;
    internal string ExternalSettlementId { get; private set; } = null!;
    internal string BodySha256 { get; private set; } = null!;
    internal DateTimeOffset VerifiedAt { get; private set; }
    internal decimal GrossAmount { get; private set; }
    internal string CurrencyCode { get; private set; } = null!;
    internal DateTimeOffset ProviderConfirmedAt { get; private set; }
    internal string Classification { get; private set; } = null!;
    internal DateTimeOffset RecordedAt { get; private set; }

    internal static ProviderSettlementFactObservation Create(
        Guid receiptId,
        ProviderSettlementFacts facts,
        ProviderSettlementFactDecision classification,
        DateTimeOffset recordedAt)
    {
        if (receiptId == Guid.Empty)
        {
            throw new ArgumentException(
                "Los hechos monetarios exigen una recepción.",
                nameof(receiptId));
        }

        ArgumentNullException.ThrowIfNull(facts);
        if (!Enum.IsDefined(classification))
        {
            throw new ArgumentOutOfRangeException(nameof(classification));
        }

        if (recordedAt < facts.Evidence.VerifiedAt)
        {
            throw new ArgumentException(
                "Los hechos no pueden registrarse antes de verificarse.",
                nameof(recordedAt));
        }

        return new()
        {
            ReceiptId = receiptId,
            Provider = facts.Evidence.Provider,
            ProviderEventId = facts.Evidence.ProviderEventId,
            ExternalSettlementId = facts.Evidence.ExternalSettlementId,
            BodySha256 = facts.Evidence.BodySha256,
            VerifiedAt = facts.Evidence.VerifiedAt,
            GrossAmount = facts.GrossAmount,
            CurrencyCode = facts.Currency.Value,
            ProviderConfirmedAt = facts.ProviderConfirmedAt,
            Classification = ToPersistenceClassification(classification),
            RecordedAt = ProviderPersistenceGuard.RequiredInstant(
                recordedAt,
                nameof(recordedAt))
        };
    }

    internal ProviderSettlementFacts ToDomain()
        => ProviderSettlementFacts.Create(
            VerifiedExternalPaymentEvidence.FromVerifiedWebhook(
                Provider,
                ProviderEventId,
                ExternalSettlementId,
                BodySha256,
                VerifiedAt),
            GrossAmount,
            CurrencyCode,
            ProviderConfirmedAt);

    internal bool HasSameFacts(ProviderSettlementFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return StringComparer.Ordinal.Equals(Provider, facts.Evidence.Provider)
            && StringComparer.Ordinal.Equals(
                ProviderEventId,
                facts.Evidence.ProviderEventId)
            && StringComparer.Ordinal.Equals(
                ExternalSettlementId,
                facts.Evidence.ExternalSettlementId)
            && StringComparer.Ordinal.Equals(
                BodySha256,
                facts.Evidence.BodySha256)
            && VerifiedAt == facts.Evidence.VerifiedAt
            && GrossAmount == facts.GrossAmount
            && StringComparer.Ordinal.Equals(
                CurrencyCode,
                facts.Currency.Value)
            && ProviderConfirmedAt == facts.ProviderConfirmedAt;
    }

    private static string ToPersistenceClassification(
        ProviderSettlementFactDecision classification)
        => classification switch
        {
            ProviderSettlementFactDecision.New =>
                ProviderSettlementFactClassifications.New,
            ProviderSettlementFactDecision.Duplicate =>
                ProviderSettlementFactClassifications.Duplicate,
            ProviderSettlementFactDecision.Conflict =>
                ProviderSettlementFactClassifications.Conflict,
            ProviderSettlementFactDecision.DecisionRequired =>
                ProviderSettlementFactClassifications.DecisionRequired,
            _ => throw new ArgumentOutOfRangeException(nameof(classification))
        };
}
