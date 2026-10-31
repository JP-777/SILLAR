using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Payments.Application.Contracts;

/// <summary>
/// Puerto no registrado para consumir únicamente la observación comercial
/// negativa de M03. No es un puerto de aplicación y no publica estados positivos.
/// </summary>
internal interface IM03CommercialReadOnlyEvaluationPort
{
    ValueTask<M03CommercialReadOnlyEvaluationResult> EvaluateAsync(
        M03OrderCorrelationRequestSnapshot request,
        CancellationToken cancellationToken);
}

internal enum M03CommercialReadOnlyEvaluationOutcome
{
    Rejected,
    DecisionRequired,
    Conflict,
    Indeterminate
}

internal enum M03CommercialReadOnlyCurrencyObservation
{
    Absent,
    MalformedValue,
    PresentWithoutRatifiedAuthority
}

/// <summary>
/// Hechos observados por M03. No contiene saldo, autenticación, referencia durable
/// ni autorización de liquidación.
/// </summary>
internal sealed record M03CommercialReadOnlyOrderFactsSnapshot(
    string Status,
    decimal FrozenTotal,
    bool IsActive,
    string? OrderCurrencyCode,
    M03CommercialReadOnlyCurrencyObservation OrderCurrency,
    decimal StaffRecordedTotal,
    int StaffPaymentCount,
    decimal UnattributedHistoricalTotal,
    int UnattributedHistoricalCount,
    decimal UnratifiedSourceTotal,
    int UnratifiedSourceCount,
    int PaymentsWithoutCurrencyCount);

/// <summary>
/// Proyección cerrada del resultado público. Conserva veredicto y razón M03
/// tipados, pero solo expone desenlaces negativos dentro de M11.
/// </summary>
internal sealed record M03CommercialReadOnlyEvaluationResult(
    M03CommercialReadOnlyEvaluationOutcome Outcome,
    CommercialReadOnlyEvaluationVerdict? SourceVerdict,
    CommercialReadOnlyEvaluationReason? SourceReason,
    M03OrderCorrelationResultSnapshot? Correlation,
    M03CommercialReadOnlyOrderFactsSnapshot? Facts,
    string? TechnicalReason)
{
    internal static M03CommercialReadOnlyEvaluationResult TechnicalFailure(
        string reason) => new(
            M03CommercialReadOnlyEvaluationOutcome.Indeterminate,
            null,
            null,
            null,
            null,
            reason);
}
