using Sillar.Modules.Catalog.Services;

namespace Sillar.Modules.Catalog.Tests;

/// <summary>El tope del selector de productos del contrato de M01.</summary>
public class CatalogSelectionTests
{
    [Fact]
    public void Pedir_mas_del_tope_devuelve_el_tope()
    {
        // **Un límite que llega de fuera se acota, no se obedece.** Y hay que
        // afirmarlo aquí: con veinte productos de demostración, una prueba
        // contra la base nunca llegaría a cincuenta resultados y pasaría sin
        // haber comprobado nada.
        Assert.Equal(50, CatalogService.AcotarSeleccion(500));
        Assert.Equal(50, CatalogService.AcotarSeleccion(51));
    }

    [Fact]
    public void Pedir_cero_o_menos_devuelve_uno()
    {
        // Cero no es «todos» ni «ninguno»: es un descuido de quien llama, y
        // devolver una lista vacía haría parecer que no hay resultados.
        Assert.Equal(1, CatalogService.AcotarSeleccion(0));
        Assert.Equal(1, CatalogService.AcotarSeleccion(-10));
    }

    [Fact]
    public void Un_limite_razonable_se_respeta()
    {
        Assert.Equal(5, CatalogService.AcotarSeleccion(5));
        Assert.Equal(50, CatalogService.AcotarSeleccion(50));
    }
}
