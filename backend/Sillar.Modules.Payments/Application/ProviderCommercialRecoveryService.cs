using Sillar.Modules.Payments.Application.Contracts;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Modela reintentos controlados sin afirmar efecto comercial hasta recibir una
/// aplicación explícita del puerto simulado/futuro.
/// </summary>
internal sealed class ProviderCommercialRecoveryService(
    IProviderCommercialEffectPort port)
{
    internal async ValueTask<ProviderCommercialRecoverySnapshot> AttemptAsync(
        ProviderCommercialEffectCandidate candidate,
        ProviderCommercialRecoverySnapshot current,
        DateTimeOffset attemptedAt,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(current);
        if (attemptedAt == default || retryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "El intento exige instante y demora de reintento positiva.");
        }

        if (current.State is ProviderCommercialRecoveryState.Applied
            or ProviderCommercialRecoveryState.AlreadyApplied
            or ProviderCommercialRecoveryState.Conflict
            or ProviderCommercialRecoveryState.DecisionRequired)
        {
            throw new InvalidOperationException(
                "Un resultado terminal no puede reintentarse.");
        }

        if (current.NextAttemptAt is { } nextAttemptAt
            && attemptedAt < nextAttemptAt)
        {
            throw new InvalidOperationException(
                "El reintento no puede adelantarse a su instante programado.");
        }

        var result = await port.TryApplyAsync(candidate, cancellationToken);
        if (result is null || !Enum.IsDefined(result.Outcome))
        {
            throw new InvalidOperationException(
                "El puerto comercial devolvió un resultado inválido.");
        }

        var attempts = checked(current.Attempts + 1);
        return result.Outcome switch
        {
            ProviderCommercialEffectOutcome.Applied => new(
                ProviderCommercialRecoveryState.Applied,
                attempts,
                result.Reference,
                null,
                attemptedAt,
                null,
                result.OrderWasExpired),
            ProviderCommercialEffectOutcome.AlreadyApplied => new(
                ProviderCommercialRecoveryState.AlreadyApplied,
                attempts,
                result.Reference,
                null,
                attemptedAt,
                null,
                result.OrderWasExpired),
            ProviderCommercialEffectOutcome.Conflict => new(
                ProviderCommercialRecoveryState.Conflict,
                attempts,
                null,
                result.Reason,
                attemptedAt,
                null,
                null),
            ProviderCommercialEffectOutcome.DecisionRequired => new(
                ProviderCommercialRecoveryState.DecisionRequired,
                attempts,
                null,
                result.Reason,
                attemptedAt,
                null,
                null),
            ProviderCommercialEffectOutcome.Indeterminate => new(
                ProviderCommercialRecoveryState.Indeterminate,
                attempts,
                null,
                result.Reason,
                attemptedAt,
                attemptedAt.Add(retryDelay),
                null),
            _ => throw new InvalidOperationException(
                "El resultado comercial no tiene transición.")
        };
    }
}
