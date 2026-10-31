using System.Diagnostics;
using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Adaptador candidato y no registrado hacia la evaluación sin efectos de M03.
/// Solo transporta observaciones; no consulta el aplicador, el journal ni el inbox.
/// </summary>
internal sealed class M03CommercialReadOnlyEvaluationPortAdapter(
    ICommercialReadOnlyEvaluationCandidate candidate)
    : IM03CommercialReadOnlyEvaluationPort
{
    public async ValueTask<M03CommercialReadOnlyEvaluationResult> EvaluateAsync(
        M03OrderCorrelationRequestSnapshot request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await candidate.EvaluateAsync(
                new OrderCorrelationRequest(
                    request.DeclaredOrderCode,
                    request.DeclaredOrderId),
                cancellationToken).ConfigureAwait(false);

            return M03CommercialReadOnlyEvaluationMapper.Map(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // La causa pública es deliberadamente saneada. Un fallo de transporte
            // no se disfraza como rechazo de negocio ni habilita un reintento.
            return M03CommercialReadOnlyEvaluationResult.TechnicalFailure(
                "m03_readonly_evaluation_call_failed");
        }
    }
}

/// <summary>
/// Valida el DTO público hostil y lo proyecta por veredicto + razón. No reproduce
/// la política comercial: comprueba únicamente las invariantes del transporte.
/// </summary>
internal static class M03CommercialReadOnlyEvaluationMapper
{
    internal static M03CommercialReadOnlyEvaluationResult Map(
        CommercialReadOnlyEvaluationResult? source)
    {
        if (source is null)
        {
            return Invalid("m03_readonly_evaluation_null_result");
        }

        if (!Enum.IsDefined(source.Verdict))
        {
            return Invalid("m03_readonly_evaluation_unknown_verdict");
        }

        if (!Enum.IsDefined(source.Reason))
        {
            return Invalid("m03_readonly_evaluation_unknown_reason");
        }

        if (!TryValidateCorrelation(source.Correlation, out var correlation))
        {
            return Invalid("m03_readonly_evaluation_invalid_correlation");
        }

        if (!IsAllowedPair(source.Verdict, source.Reason)
            || !HasExpectedShape(source, correlation!))
        {
            return Invalid("m03_readonly_evaluation_invalid_verdict_reason_pair");
        }

        if (!TryMapFacts(source.Facts, source.Reason, out var facts))
        {
            return Invalid("m03_readonly_evaluation_invalid_facts");
        }

        var outcome = source.Reason switch
        {
            CommercialReadOnlyEvaluationReason.NotAuthoritative
                or CommercialReadOnlyEvaluationReason.MalformedReference
                or CommercialReadOnlyEvaluationReason.UnknownReference
                or CommercialReadOnlyEvaluationReason.DivergentDeclaration
                or CommercialReadOnlyEvaluationReason.InactiveOrder
                or CommercialReadOnlyEvaluationReason.InvalidAmount =>
                    M03CommercialReadOnlyEvaluationOutcome.Rejected,

            CommercialReadOnlyEvaluationReason.OrderLifecycleUnratified
                or CommercialReadOnlyEvaluationReason.AlreadySettledManually
                or CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing =>
                    M03CommercialReadOnlyEvaluationOutcome.DecisionRequired,

            CommercialReadOnlyEvaluationReason.ContradictoryFacts =>
                M03CommercialReadOnlyEvaluationOutcome.Conflict,

            CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads
                or CommercialReadOnlyEvaluationReason.UnexpectedPolicyAnswer =>
                    M03CommercialReadOnlyEvaluationOutcome.Indeterminate,

            _ => throw new UnreachableException()
        };

        return new(
            outcome,
            source.Verdict,
            source.Reason,
            correlation,
            facts,
            null);
    }

    private static bool IsAllowedPair(
        CommercialReadOnlyEvaluationVerdict verdict,
        CommercialReadOnlyEvaluationReason reason) => reason switch
        {
            CommercialReadOnlyEvaluationReason.NotAuthoritative
                or CommercialReadOnlyEvaluationReason.MalformedReference
                or CommercialReadOnlyEvaluationReason.UnknownReference
                or CommercialReadOnlyEvaluationReason.DivergentDeclaration
                or CommercialReadOnlyEvaluationReason.InactiveOrder
                or CommercialReadOnlyEvaluationReason.InvalidAmount =>
                    verdict is CommercialReadOnlyEvaluationVerdict.Rejected,

            CommercialReadOnlyEvaluationReason.OrderLifecycleUnratified
                or CommercialReadOnlyEvaluationReason.AlreadySettledManually
                or CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing =>
                    verdict is CommercialReadOnlyEvaluationVerdict.PendingDecision,

            CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads
                or CommercialReadOnlyEvaluationReason.ContradictoryFacts
                or CommercialReadOnlyEvaluationReason.UnexpectedPolicyAnswer =>
                    verdict is CommercialReadOnlyEvaluationVerdict.Inconsistent,

            _ => false
        };

    private static bool HasExpectedShape(
        CommercialReadOnlyEvaluationResult source,
        M03OrderCorrelationResultSnapshot correlation)
    {
        var factsPresent = source.Facts is not null;
        return source.Reason switch
        {
            CommercialReadOnlyEvaluationReason.NotAuthoritative =>
                correlation.Outcome is M03OrderCorrelationOutcome.NotAuthoritative
                && !factsPresent,
            CommercialReadOnlyEvaluationReason.MalformedReference =>
                correlation.Outcome is M03OrderCorrelationOutcome.MalformedReference
                && !factsPresent,
            CommercialReadOnlyEvaluationReason.UnknownReference =>
                correlation.Outcome is M03OrderCorrelationOutcome.UnknownReference
                && !factsPresent,
            CommercialReadOnlyEvaluationReason.DivergentDeclaration =>
                correlation.Outcome is M03OrderCorrelationOutcome.DivergentDeclaration
                && !factsPresent,
            CommercialReadOnlyEvaluationReason.InactiveOrder
                or CommercialReadOnlyEvaluationReason.InvalidAmount
                or CommercialReadOnlyEvaluationReason.OrderLifecycleUnratified
                or CommercialReadOnlyEvaluationReason.AlreadySettledManually
                or CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing =>
                    correlation.Outcome is M03OrderCorrelationOutcome.Resolved
                    && factsPresent,
            CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads =>
                correlation.Outcome is M03OrderCorrelationOutcome.Resolved
                && !factsPresent,
            CommercialReadOnlyEvaluationReason.ContradictoryFacts =>
                correlation.Outcome is M03OrderCorrelationOutcome.Resolved,
            CommercialReadOnlyEvaluationReason.UnexpectedPolicyAnswer => true,
            _ => false
        };
    }

    private static bool TryValidateCorrelation(
        OrderCorrelationCandidate? source,
        out M03OrderCorrelationResultSnapshot? result)
    {
        result = null;
        if (source is null || !Enum.IsDefined(source.Outcome))
        {
            return false;
        }

        var outcome = source.Outcome switch
        {
            OrderCorrelationOutcome.NotAuthoritative =>
                M03OrderCorrelationOutcome.NotAuthoritative,
            OrderCorrelationOutcome.MalformedReference =>
                M03OrderCorrelationOutcome.MalformedReference,
            OrderCorrelationOutcome.UnknownReference =>
                M03OrderCorrelationOutcome.UnknownReference,
            OrderCorrelationOutcome.DivergentDeclaration =>
                M03OrderCorrelationOutcome.DivergentDeclaration,
            OrderCorrelationOutcome.Resolved =>
                M03OrderCorrelationOutcome.Resolved,
            _ => throw new UnreachableException()
        };

        var resolved = outcome is M03OrderCorrelationOutcome.Resolved;
        var identityIsValid = resolved
            ? source.OrderId is { } id
                && id != Guid.Empty
                && !string.IsNullOrWhiteSpace(source.OrderCode)
                && StringComparer.Ordinal.Equals(
                    source.Source,
                    CandidateSources.CurrentDatabase)
            : source.OrderId is null
                && source.OrderCode is null
                && ExpectedNegativeSource(outcome, source.Source);

        if (!identityIsValid)
        {
            return false;
        }

        result = new(outcome, source.OrderId, source.OrderCode, source.Source);
        return true;
    }

    private static bool ExpectedNegativeSource(
        M03OrderCorrelationOutcome outcome,
        string? source) => outcome switch
        {
            M03OrderCorrelationOutcome.NotAuthoritative
                or M03OrderCorrelationOutcome.MalformedReference =>
                    StringComparer.Ordinal.Equals(
                        source,
                        CandidateSources.RatifiedRules),
            M03OrderCorrelationOutcome.UnknownReference
                or M03OrderCorrelationOutcome.DivergentDeclaration =>
                    StringComparer.Ordinal.Equals(
                        source,
                        CandidateSources.CurrentDatabase),
            _ => false
        };

    private static bool TryMapFacts(
        CommercialReadOnlyOrderFacts? source,
        CommercialReadOnlyEvaluationReason reason,
        out M03CommercialReadOnlyOrderFactsSnapshot? result)
    {
        result = null;
        if (source is null)
        {
            return true;
        }

        if (!Enum.IsDefined(source.OrderCurrency)
            || string.IsNullOrWhiteSpace(source.Status)
            || source.StaffPaymentCount < 0
            || source.UnattributedHistoricalCount < 0
            || source.UnratifiedSourceCount < 0
            || source.PaymentsWithoutCurrencyCount < 0
            || source.PaymentsWithoutCurrencyCount
                > source.StaffPaymentCount
                    + source.UnattributedHistoricalCount
                    + source.UnratifiedSourceCount
            || !CountAndTotalAgree(
                source.StaffPaymentCount,
                source.StaffRecordedTotal)
            || !CountAndTotalAgree(
                source.UnattributedHistoricalCount,
                source.UnattributedHistoricalTotal)
            || !CountAndTotalAgree(
                source.UnratifiedSourceCount,
                source.UnratifiedSourceTotal)
            || !CurrencyShapeIsValid(
                source.OrderCurrency,
                source.OrderCurrencyCode))
        {
            return false;
        }

        if (reason is CommercialReadOnlyEvaluationReason.InvalidAmount
                ? source.FrozenTotal >= 0
                : source.FrozenTotal < 0)
        {
            return false;
        }

        // Datos contradictorios son justamente el contenido de Conflict. En los
        // demás resultados, una fuente no ratificada o agregados negativos serían
        // una contradicción encubierta y no una observación comercial ordinaria.
        if (reason is not CommercialReadOnlyEvaluationReason.ContradictoryFacts
            && (source.StaffRecordedTotal < 0
                || source.UnattributedHistoricalTotal < 0
                || source.UnratifiedSourceTotal < 0
                || source.UnratifiedSourceCount != 0
                || source.OrderCurrency is CommercialReadOnlyCurrencyObservation.MalformedValue))
        {
            return false;
        }

        var currency = source.OrderCurrency switch
        {
            CommercialReadOnlyCurrencyObservation.Absent =>
                M03CommercialReadOnlyCurrencyObservation.Absent,
            CommercialReadOnlyCurrencyObservation.MalformedValue =>
                M03CommercialReadOnlyCurrencyObservation.MalformedValue,
            CommercialReadOnlyCurrencyObservation.PresentWithoutRatifiedAuthority =>
                M03CommercialReadOnlyCurrencyObservation.PresentWithoutRatifiedAuthority,
            _ => throw new UnreachableException()
        };

        result = new(
            source.Status,
            source.FrozenTotal,
            source.IsActive,
            source.OrderCurrencyCode,
            currency,
            source.StaffRecordedTotal,
            source.StaffPaymentCount,
            source.UnattributedHistoricalTotal,
            source.UnattributedHistoricalCount,
            source.UnratifiedSourceTotal,
            source.UnratifiedSourceCount,
            source.PaymentsWithoutCurrencyCount);
        return true;
    }

    private static bool CountAndTotalAgree(int count, decimal total)
        => count != 0 || total == 0;

    private static bool CurrencyShapeIsValid(
        CommercialReadOnlyCurrencyObservation observation,
        string? currency) => observation switch
        {
            CommercialReadOnlyCurrencyObservation.Absent => currency is null,
            CommercialReadOnlyCurrencyObservation.MalformedValue =>
                !string.IsNullOrEmpty(currency) && !IsIsoShape(currency),
            CommercialReadOnlyCurrencyObservation.PresentWithoutRatifiedAuthority =>
                IsIsoShape(currency),
            _ => false
        };

    private static bool IsIsoShape(string? currency)
        => currency is { Length: 3 }
            && currency.All(character => character is >= 'A' and <= 'Z');

    private static M03CommercialReadOnlyEvaluationResult Invalid(string reason)
        => M03CommercialReadOnlyEvaluationResult.TechnicalFailure(reason);
}
