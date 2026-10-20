namespace Sillar.Modules.Payments.Infrastructure.Persistence.Entities;

/// <summary>Una entrega observada; los replays nunca se sobrescriben.</summary>
internal sealed class ProviderEvidenceReceipt
{
    private ProviderEvidenceReceipt()
    {
    }

    internal Guid ReceiptId { get; private set; }
    internal string Provider { get; private set; } = null!;
    internal string ProviderEventId { get; private set; } = null!;
    internal string ExternalSettlementId { get; private set; } = null!;
    internal string BodySha256 { get; private set; } = null!;
    internal DateTimeOffset ReceivedAt { get; private set; }
    internal string VerificationState { get; private set; } = null!;
    internal DateTimeOffset? VerifiedAt { get; private set; }
    internal string? AttestationReference { get; private set; }
    internal string? RejectionReason { get; private set; }
    internal Guid? InboxEventId { get; private set; }

    internal static ProviderEvidenceReceipt Receive(
        string? provider,
        string? providerEventId,
        string? externalSettlementId,
        string? bodySha256,
        DateTimeOffset receivedAt)
        => new()
        {
            ReceiptId = Guid.CreateVersion7(),
            Provider = ProviderPersistenceGuard.OpaqueRequired(
                provider, nameof(provider)),
            ProviderEventId = ProviderPersistenceGuard.OpaqueRequired(
                providerEventId, nameof(providerEventId)),
            ExternalSettlementId = ProviderPersistenceGuard.OpaqueRequired(
                externalSettlementId, nameof(externalSettlementId)),
            BodySha256 = ProviderPersistenceGuard.Sha256(
                bodySha256, nameof(bodySha256)),
            ReceivedAt = ProviderPersistenceGuard.RequiredInstant(
                receivedAt, nameof(receivedAt)),
            VerificationState = ProviderVerificationStates.Received
        };

    internal void MarkAuthenticated(
        Guid inboxEventId,
        DateTimeOffset verifiedAt,
        string? attestationReference)
    {
        EnsureReceived();
        if (inboxEventId == Guid.Empty)
        {
            throw new ArgumentException(
                "La evidencia autenticada exige inbox.",
                nameof(inboxEventId));
        }

        VerificationState = ProviderVerificationStates.Authenticated;
        InboxEventId = inboxEventId;
        VerifiedAt = ProviderPersistenceGuard.RequiredInstant(
            verifiedAt, nameof(verifiedAt));
        AttestationReference = ProviderPersistenceGuard.OpaqueRequired(
            attestationReference, nameof(attestationReference));
    }

    internal void MarkRejected(string? reason)
    {
        EnsureReceived();
        VerificationState = ProviderVerificationStates.Rejected;
        RejectionReason = ProviderPersistenceGuard.OpaqueRequired(
            reason, nameof(reason));
    }

    internal void MarkIndeterminate(string? reason)
    {
        EnsureReceived();
        VerificationState = ProviderVerificationStates.Indeterminate;
        RejectionReason = ProviderPersistenceGuard.OpaqueRequired(
            reason, nameof(reason));
    }

    private void EnsureReceived()
    {
        if (VerificationState != ProviderVerificationStates.Received)
        {
            throw new InvalidOperationException(
                "Una recepción ya resuelta no puede reclasificarse.");
        }
    }
}
