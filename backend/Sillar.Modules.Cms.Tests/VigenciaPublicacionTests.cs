using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Tests;

public sealed class VigenciaPublicacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 2, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Activo_sin_fechas_esta_vigente() =>
        Assert.True(PublicationWindow.IsCurrent(true, null, null, Ahora));

    [Fact]
    public void Inactivo_no_esta_vigente_aunque_no_tenga_fechas() =>
        Assert.False(PublicationWindow.IsCurrent(false, null, null, Ahora));

    [Fact]
    public void Programado_para_despues_no_esta_vigente() =>
        Assert.False(PublicationWindow.IsCurrent(true, Ahora.AddMinutes(1), null, Ahora));

    [Fact]
    public void Empieza_exactamente_ahora_y_esta_vigente() =>
        Assert.True(PublicationWindow.IsCurrent(true, Ahora, Ahora.AddDays(1), Ahora));

    [Fact]
    public void Termina_exactamente_ahora_y_ya_no_esta_vigente() =>
        Assert.False(PublicationWindow.IsCurrent(true, Ahora.AddDays(-1), Ahora, Ahora));

    [Fact]
    public void Dentro_del_intervalo_esta_vigente() =>
        Assert.True(PublicationWindow.IsCurrent(true, Ahora.AddDays(-1), Ahora.AddDays(1), Ahora));
}
