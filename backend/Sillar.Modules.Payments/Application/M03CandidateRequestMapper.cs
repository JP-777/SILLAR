using Sillar.Modules.Payments.Application.Contracts;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Adapta un candidato ya completo a la forma temporal M03. Es una función
/// pura: no invoca M03, no persiste y no produce desenlaces positivos.
/// </summary>
internal static class M03CandidateRequestMapper
{
    internal static M03CandidateRequestSnapshot Map(
        ProviderCommercialEffectCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.OrderCorrelation.Identity != candidate.Identity
            || candidate.VerifiedEvidenceReference.Identity != candidate.Identity
            || !candidate.VerifiedEvidenceReference.Matches(
                candidate.Facts.Evidence))
        {
            throw new InvalidOperationException(
                "El candidato no conserva una identidad comercial coherente.");
        }

        return new(
            candidate.Identity.Reception.Provider,
            candidate.Identity.Reception.ProviderEventId,
            candidate.Identity.Settlement.Value,
            candidate.OrderCorrelation.OrderId,
            candidate.Facts.GrossAmount,
            candidate.Facts.Currency.Value,
            candidate.VerifiedEvidenceReference.Value);
    }
}
