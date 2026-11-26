namespace Sillar.Host.AssistantBridge;

/// <summary>
/// Proyecta un DTO autoritativo tipado hacia la observación reducida del Host.
/// El tipo concreto y su proyector se aportan únicamente en la composición.
/// </summary>
/// <remarks>
/// Esta clase no declara una autoridad alternativa ni conoce CORE. El proyector
/// futuro es el único lugar que podrá interpretar Outcome, Reason, ValidUntil y
/// los hechos ratificados de licencia/activación del DTO definitivo.
/// </remarks>
public sealed class TypedAssistantAuthorityProbe<TDecision>(
    Func<CancellationToken, Task<TDecision>> evaluateAsync,
    Func<TDecision, DateTimeOffset, AssistantAuthorityObservation> projector,
    TimeProvider clock)
{
    public AssistantAuthorityProbe CreateProbe() => EvaluateAsync;

    private async ValueTask<AssistantAuthorityObservation> EvaluateAsync(
        CancellationToken cancellationToken)
    {
        var decision = await evaluateAsync(cancellationToken).ConfigureAwait(false);
        return projector(decision, clock.GetUtcNow());
    }
}
