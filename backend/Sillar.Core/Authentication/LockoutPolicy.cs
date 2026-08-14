namespace Sillar.Core.Authentication;

/// <summary>Bloqueo temporal tras varios intentos fallidos.</summary>
/// <remarks>
/// Se cuenta por cuenta, no por dirección de origen. El compromiso está asumido
/// en la entrega: alguien puede bloquear la cuenta de otro fallando cinco veces
/// a propósito. Se acepta porque el bloqueo es corto y queda auditado, mientras
/// que contar por IP no sirve en un local donde todo el personal comparte la
/// misma salida a internet.
/// </remarks>
public static class LockoutPolicy
{
    /// <summary>Intentos fallidos consecutivos que provocan el bloqueo.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>Cuánto dura el bloqueo.</summary>
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Devuelve hasta cuándo queda bloqueada la cuenta tras un intento fallido,
    /// o <c>null</c> si aún no toca bloquearla.
    /// </summary>
    /// <param name="failedAttempts">Intentos fallidos ya contando el actual.</param>
    /// <param name="now">Momento actual.</param>
    public static DateTimeOffset? LockedUntil(int failedAttempts, DateTimeOffset now)
        => failedAttempts >= MaxFailedAttempts ? now + LockDuration : null;

    /// <summary>Indica si la cuenta está bloqueada en este momento.</summary>
    public static bool IsLocked(DateTimeOffset? lockedUntil, DateTimeOffset now)
        => lockedUntil is { } until && until > now;
}
