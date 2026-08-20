using Sillar.Modules.Catalog.Services;

namespace Sillar.Modules.Catalog.Tests;

/// <summary>Precio efectivo de una variante (SPEC regla 5).</summary>
public class ItemPricingTests
{
    [Fact]
    public void Sin_precio_propio_usa_el_de_lista()
    {
        Assert.Equal(12.50m, ItemPricing.Effective(priceOverride: null, listPrice: 12.50m));
    }

    [Fact]
    public void Con_precio_propio_lo_usa_a_el()
    {
        Assert.Equal(15m, ItemPricing.Effective(priceOverride: 15m, listPrice: 12.50m));
    }

    [Fact]
    public void Nulo_en_los_dos_es_consultar_precio_no_cero()
    {
        Assert.Null(ItemPricing.Effective(priceOverride: null, listPrice: null));
    }

    [Fact]
    public void Cero_no_es_lo_mismo_que_nulo()
    {
        Assert.Equal(0m, ItemPricing.Effective(priceOverride: 0m, listPrice: 12.50m));
        Assert.NotNull(ItemPricing.Effective(priceOverride: 0m, listPrice: null));
    }

    [Fact]
    public void Un_producto_sin_presentaciones_ensena_su_precio_de_lista()
    {
        Assert.Equal((4.90m, false), ItemPricing.ForCard([], 4.90m));
    }

    [Fact]
    public void Si_todas_las_presentaciones_cuestan_igual_la_tarjeta_no_dice_desde()
    {
        Assert.Equal((4.90m, false), ItemPricing.ForCard([null, null, null], 4.90m));
        Assert.Equal((6m, false), ItemPricing.ForCard([6m, 6m], 4.90m));
    }

    [Fact]
    public void Si_cuestan_distinto_la_tarjeta_ensena_el_minimo_con_desde()
    {
        Assert.Equal((5.50m, true), ItemPricing.ForCard([8m, 5.50m, 6.20m], 4.90m));
    }

    [Fact]
    public void El_minimo_cuenta_lo_que_se_hereda_no_solo_lo_propio()
    {
        // La que no tiene precio propio cuesta el de lista, y aquí es la
        // barata: mirar solo los `price_override` daría 8.
        Assert.Equal((4.90m, true), ItemPricing.ForCard([null, 8m], 4.90m));
    }

    [Fact]
    public void Una_sola_a_consultar_deja_toda_la_tarjeta_a_consultar()
    {
        // «Desde» promete una cota, y una presentación sin precio puede
        // costar cualquier cosa. Prevalece sobre el mínimo aunque las demás
        // tengan número.
        Assert.Equal(((decimal?)null, false), ItemPricing.ForCard([null, 8m], listPrice: null));
    }

    [Fact]
    public void Gratis_es_un_precio_y_puede_ser_el_minimo()
    {
        // Cero no es «sin precio»: es el mínimo, y con otra más cara la
        // tarjeta dice «Desde gratis», que es verdad.
        Assert.Equal((0m, true), ItemPricing.ForCard([0m, 8m], 4.90m));
    }
}
