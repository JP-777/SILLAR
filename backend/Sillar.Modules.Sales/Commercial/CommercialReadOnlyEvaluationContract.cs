using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;

namespace Sillar.Modules.Sales.Commercial;

/// <summary>
/// Traduce la observación interna de la evaluación a su forma pública.
/// </summary>
/// <remarks>
/// <para>
/// <b>Traduce, no decide.</b> Cada veredicto, razón y observación de moneda tiene su
/// gemelo público con el mismo nombre, y se traduce por nombre explícito, nunca por
/// valor numérico: reordenar un enum no puede cambiar el significado en silencio.
/// </para>
/// <para>
/// <b>Lo imposible falla, no se disfraza.</b> Un valor que no existía al escribir esto,
/// o un veredicto que no corresponde a su razón, lanza
/// <see cref="InvalidOperationException"/>. Convertirlo en un rechazo aparentemente
/// válido fabricaría una respuesta comercial que nadie dio.
/// </para>
/// <para>
/// <b>Lo que no se transporta, y por qué es seguro:</b>
/// la pagabilidad interna repite veredicto y razón, salvo en
/// <see cref="CommercialEvaluationReason.UnexpectedPolicyAnswer"/>, donde es justo la
/// respuesta que se rehúsa propagar —podría ser un positivo—; su
/// <c>ManuallySettledTotal</c> suma históricos con personal, que es lo que los hechos
/// separan; la identidad de los hechos es la de la correlación, porque si no lo fuera
/// no habría hechos; y el saldo es siempre nulo.
/// </para>
/// </remarks>
internal static class CommercialReadOnlyEvaluationProjection
{
    /// <summary>Proyecta una observación interna, o lanza si es imposible.</summary>
    public static CommercialReadOnlyEvaluationResult Proyectar(CommercialEvaluationObservation observacion)
    {
        ArgumentNullException.ThrowIfNull(observacion);
        ArgumentNullException.ThrowIfNull(observacion.Correlation);

        var veredicto = Veredicto(observacion.Verdict);
        var razon = Razon(observacion.Reason);

        ComprobarCoherencia(observacion, veredicto, razon);

        return new CommercialReadOnlyEvaluationResult(
            veredicto,
            razon,
            observacion.Correlation,
            observacion.Facts is { } hechos ? Hechos(hechos) : null);
    }

    private static CommercialReadOnlyEvaluationVerdict Veredicto(CommercialEvaluationVerdict veredicto) => veredicto switch
    {
        CommercialEvaluationVerdict.Rejected => CommercialReadOnlyEvaluationVerdict.Rejected,
        CommercialEvaluationVerdict.PendingDecision => CommercialReadOnlyEvaluationVerdict.PendingDecision,
        CommercialEvaluationVerdict.Inconsistent => CommercialReadOnlyEvaluationVerdict.Inconsistent,
        _ => throw Imposible($"veredicto interno desconocido {(int)veredicto}"),
    };

    private static CommercialReadOnlyEvaluationReason Razon(CommercialEvaluationReason razon) => razon switch
    {
        CommercialEvaluationReason.NotAuthoritative => CommercialReadOnlyEvaluationReason.NotAuthoritative,
        CommercialEvaluationReason.MalformedReference => CommercialReadOnlyEvaluationReason.MalformedReference,
        CommercialEvaluationReason.UnknownReference => CommercialReadOnlyEvaluationReason.UnknownReference,
        CommercialEvaluationReason.DivergentDeclaration => CommercialReadOnlyEvaluationReason.DivergentDeclaration,
        CommercialEvaluationReason.InactiveOrder => CommercialReadOnlyEvaluationReason.InactiveOrder,
        CommercialEvaluationReason.InvalidAmount => CommercialReadOnlyEvaluationReason.InvalidAmount,
        CommercialEvaluationReason.OrderLifecycleUnratified => CommercialReadOnlyEvaluationReason.OrderLifecycleUnratified,
        CommercialEvaluationReason.AlreadySettledManually => CommercialReadOnlyEvaluationReason.AlreadySettledManually,
        CommercialEvaluationReason.CurrencyAuthorityMissing => CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing,
        CommercialEvaluationReason.OrderChangedBetweenReads => CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads,
        CommercialEvaluationReason.ContradictoryFacts => CommercialReadOnlyEvaluationReason.ContradictoryFacts,
        CommercialEvaluationReason.UnexpectedPolicyAnswer => CommercialReadOnlyEvaluationReason.UnexpectedPolicyAnswer,
        _ => throw Imposible($"razón interna desconocida {(int)razon}"),
    };

    private static CommercialReadOnlyCurrencyObservation Moneda(CurrencyObservation moneda) => moneda switch
    {
        CurrencyObservation.Absent => CommercialReadOnlyCurrencyObservation.Absent,
        CurrencyObservation.MalformedValue => CommercialReadOnlyCurrencyObservation.MalformedValue,
        CurrencyObservation.PresentWithoutRatifiedAuthority => CommercialReadOnlyCurrencyObservation.PresentWithoutRatifiedAuthority,
        _ => throw Imposible($"observación de moneda desconocida {(int)moneda}"),
    };

    /// <remarks>
    /// Las combinaciones que la evaluación produce, y nada más. Una razón de correlación
    /// llega rechazada, con su mismo desenlace de correlación y sin hechos; una de
    /// pagabilidad llega con el pedido resuelto y leído.
    /// </remarks>
    private static void ComprobarCoherencia(
        CommercialEvaluationObservation observacion,
        CommercialReadOnlyEvaluationVerdict veredicto,
        CommercialReadOnlyEvaluationReason razon)
    {
        var correlacion = observacion.Correlation.Outcome;
        var resuelto = correlacion is OrderCorrelationOutcome.Resolved;
        var conHechos = observacion.Facts is not null;

        var coherente = razon switch
        {
            CommercialReadOnlyEvaluationReason.NotAuthoritative =>
                Rechazo(veredicto, conHechos) && correlacion is OrderCorrelationOutcome.NotAuthoritative,
            CommercialReadOnlyEvaluationReason.MalformedReference =>
                Rechazo(veredicto, conHechos) && correlacion is OrderCorrelationOutcome.MalformedReference,
            CommercialReadOnlyEvaluationReason.UnknownReference =>
                Rechazo(veredicto, conHechos) && correlacion is OrderCorrelationOutcome.UnknownReference,
            CommercialReadOnlyEvaluationReason.DivergentDeclaration =>
                Rechazo(veredicto, conHechos) && correlacion is OrderCorrelationOutcome.DivergentDeclaration,

            CommercialReadOnlyEvaluationReason.InactiveOrder or CommercialReadOnlyEvaluationReason.InvalidAmount =>
                veredicto is CommercialReadOnlyEvaluationVerdict.Rejected && resuelto && conHechos,

            CommercialReadOnlyEvaluationReason.OrderLifecycleUnratified
                or CommercialReadOnlyEvaluationReason.AlreadySettledManually
                or CommercialReadOnlyEvaluationReason.CurrencyAuthorityMissing =>
                veredicto is CommercialReadOnlyEvaluationVerdict.PendingDecision && resuelto && conHechos,

            CommercialReadOnlyEvaluationReason.OrderChangedBetweenReads =>
                veredicto is CommercialReadOnlyEvaluationVerdict.Inconsistent && resuelto && !conHechos,

            CommercialReadOnlyEvaluationReason.ContradictoryFacts =>
                veredicto is CommercialReadOnlyEvaluationVerdict.Inconsistent && resuelto,

            CommercialReadOnlyEvaluationReason.UnexpectedPolicyAnswer =>
                veredicto is CommercialReadOnlyEvaluationVerdict.Inconsistent,

            _ => false,
        };

        if (!coherente)
        {
            throw Imposible($"combinación imposible: {veredicto}/{razon} con correlación {correlacion} y hechos={conHechos}");
        }
    }

    private static bool Rechazo(CommercialReadOnlyEvaluationVerdict veredicto, bool conHechos)
        => veredicto is CommercialReadOnlyEvaluationVerdict.Rejected && !conHechos;

    private static CommercialReadOnlyOrderFacts Hechos(CommercialFactsObservation hechos) => new(
        hechos.Status,
        hechos.FrozenTotal,
        hechos.IsActive,
        hechos.OrderCurrencyCode,
        Moneda(hechos.OrderCurrency),
        hechos.StaffRecordedTotal,
        hechos.StaffPaymentCount,
        hechos.UnattributedHistoricalTotal,
        hechos.UnattributedHistoricalCount,
        hechos.UnratifiedSourceTotal,
        hechos.UnratifiedSourceCount,
        hechos.PaymentsWithoutCurrencyCount);

    private static InvalidOperationException Imposible(string detalle)
        => new($"La evaluación comercial devolvió una respuesta imposible: {detalle}.");
}

/// <summary>
/// La frontera pública de la evaluación comercial, sobre el evaluador real.
/// </summary>
/// <remarks>
/// <b>Solo invoca y traduce.</b> Las consultas y las reglas siguen en
/// <see cref="CandidateCommercialReadOnlyEvaluator"/>; aquí no hay ni una ni otra. No
/// depende del journal, del aplicador ni de nada de Pagos. Una cancelación o un fallo
/// de lectura pasan tal cual: no se convierten en respuesta comercial.
/// </remarks>
internal sealed class CandidateCommercialReadOnlyEvaluationAdapter : ICommercialReadOnlyEvaluationCandidate
{
    private readonly CandidateCommercialReadOnlyEvaluator evaluator;

    public CandidateCommercialReadOnlyEvaluationAdapter(CandidateCommercialReadOnlyEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        this.evaluator = evaluator;
    }

    /// <inheritdoc />
    public async Task<CommercialReadOnlyEvaluationResult> EvaluateAsync(
        OrderCorrelationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var observacion = await evaluator.EvaluateAsync(request, cancellationToken);
        return CommercialReadOnlyEvaluationProjection.Proyectar(observacion);
    }
}

/// <summary>
/// Registro opcional de la evaluación comercial. <b>Deliberadamente no se llama desde
/// <c>SalesModule</c>.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>No pasa por <see cref="CandidateSalesCommercialRegistration.AddCandidateSalesCommerce"/></b>:
/// no registra el aplicador, el journal ni la pagabilidad legada.
/// </para>
/// <para>
/// <b>Registra solo el contrato</b>, por fábrica: el evaluador se construye dentro con el
/// <see cref="SalesDbContext"/> del ámbito y no queda en el contenedor. Sin M03
/// registrado, resolver el contrato falla en vez de inventar una base.
/// </para>
/// <para>Una extensión opt-in no es una autorización productiva.</para>
/// </remarks>
public static class CandidateSalesReadOnlyEvaluationRegistration
{
    /// <summary>Registra <see cref="ICommercialReadOnlyEvaluationCandidate"/>, scoped, y nada más.</summary>
    public static IServiceCollection AddCandidateSalesReadOnlyEvaluation(this IServiceCollection services)
    {
        services.TryAddScoped<ICommercialReadOnlyEvaluationCandidate>(proveedor =>
            new CandidateCommercialReadOnlyEvaluationAdapter(
                new CandidateCommercialReadOnlyEvaluator(proveedor.GetRequiredService<SalesDbContext>())));
        return services;
    }
}
