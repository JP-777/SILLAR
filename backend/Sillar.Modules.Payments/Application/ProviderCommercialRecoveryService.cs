using Sillar.Modules.Payments.Application.Contracts;

namespace Sillar.Modules.Payments.Application;

/// <summary>
/// Modela reintentos controlados sin afirmar efecto comercial hasta recibir una
/// acreditación explícita del puerto simulado/futuro.
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

        if (current.State is ProviderCommercialRecoveryState.Accredited
            or ProviderCommercialRecoveryState.Rejected)
        {
            throw new InvalidOperationException(
                "Un resultado terminal no puede reintentarse.");
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
            ProviderCommercialEffectOutcome.Accredited => new(
                ProviderCommercialRecoveryState.Accredited,
                attempts,
                result.Reference,
                null,
                attemptedAt,
                null),
            ProviderCommercialEffectOutcome.Rejected => new(
                ProviderCommercialRecoveryState.Rejected,
                attempts,
                null,
                result.Reason,
                attemptedAt,
                null),
            ProviderCommercialEffectOutcome.Indeterminate => new(
                ProviderCommercialRecoveryState.Indeterminate,
                attempts,
                null,
                result.Reason,
                attemptedAt,
                attemptedAt.Add(retryDelay)),
            _ => throw new InvalidOperationException(
                "El resultado comercial no tiene transición.")
        };
    }
}
