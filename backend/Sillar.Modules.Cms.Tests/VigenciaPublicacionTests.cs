using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Tests;

public sealed class VigenciaPublicacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 2, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Activo_sin_fechas_esta_vigente() =>
        Assert.True(EstaVigente(true, null, null));

    [Fact]
    public void Inactivo_no_esta_vigente_aunque_no_tenga_fechas() =>
        Assert.False(EstaVigente(false, null, null));

    [Fact]
    public void Programado_para_despues_no_esta_vigente() =>
        Assert.False(EstaVigente(true, Ahora.AddMinutes(1), null));

    [Fact]
    public void Empieza_exactamente_ahora_y_esta_vigente() =>
        Assert.True(EstaVigente(true, Ahora, Ahora.AddDays(1)));

    [Fact]
    public void Termina_exactamente_ahora_y_ya_no_esta_vigente() =>
        Assert.False(EstaVigente(true, Ahora.AddDays(-1), Ahora));

    [Fact]
    public void Dentro_del_intervalo_esta_vigente() =>
        Assert.True(EstaVigente(true, Ahora.AddDays(-1), Ahora.AddDays(1)));

    [Fact]
    public void Banners_promociones_y_destacados_usan_la_misma_expresion()
    {
        var start = Ahora.AddDays(-1);
        var end = Ahora.AddDays(1);

        Assert.True(PublicationWindow.IsCurrent(new Banner { StartsAt = start, EndsAt = end }, Ahora));
        Assert.True(PublicationWindow.IsCurrent(new Promotion { StartsAt = start, EndsAt = end }, Ahora));
        Assert.True(PublicationWindow.IsCurrent(
            new FeaturedProduct { ProductName = "Cuaderno", StartsAt = start, EndsAt = end },
            Ahora));
    }

    private static bool EstaVigente(
        bool isActive,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt)
        => PublicationWindow.IsCurrent(
            new Banner { IsActive = isActive, StartsAt = startsAt, EndsAt = endsAt },
            Ahora);
}
