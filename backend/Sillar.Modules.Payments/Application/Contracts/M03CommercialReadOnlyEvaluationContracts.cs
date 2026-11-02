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

/// <summary>
/// Formateador cerrado de las razones persistibles que nacen de una evaluación negativa
/// M03. Todas empiezan por <see cref="Prefix"/>, que no usa ninguna barrera de preparación,
/// y salen de una tabla literal: un texto que no figura aquí no puede llevar el prefijo.
/// </summary>
internal static class M03ReadOnlyNegativeReason
{
    internal const string Prefix = "m03ro:";

    /// <summary>El puerto de evaluación no respondió o lanzó: técnico, nunca comercial.</summary>
    internal const string EvaluationUnavailable = Prefix + "technical/evaluation_unavailable";

    /// <summary>La evaluación no corresponde a ninguna combinación del contrato.</summary>
    internal const string InconsistentEvaluation = Prefix + "technical/inconsistent_evaluation";

    private static readonly Dictionary<
        (CommercialReadOnlyEvaluationVerdict Verdict, CommercialReadOnlyEvaluationReason Reason),
        (M03CommercialReadOnlyEvaluationOutcome Outcome, string Code)> Commercial = new()
        {
            [(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.NotAuthoritative)] =
                (M03CommercialReadOnlyEvaluationOutcome.Rejected, Prefix + "rejected/not_authoritative"),
            [(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.MalformedReference)] =
                (M03CommercialReadOnlyEvaluationOutcome.Rejected, Prefix + "rejected/malformed_reference"),
            [(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.UnknownReference)] =
                (M03CommercialReadOnlyEvaluationOutcome.Rejected, Prefix + "rejected/unknown_reference"),
            [(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.DivergentDeclaration)] =
                (M03CommercialReadOnlyEvaluationOutcome.Rejected, Prefix + "rejected/divergent_declaration"),
            [(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.InactiveOrder)] =
                (M03CommercialReadOnlyEvaluationOutcome.Rejected, Prefix + "rejected/inactive_order"),
            [(CommercialReadOnlyEvaluationVerdict.Rejected, CommercialReadOnlyEvaluationReason.InvalidAmount)] =
                (M03CommercialReadOnlyEvaluationOutcome.Rejected, Prefix + "rejected/invalid_amount"),
            [(CommercialReadOnlyEvaluationVerdict.PendingDecision, CommercialReadOnlyEvaluationReason.OrderLifecycleUnratified)] =
                (M03CommercialReadOnlyEvaluationOutcome.DecisionRequired, Prefix + "decision_required/order_lifecycle_unratified"),
            [(CommercialReadOnlyEvaluationVerdict.PendingDecision, CommercialReadOnlyEvaluationReason.AlreadySettledManually)] =
                (M03CommercialReadOnlyEvaluationOutcome.DecisionRequired, Prefix + "decision_required/already_settled_manually"),
            [(CommercialReadOnlyEvaluationVerdict.PendingDecision, CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing)] =
                (M03CommercialReadOnlyEvaluationOutcome.DecisionRequired, Prefix + "decision_required/currency_authority_missing"),
            [(CommercialReadOnlyEvaluationVerdict.Inconsistent, CommercialReadOnlyEvaluationReason.ContradictoryFacts)] =
                (M03CommercialReadOnlyEvaluationOutcome.Conflict, Prefix + "conflict/contradictory_facts"),
            [(CommercialReadOnlyEvaluationVerdict.Inconsistent, CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads)] =
                (M03CommercialReadOnlyEvaluationOutcome.Indeterminate, Prefix + "indeterminate/order_changed_between_reads"),
            [(CommercialReadOnlyEvaluationVerdict.Inconsistent, CommercialReadOnlyEvaluationReason.UnexpectedPolicyAnswer)] =
                (M03CommercialReadOnlyEvaluationOutcome.Indeterminate, Prefix + "indeterminate/unexpected_policy_answer"),
        };

    /// <summary>Las causas técnicas que publica el consumidor M11, y solo esas.</summary>
    private static readonly Dictionary<string, string> Technical = new(StringComparer.Ordinal)
    {
        ["m03_readonly_evaluation_null_result"] = Prefix + "technical/null_result",
        ["m03_readonly_evaluation_unknown_verdict"] = Prefix + "technical/unknown_verdict",
        ["m03_readonly_evaluation_unknown_reason"] = Prefix + "technical/unknown_reason",
        ["m03_readonly_evaluation_invalid_correlation"] = Prefix + "technical/invalid_correlation",
        ["m03_readonly_evaluation_invalid_verdict_reason_pair"] = Prefix + "technical/invalid_verdict_reason_pair",
        ["m03_readonly_evaluation_invalid_facts"] = Prefix + "technical/invalid_facts",
        ["m03_readonly_evaluation_call_failed"] = Prefix + "technical/call_failed",
    };

    /// <summary>Todo el vocabulario cerrado, para comprobar longitudes y forma.</summary>
    internal static IReadOnlyCollection<string> AllCodes =>
        [.. Commercial.Values.Select(entry => entry.Code), .. Technical.Values, EvaluationUnavailable, InconsistentEvaluation];

    /// <summary>
    /// Traduce una evaluación al código cerrado. Devuelve <c>false</c> si el resultado no
    /// pertenece al contrato: pareja desconocida, desenlace que no le corresponde, causa
    /// técnica ajena o mezcla de causa técnica con veredicto, correlación o hechos.
    /// </summary>
    internal static bool TryFormat(
        M03CommercialReadOnlyEvaluationResult result,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(result);
        reason = InconsistentEvaluation;

        if (result.TechnicalReason is { } technical)
        {
            if (result.Outcome is not M03CommercialReadOnlyEvaluationOutcome.Indeterminate
                || result.SourceVerdict is not null
                || result.SourceReason is not null
                || result.Correlation is not null
                || result.Facts is not null
                || !Technical.TryGetValue(technical, out var technicalCode))
            {
                return false;
            }

            reason = technicalCode;
            return true;
        }

        if (result.SourceVerdict is not { } verdict
            || result.SourceReason is not { } sourceReason
            || !Commercial.TryGetValue((verdict, sourceReason), out var entry)
            || entry.Outcome != result.Outcome)
        {
            return false;
        }

        reason = entry.Code;
        return true;
    }

    internal static bool IsM03ReadOnly(string? reason)
        => reason is not null && reason.StartsWith(Prefix, StringComparison.Ordinal);
}
