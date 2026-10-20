using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application.Contracts;

/// <summary>Frontera interna para ejecutar el núcleo puro P1–P16.</summary>
internal interface IProviderSettlementAnalysisService
{
    ProviderSettlementAnalysisResult Analyze(
        IEnumerable<VerifiedExternalPaymentEvidence> incoming,
        IEnumerable<VerifiedExternalPaymentEvidence> known,
        IEnumerable<ProviderEvidenceObservation> observations);
}
