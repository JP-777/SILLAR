using Sillar.Modules.Catalog.Services;

namespace Sillar.Modules.Catalog.Tests;

/// <summary>Generación y formato del slug (SPEC regla 3).</summary>
public class SlugGeneratorTests
{
    [Fact]
    public void Un_nombre_simple_se_pasa_a_minusculas_y_guiones()
    {
        Assert.Equal("cuaderno-universitario", SlugGenerator.From("Cuaderno Universitario"));
    }

    [Fact]
    public void Las_tildes_desaparecen_sin_dejar_hueco()
    {
        Assert.Equal("plumon-de-pizarra", SlugGenerator.From("Plumón de pizarra"));
    }

    [Fact]
    public void La_ene_con_virgulilla_pliega_a_ene()
    {
        Assert.Equal("cana-de-azucar", SlugGenerator.From("Caña de azúcar"));
    }

    [Fact]
    public void Los_espacios_multiples_no_dejan_guiones_dobles()
    {
        Assert.Equal("a4-100-hojas", SlugGenerator.From("A4   100    hojas"));
    }

    [Fact]
    public void No_hay_guion_al_principio_ni_al_final()
    {
        Assert.Equal("producto", SlugGenerator.From("  ¡Producto!  "));
    }

    [Theory]
    [InlineData("artesco")]
    [InlineData("a4-100-hojas")]
    [InlineData("plumon-de-pizarra-verde")]
    public void Formatos_validos_pasan(string slug)
    {
        Assert.True(SlugGenerator.IsValidFormat(slug));
    }

    [Theory]
    [InlineData("Artesco")]
    [InlineData("-artesco")]
    [InlineData("artesco-")]
    [InlineData("artesco--verde")]
    [InlineData("artesco verde")]
    [InlineData("")]
    [InlineData(null)]
    public void Formatos_invalidos_no_pasan(string? slug)
    {
        Assert.False(SlugGenerator.IsValidFormat(slug));
    }

    [Fact]
    public void Lo_que_genera_esta_clase_siempre_es_valido_para_ella_misma()
    {
        // La garantía que le importa a quien la llama: si hay algo que
        // convertir, el resultado ya pasa el CHECK de la base.
        Assert.True(SlugGenerator.IsValidFormat(SlugGenerator.From("Cuaderno Universitario Stanford A4")));
    }
}
