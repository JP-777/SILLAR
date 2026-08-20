using Sillar.Modules.Catalog.Services;
using static Sillar.Modules.Catalog.Services.Breadcrumb;

namespace Sillar.Modules.Catalog.Tests;

/// <summary>
/// Miga de pan cuando la categoría principal está desactivada. No es del
/// SPEC: nunca un enlace a algo invisible.
/// </summary>
public class BreadcrumbTests
{
    private static CategoryNode Nodo(string nombre, bool activa, Guid? padre = null)
        => new(Guid.NewGuid(), nombre.ToLowerInvariant(), nombre, padre, activa);

    [Fact]
    public void Si_la_principal_esta_activa_se_usa_ella()
    {
        var principal = Nodo("Deporte", activa: true);

        var elegida = ChooseTarget(principal, others: []);

        Assert.Equal(principal, elegida);
    }

    [Fact]
    public void Si_la_principal_esta_inactiva_se_usa_otra_activa_del_producto()
    {
        var principal = Nodo("Deporte", activa: false);
        var otra = Nodo("Juguetes", activa: true);

        var elegida = ChooseTarget(principal, others: [otra]);

        Assert.Equal(otra, elegida);
    }

    [Fact]
    public void Entre_varias_activas_se_elige_por_nombre_no_por_orden_de_llegada()
    {
        var principal = Nodo("Deporte", activa: false);
        var zeta = Nodo("Zapatillas", activa: true);
        var alfa = Nodo("Accesorios", activa: true);

        var elegida = ChooseTarget(principal, others: [zeta, alfa]);

        Assert.Equal(alfa, elegida);
    }

    [Fact]
    public void Si_ninguna_categoria_esta_activa_no_hay_miga()
    {
        var principal = Nodo("Deporte", activa: false);
        var otra = Nodo("Juguetes", activa: false);

        var elegida = ChooseTarget(principal, others: [otra]);

        Assert.Null(elegida);
    }

    [Fact]
    public void Sin_categoria_principal_se_usa_otra_activa_igual()
    {
        var otra = Nodo("Juguetes", activa: true);

        var elegida = ChooseTarget(primary: null, others: [otra]);

        Assert.Equal(otra, elegida);
    }

    [Fact]
    public void La_ruta_sube_de_la_categoria_hasta_la_raiz()
    {
        var raiz = Nodo("Ropa", activa: true);
        var media = Nodo("Deportiva", activa: true, padre: raiz.Id);
        var hoja = Nodo("Zapatillas", activa: true, padre: media.Id);

        var byId = new Dictionary<Guid, CategoryNode> { [raiz.Id] = raiz, [media.Id] = media, [hoja.Id] = hoja };

        var ruta = BuildTrail(hoja, byId);

        Assert.Equal([raiz, media, hoja], ruta);
    }

    [Fact]
    public void La_ruta_se_corta_en_el_primer_antecesor_inactivo_sin_incluirlo()
    {
        var raiz = Nodo("Ropa", activa: true);
        var media = Nodo("Deportiva", activa: false, padre: raiz.Id);
        var hoja = Nodo("Zapatillas", activa: true, padre: media.Id);

        var byId = new Dictionary<Guid, CategoryNode> { [raiz.Id] = raiz, [media.Id] = media, [hoja.Id] = hoja };

        var ruta = BuildTrail(hoja, byId);

        Assert.Equal([hoja], ruta);
        Assert.DoesNotContain(media, ruta);
        Assert.DoesNotContain(raiz, ruta);
    }
}
