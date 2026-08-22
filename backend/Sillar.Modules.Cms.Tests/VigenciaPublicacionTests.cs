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

    public static TheoryData<bool, int?, int?, PublicationState> CasosDeEstado => new()
    {
        { false, null, null, PublicationState.Inactive },
        { true, 1, null, PublicationState.Scheduled },
        { true, -1, 1, PublicationState.Current },
        { true, -2, -1, PublicationState.Expired },
        { true, -1, null, PublicationState.Current },
        { true, 0, 1, PublicationState.Current },
        { true, -1, 0, PublicationState.Expired }
    };

    [Theory]
    [MemberData(nameof(CasosDeEstado))]
    public void Estado_administrativo_respeta_la_misma_ventana(
        bool isActive,
        int? startMinutes,
        int? endMinutes,
        PublicationState expected)
    {
        var content = new Banner
        {
            IsActive = isActive,
            StartsAt = startMinutes is { } start ? Ahora.AddMinutes(start) : null,
            EndsAt = endMinutes is { } end ? Ahora.AddMinutes(end) : null
        };

        var state = PublicationWindow.StateAt(content, Ahora);

        Assert.Equal(expected, state);
        Assert.Equal(PublicationWindow.IsCurrent(content, Ahora), state == PublicationState.Current);
    }

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

    [Fact]
    public void Banners_promociones_y_destacados_usan_la_misma_clasificacion()
    {
        var start = Ahora.AddMinutes(1);

        Assert.Equal(PublicationState.Scheduled,
            PublicationWindow.StateAt(new Banner { StartsAt = start }, Ahora));
        Assert.Equal(PublicationState.Scheduled,
            PublicationWindow.StateAt(new Promotion { StartsAt = start }, Ahora));
        Assert.Equal(PublicationState.Scheduled,
            PublicationWindow.StateAt(new FeaturedProduct { ProductName = "Cuaderno", StartsAt = start }, Ahora));
    }

    private static bool EstaVigente(
        bool isActive,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt)
        => PublicationWindow.IsCurrent(
            new Banner { IsActive = isActive, StartsAt = startsAt, EndsAt = endsAt },
            Ahora);
}
