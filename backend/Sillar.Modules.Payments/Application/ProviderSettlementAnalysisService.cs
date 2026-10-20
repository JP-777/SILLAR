using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Adaptador de aplicación del análisis puro; no añade efectos ni persistencia.
/// </summary>
internal sealed class ProviderSettlementAnalysisService
    : IProviderSettlementAnalysisService
{
    public ProviderSettlementAnalysisResult Analyze(
        IEnumerable<VerifiedExternalPaymentEvidence> incoming,
        IEnumerable<VerifiedExternalPaymentEvidence> known,
        IEnumerable<ProviderEvidenceObservation> observations)
        => ProviderSettlementAnalysisPipeline.Analyze(
            incoming,
            known,
            observations);
}
