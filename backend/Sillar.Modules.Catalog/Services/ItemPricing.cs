namespace Sillar.Modules.Catalog.Services;

/// <summary>Resolución del precio efectivo de una variante (SPEC regla 5).</summary>
public static class ItemPricing
{
    /// <summary>
    /// <c>price_override ?? list_price</c>. Nulo en los dos significa
    /// «consultar precio»; <b>nunca</b> se confunde con cero, que es gratis.
    /// </summary>
    public static decimal? Effective(decimal? priceOverride, decimal? listPrice) => priceOverride ?? listPrice;

    /// <summary>
    /// El precio que enseña la tarjeta del listado público, y si hay que
    /// decirlo con «Desde».
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La tarjeta no tiene selector de variante, pero enseña un precio.</b>
    /// Mientras todas las presentaciones cuesten lo mismo da igual cuál se
    /// enseñe; en cuanto una tiene <c>price_override</c> propio, enseñar el de
    /// lista es enseñar un número que no se cobra.
    /// </para>
    /// <para>
    /// Se enseña el <b>mínimo efectivo</b>, y con <c>Desde</c> cuando no
    /// coinciden: es la única cota que se puede prometer sin conocer la
    /// elección de quien compra.
    /// </para>
    /// <para>
    /// <b>Y si alguna es «a consultar», toda la tarjeta lo es.</b> «Desde»
    /// promete una cota, y una presentación sin precio puede costar cualquier
    /// cosa: no hay cota que prometer. Prevalece sobre el mínimo, aunque las
    /// demás tengan número.
    /// </para>
    /// </remarks>
    /// <param name="priceOverrides">
    /// El <c>price_override</c> de cada presentación <b>activa</b>, en
    /// cualquier orden. Vacío se trata como el producto sin presentaciones:
    /// manda el precio de lista.
    /// </param>
    /// <param name="listPrice">Precio de lista del producto, del que heredan las que no tienen propio.</param>
    /// <returns>
    /// El importe a enseñar —nulo si es «a consultar»— y si va precedido de
    /// «Desde».
    /// </returns>
    public static (decimal? Price, bool From) ForCard(IReadOnlyCollection<decimal?> priceOverrides, decimal? listPrice)
    {
        if (priceOverrides.Count == 0)
        {
            return (listPrice, false);
        }

        var effective = priceOverrides.Select(over => Effective(over, listPrice)).ToList();

        // Una sola sin precio contagia a toda la tarjeta, tenga la que tenga
        // el resto: ver el porqué arriba.
        if (effective.Any(price => price is null))
        {
            return (null, false);
        }

        var min = effective.Min()!.Value;
        var max = effective.Max()!.Value;

        return (min, min != max);
    }
}
