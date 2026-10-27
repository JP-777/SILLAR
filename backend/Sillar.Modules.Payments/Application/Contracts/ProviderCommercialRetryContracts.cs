namespace Sillar.Modules.Payments.Application.Contracts;

internal sealed record ProviderCommercialRetryDecision
{
    private ProviderCommercialRetryDecision(
        DateTimeOffset? retryAt,
        string reason)
    {
        RetryAt = retryAt;
        Reason = reason;
    }

    internal DateTimeOffset? RetryAt { get; }
    internal string Reason { get; }
    internal bool ShouldRetry => RetryAt is not null;

    internal void EnsureValidFor(DateTimeOffset observedAt)
    {
        if (observedAt == default
            || (RetryAt is { } retryAt && retryAt <= observedAt))
        {
            throw new InvalidOperationException(
                "La política devolvió una decisión de reintento inválida.");
        }
    }

    internal static ProviderCommercialRetryDecision RetryAtInstant(
        DateTimeOffset retryAt)
        => retryAt == default
            ? throw new ArgumentException(
                "El reintento exige un instante.",
                nameof(retryAt))
            : new(retryAt, "automatic_retry_authorized");

    internal static ProviderCommercialRetryDecision Hold(string? reason)
        => string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException(
                "El HOLD exige una razón.",
                nameof(reason))
            : new(null, reason);
}

internal interface IProviderCommercialRetryPolicy
{
    ProviderCommercialRetryDecision Decide(
        int completedAttemptCount,
        DateTimeOffset observedAt,
        TimeSpan requestedDelay,
        string uncertaintyReason);
}

/// <summary>
/// Política segura mientras JP no ratifique un límite automático: conserva la
/// incertidumbre, pero no genera un ciclo de reintentos implícito.
/// </summary>
internal sealed class HoldIndeterminateCommercialRetryPolicy
    : IProviderCommercialRetryPolicy
{
    public ProviderCommercialRetryDecision Decide(
        int completedAttemptCount,
        DateTimeOffset observedAt,
        TimeSpan requestedDelay,
        string uncertaintyReason)
    {
        Validate(
            completedAttemptCount,
            observedAt,
            requestedDelay,
            uncertaintyReason);
        return ProviderCommercialRetryDecision.Hold(
            "automatic_retry_policy_not_ratified");
    }

    internal static void Validate(
        int completedAttemptCount,
        DateTimeOffset observedAt,
        TimeSpan requestedDelay,
        string uncertaintyReason)
    {
        if (completedAttemptCount <= 0
            || observedAt == default
            || requestedDelay <= TimeSpan.Zero
            || string.IsNullOrWhiteSpace(uncertaintyReason))
        {
            throw new ArgumentException(
                "La decisión de reintento exige intento, instante, demora y causa.");
        }
    }
}

/// <summary>
/// Política configurable para composición futura y pruebas. El límite debe ser
/// aportado por el consumidor; M11 no fija aquí una decisión numérica de JP.
/// </summary>
internal sealed class ConfiguredCommercialRetryPolicy(int maximumAttempts)
    : IProviderCommercialRetryPolicy
{
    private readonly int _maximumAttempts = maximumAttempts > 0
        ? maximumAttempts
        : throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

    public ProviderCommercialRetryDecision Decide(
        int completedAttemptCount,
        DateTimeOffset observedAt,
        TimeSpan requestedDelay,
        string uncertaintyReason)
    {
        HoldIndeterminateCommercialRetryPolicy.Validate(
            completedAttemptCount,
            observedAt,
            requestedDelay,
            uncertaintyReason);
        return completedAttemptCount < _maximumAttempts
            ? ProviderCommercialRetryDecision.RetryAtInstant(
                observedAt.Add(requestedDelay))
            : ProviderCommercialRetryDecision.Hold(
                "automatic_retry_budget_exhausted");
    }
}
