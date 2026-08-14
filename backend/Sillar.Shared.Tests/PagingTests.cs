using Sillar.Shared.Paging;

namespace Sillar.Shared.Tests;

/// <summary>Paginación compartida.</summary>
public class PagingTests
{
    [Fact]
    public void Sin_pedir_nada_se_devuelve_la_primera_pagina_de_cincuenta()
    {
        var page = PageRequest.Of(null, null);

        Assert.Equal(1, page.Number);
        Assert.Equal(50, page.Size);
        Assert.Equal(0, page.Skip);
    }

    [Fact]
    public void No_se_puede_pedir_mas_del_maximo()
    {
        // Sin tope, la primera consulta de una instalación con dos años de
        // auditoría vuelca la tabla entera.
        Assert.Equal(200, PageRequest.Of(1, 5000).Size);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Una_pagina_por_debajo_de_uno_se_trata_como_la_primera(int pedida)
    {
        Assert.Equal(1, PageRequest.Of(pedida, null).Number);
    }

    [Fact]
    public void Un_tamano_de_cero_o_negativo_se_lleva_al_minimo()
    {
        Assert.Equal(1, PageRequest.Of(1, 0).Size);
        Assert.Equal(1, PageRequest.Of(1, -10).Size);
    }

    [Fact]
    public void El_salto_corresponde_a_la_pagina_pedida()
    {
        Assert.Equal(100, PageRequest.Of(3, 50).Skip);
    }

    [Fact]
    public void El_total_de_paginas_redondea_hacia_arriba()
    {
        var resultado = new PagedResult<string>(["a"], Page: 1, PageSize: 50, TotalItems: 51);

        Assert.Equal(2, resultado.TotalPages);
        Assert.True(resultado.HasNext);
    }

    [Fact]
    public void Sin_resultados_no_hay_paginas_ni_siguiente()
    {
        var resultado = new PagedResult<string>([], Page: 1, PageSize: 50, TotalItems: 0);

        Assert.Equal(0, resultado.TotalPages);
        Assert.False(resultado.HasNext);
    }

    [Fact]
    public void La_ultima_pagina_no_tiene_siguiente()
    {
        var resultado = new PagedResult<string>(["a"], Page: 2, PageSize: 50, TotalItems: 51);

        Assert.False(resultado.HasNext);
    }
}
