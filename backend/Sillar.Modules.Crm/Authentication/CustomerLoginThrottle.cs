using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Sillar.Modules.Crm.Authentication;

/// <summary>Reglas puras de espera creciente del login público.</summary>
internal static class CustomerLoginThrottlePolicy
{
    public static TimeSpan AccountDelay(int failures) => failures switch
    {
        <= 2 => TimeSpan.Zero,
        3 => TimeSpan.FromMilliseconds(250),
        4 => TimeSpan.FromMilliseconds(500),
        5 => TimeSpan.FromSeconds(1),
        6 => TimeSpan.FromSeconds(2),
        7 => TimeSpan.FromSeconds(4),
        _ => TimeSpan.FromSeconds(8)
    };

    // El límite por IP es deliberadamente generoso: colegios y oficinas
    // comparten una misma salida a Internet.
    public static TimeSpan IpDelay(int failures) => failures switch
    {
        <= 20 => TimeSpan.Zero,
        21 => TimeSpan.FromMilliseconds(250),
        22 => TimeSpan.FromMilliseconds(500),
        23 => TimeSpan.FromSeconds(1),
        24 => TimeSpan.FromSeconds(2),
        25 => TimeSpan.FromSeconds(4),
        _ => TimeSpan.FromSeconds(8)
    };
}

/// <summary>
/// Espera creciente en memoria por cuenta e IP. Nunca bloquea una cuenta.
/// </summary>
/// <remarks>
/// El estado es deliberadamente efímero: reiniciar el proceso reduce la
/// penalización, pero jamás deja a una persona bloqueada por datos persistidos.
/// </remarks>
internal sealed class CustomerLoginThrottle(TimeProvider clock)
{
    private static readonly TimeSpan AccountWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan IpWindow = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Counter> _accounts = new();
    private readonly ConcurrentDictionary<string, Counter> _ips = new();

    public async Task RegisterFailureAsync(
        string normalizedEmail,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        var accountFailures = Increment(
            _accounts,
            HashKey("account:" + normalizedEmail),
            now,
            AccountWindow);

        var ipFailures = string.IsNullOrWhiteSpace(ipAddress)
            ? 0
            : Increment(
                _ips,
                HashKey("ip:" + ipAddress),
                now,
                IpWindow);

        var delay = Max(
            CustomerLoginThrottlePolicy.AccountDelay(accountFailures),
            CustomerLoginThrottlePolicy.IpDelay(ipFailures));

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, clock, cancellationToken);
        }
    }

    public void RegisterSuccess(string normalizedEmail)
        => _accounts.TryRemove(
            HashKey("account:" + normalizedEmail),
            out _);

    private static int Increment(
        ConcurrentDictionary<string, Counter> source,
        string key,
        DateTimeOffset now,
        TimeSpan window)
    {
        var counter = source.GetOrAdd(key, _ => new Counter(now));

        lock (counter.Gate)
        {
            if (now - counter.WindowStarted >= window)
            {
                counter.WindowStarted = now;
                counter.Failures = 0;
            }

            counter.Failures++;
            return counter.Failures;
        }
    }

    private static string HashKey(string value)
        => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static TimeSpan Max(TimeSpan left, TimeSpan right)
        => left >= right ? left : right;

    private sealed class Counter(DateTimeOffset started)
    {
        public object Gate { get; } = new();
        public DateTimeOffset WindowStarted { get; set; } = started;
        public int Failures { get; set; }
    }
}
