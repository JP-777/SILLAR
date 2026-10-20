using System.Security.Cryptography;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Coordina recepción, verificación y análisis sin persistencia concreta ni
/// aplicación comercial. El body sólo vive durante esta llamada.
/// </summary>
internal sealed class ProviderReceptionOrchestrator(
    IProviderEvidenceVerifier verifier,
    IProviderReceptionLedger ledger)
    : IProviderReceptionService
{
    public async ValueTask<ProviderReceptionResult> ReceiveAsync(
        ProviderReceptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bodySha256 = Convert.ToHexString(
                SHA256.HashData(request.ExactBody.Span))
            .ToLowerInvariant();
        var verification = await verifier.VerifyAsync(
            request,
            cancellationToken);
        if (verification is null || !Enum.IsDefined(verification.Outcome))
        {
            throw new InvalidOperationException(
                "El verificador devolvió un resultado inválido.");
        }

        if (verification.Outcome != ProviderVerificationOutcome.Authenticated)
        {
            var unauthenticatedState = verification.Outcome
                == ProviderVerificationOutcome.Rejected
                ? ProviderReceptionState.Rejected
                : ProviderReceptionState.Indeterminate;
            var unauthenticated = CreateRecord(
                request,
                bodySha256,
                verification,
                unauthenticatedState,
                null,
                null);
            await ledger.RecordAsync(unauthenticated, cancellationToken);
            return new(unauthenticated, null);
        }

        var evidence = VerifiedExternalPaymentEvidence.FromVerifiedWebhook(
            request.Provider,
            request.ProviderEventId,
            request.ExternalSettlementId,
            bodySha256,
            verification.VerifiedAt!.Value);
        var observations = new[]
        {
            new ProviderEvidenceObservation(
                PaymentIdempotencyKeys.From(evidence),
                ProviderEvidenceVerdict.AttestedByVerifier,
                verification.AttestationReference)
        };
        for (var persistenceAttempt = 0; persistenceAttempt < 2;
             persistenceAttempt++)
        {
            var known = await ledger.LoadKnownVerifiedEvidenceAsync(
                cancellationToken);
            var analysis = ProviderSettlementAnalysisPipeline.Analyze(
                [evidence],
                known,
                observations);
            var trace = analysis.Trace.Single();
            var state = trace.ReviewLane switch
            {
                ProviderSettlementReviewLane.Hold => ProviderReceptionState.Held,
                ProviderSettlementReviewLane.PreviouslyKnown =>
                    ProviderReceptionState.Duplicate,
                ProviderSettlementReviewLane.RequiresSeparateDurableGate =>
                    ProviderReceptionState.PendingDurableReview,
                _ => ProviderReceptionState.Indeterminate
            };
            var integrity = ProviderSettlementAnalysisIntegrity.Create(analysis);
            var authenticated = CreateRecord(
                request,
                bodySha256,
                verification,
                state,
                evidence,
                integrity.ContentSha256);

            try
            {
                await ledger.RecordAsync(authenticated, cancellationToken);
                return new(authenticated, analysis);
            }
            catch (ProviderInboxConcurrencyException)
                when (persistenceAttempt == 0)
            {
                // La restricción durable decidió la carrera. Se relee toda la
                // evidencia y se vuelve a clasificar una sola vez.
            }
        }

        throw new InvalidOperationException(
            "La recepción no pudo estabilizar su identidad concurrente.");
    }

    private static ProviderReceptionRecord CreateRecord(
        ProviderReceptionRequest request,
        string bodySha256,
        ProviderVerificationResult verification,
        ProviderReceptionState state,
        VerifiedExternalPaymentEvidence? evidence,
        string? analysisSha256)
        => new(
            Guid.CreateVersion7(),
            request.Provider,
            request.ProviderEventId,
            request.ExternalSettlementId,
            bodySha256,
            request.ReceivedAt,
            verification.Outcome,
            verification.AttestationReference,
            verification.Reason,
            state,
            evidence,
            analysisSha256);
}
