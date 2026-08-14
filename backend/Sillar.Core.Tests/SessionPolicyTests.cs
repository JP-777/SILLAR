using Sillar.Core.Authentication;

namespace Sillar.Core.Tests;

/// <summary>Vigencia de las sesiones administrativas.</summary>
public class SessionPolicyTests
{
    private static readonly DateTimeOffset Emision = new(2026, 8, 14, 8, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void Una_sesion_recien_usada_sirve()
    {
        var ahora = Emision.AddHours(3);

        Assert.Equal(SessionState.Valid, SessionPolicy.Evaluate(Emision, ahora.AddMinutes(-5), null, ahora));
    }

    [Fact]
    public void Ocho_horas_sin_actividad_caducan_la_sesion()
    {
        var ahora = Emision.AddHours(9);

        Assert.Equal(SessionState.IdleExpired, SessionPolicy.Evaluate(Emision, Emision.AddMinutes(30), null, ahora));
    }

    [Fact]
    public void Siete_dias_desde_la_emision_caducan_la_sesion_aunque_se_acabe_de_usar()
    {
        // El tope absoluto existe justamente para esto: una sesión usada a
        // diario no puede vivir indefinidamente.
        var ahora = Emision.AddDays(8);

        Assert.Equal(SessionState.AbsoluteExpired, SessionPolicy.Evaluate(Emision, ahora.AddSeconds(-10), null, ahora));
    }

    [Fact]
    public void Una_sesion_revocada_no_sirve_aunque_este_en_plazo()
    {
        var ahora = Emision.AddMinutes(5);

        Assert.Equal(
            SessionState.Revoked,
            SessionPolicy.Evaluate(Emision, ahora, revokedAt: ahora.AddMinutes(-1), now: ahora));
    }

    [Fact]
    public void Dentro_del_minuto_siguiente_no_se_reescribe_last_seen_at()
    {
        // Sin este umbral, cada petición del panel sería una escritura solo para
        // anotar que el usuario sigue ahí.
        var ultimoUso = Emision.AddHours(1);

        Assert.False(SessionPolicy.ShouldRenew(ultimoUso, ultimoUso.AddSeconds(30)));
    }

    [Fact]
    public void Pasado_el_minuto_se_reescribe_last_seen_at()
    {
        var ultimoUso = Emision.AddHours(1);

        Assert.True(SessionPolicy.ShouldRenew(ultimoUso, ultimoUso.AddSeconds(61)));
    }

    [Fact]
    public void La_caducidad_es_la_inactividad_mientras_no_choque_con_el_tope()
    {
        var ultimoUso = Emision.AddHours(2);

        Assert.Equal(ultimoUso.AddHours(8), SessionPolicy.ExpiresAt(Emision, ultimoUso));
    }

    [Fact]
    public void Cerca_del_septimo_dia_manda_el_tope_absoluto()
    {
        var ultimoUso = Emision.AddDays(7).AddHours(-1);

        Assert.Equal(Emision.AddDays(7), SessionPolicy.ExpiresAt(Emision, ultimoUso));
    }
}
