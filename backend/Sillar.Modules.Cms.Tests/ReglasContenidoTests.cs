using Sillar.Modules.Cms.Services;

namespace Sillar.Modules.Cms.Tests;

public sealed class ReglasContenidoTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Fecha_de_fin_anterior_nombra_la_fecha_que_hay_que_corregir()
    {
        var error = CmsContentRules.ValidatePeriod(Inicio, Inicio.AddDays(-1));

        Assert.Contains("fecha de fin", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fecha_de_fin_igual_al_inicio_tambien_se_rechaza() =>
        Assert.NotNull(CmsContentRules.ValidatePeriod(Inicio, Inicio));

    [Fact]
    public void Enlace_sin_texto_visible_se_rechaza() =>
        Assert.NotNull(CmsContentRules.ValidateLink("/catalogo", null));

    [Theory]
    [InlineData("/catalogo")]
    [InlineData("https://sillar.example/catalogo")]
    public void Ruta_interna_y_https_con_etiqueta_se_aceptan(string url) =>
        Assert.Null(CmsContentRules.ValidateLink(url, "Ver productos"));

    [Fact]
    public void Imagen_sin_texto_alternativo_se_rechaza() =>
        Assert.NotNull(CmsContentRules.ValidateAltText(true, null));

    [Fact]
    public void Sin_imagen_el_texto_alternativo_puede_ser_nulo() =>
        Assert.Null(CmsContentRules.ValidateAltText(false, null));

    [Fact]
    public void Texto_alternativo_de_espacios_se_rechaza_aunque_no_haya_imagen() =>
        Assert.NotNull(CmsContentRules.ValidateAltText(false, "   "));

    [Theory]
    [InlineData("Instagram", "instagram")]
    [InlineData(" WHATSAPP ", "whatsapp")]
    public void Plataforma_social_se_normaliza(string original, string expected) =>
        Assert.Equal(expected, CmsContentRules.NormalizePlatform(original));

    [Fact]
    public void Plataforma_social_fuera_de_la_lista_se_rechaza() =>
        Assert.NotNull(CmsContentRules.ValidateSocialLink("myspace", "https://example.com"));
}
