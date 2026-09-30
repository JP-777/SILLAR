using Sillar.Modules.B2B.Bandeja;

namespace Sillar.Modules.B2B.Tests;

/// <summary>SPEC regla 5, como tabla completa: lo permitido y todo lo demás.</summary>
public sealed class TransicionesDeSolicitudTests
{
    private static readonly HashSet<(string, string)> Permitidas =
    [
        ("recibida", "en_revision"), ("recibida", "rechazada"),
        ("en_revision", "cotizada"), ("en_revision", "rechazada"),
        ("cotizada", "cerrada"), ("cotizada", "rechazada"),
    ];

    [Fact]
    public void Solo_se_permiten_las_seis_transiciones_de_la_regla_5()
    {
        foreach (var desde in TransicionesDeSolicitud.Todos)
        foreach (var hasta in TransicionesDeSolicitud.Todos)
        {
            Assert.True(Permitidas.Contains((desde, hasta)) == TransicionesDeSolicitud.Permitida(desde, hasta),
                $"{desde} → {hasta}");
        }
    }

    [Fact]
    public void Cerrada_y_rechazada_son_finales_y_la_frase_lo_dice()
    {
        Assert.Equal("La solicitud ya está cerrada y no admite más cambios de estado.",
            TransicionesDeSolicitud.PorQueNo("cerrada", "en_revision"));
        Assert.Contains("Puede pasar a: en revisión o rechazada.", TransicionesDeSolicitud.PorQueNo("recibida", "cerrada"));
    }

    [Fact]
    public void Un_estado_inventado_no_es_estado()
    {
        Assert.False(TransicionesDeSolicitud.EsEstado("aprobada"));
        Assert.False(TransicionesDeSolicitud.EsEstado(null));
        Assert.True(TransicionesDeSolicitud.EsEstado("en_revision"));
    }
}
