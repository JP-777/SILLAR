using Sillar.Modules.Payments.Application.Contracts;
using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Costura candidata y no registrada entre el contrato público de correlación
/// de M03 y la vista consumidora interna de M11. No autentica declaraciones ni
/// juzga pagabilidad: esas precondiciones permanecen fuera de este adaptador.
/// </summary>
internal sealed class M03OrderCorrelationCandidatePortAdapter(
    IOrderCorrelationCandidate candidate)
    : IM03OrderCorrelationCandidatePort
{
    public async ValueTask<M03OrderCorrelationResultSnapshot?> ResolveAsync(
        M03OrderCorrelationRequestSnapshot request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await candidate.ResolveAsync(
            new OrderCorrelationRequest(
                request.DeclaredOrderCode,
                request.DeclaredOrderId),
            cancellationToken);
        if (result is null)
        {
            return null;
        }

        var outcome = result.Outcome switch
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
            _ => (M03OrderCorrelationOutcome)(-1)
        };

        if (!Enum.IsDefined(outcome))
        {
            return null;
        }

        return new(
            outcome,
            result.OrderId,
            result.OrderCode,
            result.Source);
    }
}
