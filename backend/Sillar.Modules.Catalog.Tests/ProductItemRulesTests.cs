using Sillar.Modules.Catalog.Services;

namespace Sillar.Modules.Catalog.Tests;

/// <summary>No se puede desactivar la última variante activa de un producto activo (SPEC regla 8).</summary>
public class ProductItemRulesTests
{
    [Fact]
    public void Si_no_es_la_ultima_no_hay_bloqueo()
    {
        Assert.Null(ProductItemRules.DeactivationBlockedReason(isLastActiveVariantOfActiveProduct: false));
    }

    [Fact]
    public void Si_es_la_ultima_el_mensaje_propone_desactivar_el_producto()
    {
        var motivo = ProductItemRules.DeactivationBlockedReason(isLastActiveVariantOfActiveProduct: true);

        Assert.NotNull(motivo);
        Assert.Contains("desactiva el producto", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void El_mensaje_no_es_un_error_generico()
    {
        var motivo = ProductItemRules.DeactivationBlockedReason(isLastActiveVariantOfActiveProduct: true);

        Assert.DoesNotContain("ha ocurrido un error", motivo, StringComparison.OrdinalIgnoreCase);
    }
}
