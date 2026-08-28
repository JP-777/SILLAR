using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Sillar.Modules.Crm.Contact;

internal static class ContactSubmissionThrottlePolicy
{
    public const int MaxAttemptsPerWindow = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Límite efímero por IP para el formulario público de contacto.
/// </summary>
/// <remarks>
/// Es independiente del throttle de login: aquí no existe una cuenta que
/// proteger de enumeración ni de bloqueo dirigido. El estado vive en memoria
/// y se pierde al reiniciar el proceso.
/// </remarks>
internal sealed class ContactSubmissionThrottle(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, Counter> _ips = new();

    public bool TryAcquire(
        string? ipAddress,
        out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        // Sin IP observable no se inventa una clave global que pudiera
        // bloquear a todos los visitantes a la vez.
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return true;
        }

        var now = clock.GetUtcNow();
        var key = HashKey(ipAddress);
        var counter = _ips.GetOrAdd(
            key,
            _ => new Counter(now));

        lock (counter.Gate)
        {
            if (now - counter.WindowStarted
                >= ContactSubmissionThrottlePolicy.Window)
            {
                counter.WindowStarted = now;
                counter.Attempts = 0;
            }

            if (counter.Attempts
                >= ContactSubmissionThrottlePolicy.MaxAttemptsPerWindow)
            {
                var remaining =
                    ContactSubmissionThrottlePolicy.Window
                    - (now - counter.WindowStarted);

                retryAfter = remaining > TimeSpan.Zero
                    ? remaining
                    : TimeSpan.FromSeconds(1);

                return false;
            }

            counter.Attempts++;
            return true;
        }
    }

    private static string HashKey(string value)
        => Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));

    private sealed class Counter(DateTimeOffset started)
    {
        public object Gate { get; } = new();
        public DateTimeOffset WindowStarted { get; set; } = started;
        public int Attempts { get; set; }
    }
}
