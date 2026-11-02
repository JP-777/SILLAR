using Sillar.Modules.Payments.Application.Contracts;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Puerto de efectos negativo sobre la evaluación sin efectos de M03. Interno y sin
/// registro productivo: existe para componer el inbox en pruebas sin un doble ad hoc.
/// </summary>
/// <remarks>
/// <b>Evalúa, no aplica.</b> Traduce uno a uno los cuatro desenlaces negativos de M11 y no
/// tiene camino hacia <c>Applied</c>, <c>AlreadyApplied</c> ni hacia una referencia
/// comercial. No reclasifica: la precedencia es la que M03 y el consumidor M11 ya
/// resolvieron. No compara importes ni monedas del proveedor, que es tarea de aplicación.
/// Todo lo que no encaja en el contrato termina en Indeterminate técnico, con razón cerrada.
/// </remarks>
internal sealed class M03ReadOnlyNegativeEffectPortAdapter(
    IM03CommercialReadOnlyEvaluationPort evaluation)
    : IProviderCommercialEffectPort
{
    public async ValueTask<ProviderCommercialEffectResult> TryApplyAsync(
        ProviderCommercialEffectCandidate candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        M03CommercialReadOnlyEvaluationResult? result;
        try
        {
            result = await evaluation.EvaluateAsync(
                new M03OrderCorrelationRequestSnapshot(
                    candidate.OrderCorrelation.OrderCode,
                    candidate.OrderCorrelation.OrderId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Saneado: el detalle del fallo no viaja a una razón persistible.
            return ProviderCommercialEffectResult.Indeterminate(
                M03ReadOnlyNegativeReason.EvaluationUnavailable);
        }

        if (result is null)
        {
            return ProviderCommercialEffectResult.Indeterminate(
                M03ReadOnlyNegativeReason.EvaluationUnavailable);
        }

        if (!M03ReadOnlyNegativeReason.TryFormat(result, out var reason))
        {
            return ProviderCommercialEffectResult.Indeterminate(
                M03ReadOnlyNegativeReason.InconsistentEvaluation);
        }

        return result.Outcome switch
        {
            M03CommercialReadOnlyEvaluationOutcome.Rejected =>
                ProviderCommercialEffectResult.Rejected(reason),
            M03CommercialReadOnlyEvaluationOutcome.DecisionRequired =>
                ProviderCommercialEffectResult.DecisionRequired(reason),
            M03CommercialReadOnlyEvaluationOutcome.Conflict =>
                ProviderCommercialEffectResult.Conflict(reason),
            M03CommercialReadOnlyEvaluationOutcome.Indeterminate =>
                ProviderCommercialEffectResult.Indeterminate(reason),
            _ => ProviderCommercialEffectResult.Indeterminate(
                M03ReadOnlyNegativeReason.InconsistentEvaluation),
        };
    }
}
