using Sillar.Modules.Crm.Contact;

namespace Sillar.Modules.Crm.Tests;

public sealed class ContactSubmissionThrottleTests
{
    [Fact]
    public void Cinco_intentos_entran_y_el_sexto_se_limita()
    {
        var clock = new MutableTimeProvider(
            new DateTimeOffset(
                2026, 8, 28, 22, 0, 0,
                TimeSpan.Zero));

        var throttle =
            new ContactSubmissionThrottle(clock);

        for (var attempt = 0;
             attempt
             < ContactSubmissionThrottlePolicy.MaxAttemptsPerWindow;
             attempt++)
        {
            Assert.True(
                throttle.TryAcquire(
                    "203.0.113.7",
                    out var retry));

            Assert.Equal(
                TimeSpan.Zero,
                retry);
        }

        Assert.False(
            throttle.TryAcquire(
                "203.0.113.7",
                out var blockedFor));

        Assert.True(
            blockedFor > TimeSpan.Zero);
    }

    [Fact]
    public void Otra_ip_tiene_su_propio_contador()
    {
        var throttle =
            new ContactSubmissionThrottle(
                new MutableTimeProvider(
                    DateTimeOffset.UtcNow));

        for (var attempt = 0;
             attempt
             < ContactSubmissionThrottlePolicy.MaxAttemptsPerWindow;
             attempt++)
        {
            Assert.True(
                throttle.TryAcquire(
                    "203.0.113.7",
                    out _));
        }

        Assert.True(
            throttle.TryAcquire(
                "203.0.113.8",
                out _));
    }

    [Fact]
    public void Al_terminar_la_ventana_la_ip_puede_enviar_de_nuevo()
    {
        var clock = new MutableTimeProvider(
            new DateTimeOffset(
                2026, 8, 28, 22, 0, 0,
                TimeSpan.Zero));

        var throttle =
            new ContactSubmissionThrottle(clock);

        for (var attempt = 0;
             attempt
             < ContactSubmissionThrottlePolicy.MaxAttemptsPerWindow;
             attempt++)
        {
            Assert.True(
                throttle.TryAcquire(
                    "203.0.113.7",
                    out _));
        }

        Assert.False(
            throttle.TryAcquire(
                "203.0.113.7",
                out _));

        clock.Advance(
            ContactSubmissionThrottlePolicy.Window);

        Assert.True(
            throttle.TryAcquire(
                "203.0.113.7",
                out _));
    }

    private sealed class MutableTimeProvider(
        DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow()
            => _now;

        public void Advance(TimeSpan amount)
            => _now += amount;
    }
}
