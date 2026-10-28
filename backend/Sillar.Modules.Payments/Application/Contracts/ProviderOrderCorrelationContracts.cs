using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application.Contracts;

/// <summary>
/// Declaración de pedido extraída por un verificador de proveedor y ligada a
/// la misma recepción autenticada. No se construye desde event_id, settlement
/// ni referencias libres del pagador.
/// </summary>
internal sealed record ProviderAuthenticatedOrderDeclaration
{
    private ProviderAuthenticatedOrderDeclaration(
        Guid inboxEventId,
        Guid receiptId,
        PaymentIdempotencyKeys identity,
        string bodySha256,
        DateTimeOffset verifiedAt,
        string attestationReference,
        string? declaredOrderCode,
        Guid? declaredOrderId)
    {
        InboxEventId = inboxEventId;
        ReceiptId = receiptId;
        Identity = identity;
        BodySha256 = bodySha256;
        VerifiedAt = verifiedAt;
        AttestationReference = attestationReference;
        DeclaredOrderCode = declaredOrderCode;
        DeclaredOrderId = declaredOrderId;
    }

    internal Guid InboxEventId { get; }
    internal Guid ReceiptId { get; }
    internal PaymentIdempotencyKeys Identity { get; }
    internal string BodySha256 { get; }
    internal DateTimeOffset VerifiedAt { get; }
    internal string AttestationReference { get; }
    internal string? DeclaredOrderCode { get; }
    internal Guid? DeclaredOrderId { get; }

    internal static ProviderAuthenticatedOrderDeclaration Create(
        Guid inboxEventId,
        ProviderVerifiedEvidenceReference evidenceReference,
        string? declaredOrderCode,
        Guid? declaredOrderId)
    {
        if (inboxEventId == Guid.Empty)
        {
            throw new ArgumentException(
                "La declaración autenticada exige inbox.",
                nameof(inboxEventId));
        }

        ArgumentNullException.ThrowIfNull(evidenceReference);
        if (declaredOrderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Guid.Empty no identifica un pedido.",
                nameof(declaredOrderId));
        }

        if (string.IsNullOrWhiteSpace(declaredOrderCode)
            && declaredOrderId is null)
        {
            throw new ArgumentException(
                "La declaración exige código o identificador de pedido.");
        }

        return new(
            inboxEventId,
            evidenceReference.ReceiptId,
            evidenceReference.Identity,
            evidenceReference.BodySha256,
            evidenceReference.VerifiedAt,
            evidenceReference.Value,
            declaredOrderCode,
            declaredOrderId);
    }

    internal bool Matches(
        Guid inboxEventId,
        ProviderVerifiedEvidenceReference evidenceReference)
    {
        ArgumentNullException.ThrowIfNull(evidenceReference);
        return InboxEventId == inboxEventId
            && ReceiptId == evidenceReference.ReceiptId
            && Identity == evidenceReference.Identity
            && StringComparer.Ordinal.Equals(
                BodySha256,
                evidenceReference.BodySha256)
            && VerifiedAt == evidenceReference.VerifiedAt
            && StringComparer.Ordinal.Equals(
                AttestationReference,
                evidenceReference.Value);
    }
}

internal interface IProviderAuthenticatedOrderDeclarationSource
{
    ValueTask<IReadOnlyList<ProviderAuthenticatedOrderDeclaration>> LoadAsync(
        Guid inboxEventId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Vista local del request candidato M03. El adaptador físico se incorpora
/// cuando IOrderCorrelationCandidate esté presente en la misma base integrada.
/// </summary>
internal sealed record M03OrderCorrelationRequestSnapshot(
    string? DeclaredOrderCode,
    Guid? DeclaredOrderId);

internal enum M03OrderCorrelationOutcome
{
    NotAuthoritative,
    MalformedReference,
    UnknownReference,
    DivergentDeclaration,
    Resolved
}

internal sealed record M03OrderCorrelationResultSnapshot(
    M03OrderCorrelationOutcome Outcome,
    Guid? OrderId,
    string? OrderCode,
    string? Source);

/// <summary>
/// Puerto candidato no registrado. Conserva la costura de FASE A sin copiar
/// una implementación M03 ni habilitar lecturas o efectos productivos.
/// </summary>
internal interface IM03OrderCorrelationCandidatePort
{
    ValueTask<M03OrderCorrelationResultSnapshot?> ResolveAsync(
        M03OrderCorrelationRequestSnapshot request,
        CancellationToken cancellationToken);
}
