namespace Sillar.Core.Authentication;

/// <summary>Estado de una sesión frente al reloj.</summary>
public enum SessionState
{
    /// <summary>Sirve.</summary>
    Valid,

    /// <summary>Se cerró sesión o alguien la revocó.</summary>
    Revoked,

    /// <summary>Demasiado tiempo sin usarse.</summary>
    IdleExpired,

    /// <summary>Superó el tope absoluto desde que se emitió.</summary>
    AbsoluteExpired
}

/// <summary>Vigencia de las sesiones administrativas.</summary>
public static class SessionPolicy
{
    /// <summary>Inactividad máxima. La jornada de un negocio cabe en una sesión.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(8);

    /// <summary>
    /// Tope absoluto desde la emisión: una sesión usada a diario no puede vivir
    /// indefinidamente.
    /// </summary>
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// Antigüedad mínima de <c>last_seen_at</c> para volver a escribirlo.
    /// </summary>
    /// <remarks>
    /// Sin este umbral, cada petición del panel sería una escritura en la base
    /// de datos solo para anotar que sigue ahí.
    /// </remarks>
    public static readonly TimeSpan RenewalThreshold = TimeSpan.FromMinutes(1);

    /// <summary>Juzga una sesión.</summary>
    public static SessionState Evaluate(
        DateTimeOffset issuedAt,
        DateTimeOffset lastSeenAt,
        DateTimeOffset? revokedAt,
        DateTimeOffset now)
    {
        if (revokedAt is not null)
        {
            return SessionState.Revoked;
        }

        // El tope absoluto se mira antes que la inactividad: da igual que se
        // haya usado hace un segundo si lleva más de una semana abierta.
        if (now - issuedAt > AbsoluteLifetime)
        {
            return SessionState.AbsoluteExpired;
        }

        return now - lastSeenAt > IdleTimeout ? SessionState.IdleExpired : SessionState.Valid;
    }

    /// <summary>Indica si toca reescribir <c>last_seen_at</c>.</summary>
    public static bool ShouldRenew(DateTimeOffset lastSeenAt, DateTimeOffset now)
        => now - lastSeenAt >= RenewalThreshold;

    /// <summary>
    /// Cuándo caduca la sesión: lo que ocurra antes entre la inactividad y el
    /// tope absoluto.
    /// </summary>
    public static DateTimeOffset ExpiresAt(DateTimeOffset issuedAt, DateTimeOffset lastSeenAt)
    {
        var byIdle = lastSeenAt + IdleTimeout;
        var byAbsolute = issuedAt + AbsoluteLifetime;
        return byIdle < byAbsolute ? byIdle : byAbsolute;
    }

    /// <summary>
    /// Momento antes del cual las sesiones caducadas ya no interesan y se
    /// pueden borrar. Se purgan al iniciar sesión.
    /// </summary>
    public static DateTimeOffset PurgeBefore(DateTimeOffset now) => now - AbsoluteLifetime;
}
