using Sillar.Modules.Payments.Domain;

namespace Sillar.Modules.Payments.Application.Contracts;

/// <summary>
/// Conserva una observación monetaria vinculada a una recepción ya autenticada.
/// No aplica pagos ni representa el contrato físico M03↔M11.
/// </summary>
internal interface IProviderSettlementFactsSource
{
    ValueTask<ProviderSettlementFacts?> LoadCandidateFactsAsync(
        Guid inboxEventId,
        CancellationToken cancellationToken);
}

internal interface IProviderSettlementFactRepository
    : IProviderSettlementFactsSource
{
    ValueTask<ProviderSettlementFactDecision> RecordSettlementFactsAsync(
        Guid receiptId,
        ProviderSettlementFacts facts,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken);
}
