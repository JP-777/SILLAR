using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Tests;

public sealed class ProductoDestacadoTests
{
    [Fact]
    public void Snapshot_sin_producto_esta_pendiente_de_volver_a_enlazar()
    {
        var featured = new FeaturedProduct
        {
            ProductId = null,
            ProductName = "Cuaderno cuadriculado",
            ProductSlug = "cuaderno-cuadriculado"
        };

        Assert.True(FeaturedProductRules.IsPendingRelink(featured));
    }

    [Fact]
    public void Snapshot_con_producto_no_esta_pendiente_de_volver_a_enlazar()
    {
        var featured = new FeaturedProduct
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Cuaderno cuadriculado"
        };

        Assert.False(FeaturedProductRules.IsPendingRelink(featured));
    }
}
