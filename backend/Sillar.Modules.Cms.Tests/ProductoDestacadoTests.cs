using System.Text.Json;
using Sillar.Modules.Cms.Domain;
using Sillar.Modules.Cms.Dtos;

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

    [Fact]
    public void Producto_no_publico_no_puede_publicarse()
    {
        var featured = new FeaturedProduct
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Producto preparado",
            ProductIsPublic = false
        };

        Assert.False(FeaturedProductRules.HasPublicProduct().Compile()(featured));
    }

    [Fact]
    public void Producto_publico_y_enlazado_puede_publicarse()
    {
        var featured = new FeaturedProduct
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Producto publicado",
            ProductIsPublic = true
        };

        Assert.True(FeaturedProductRules.HasPublicProduct().Compile()(featured));
    }

    [Fact]
    public void Precio_a_consultar_gratis_y_positivo_son_validos()
    {
        Assert.Null(FeaturedProductRules.ValidateSnapshotValues(null, null));
        Assert.Null(FeaturedProductRules.ValidateSnapshotValues(0m, null));
        Assert.Null(FeaturedProductRules.ValidateSnapshotValues(8m, null));
    }

    [Fact]
    public void Precio_negativo_se_rechaza() =>
        Assert.Equal(
            "El precio del producto no puede ser negativo.",
            FeaturedProductRules.ValidateSnapshotValues(-0.01m, null));

    [Fact]
    public void Respuesta_conserva_precio_nulo_cero_y_positivo_como_estados_distintos()
    {
        FeaturedProductResponse[] responses =
        [
            new(1, "A consultar", "a-consultar", null, null, false, null, true),
            new(2, "Gratis", "gratis", null, 0m, false, null, true),
            new(3, "Desde ocho", "desde-ocho", null, 8m, true, "Cuadernos", true)
        ];

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(
            responses,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var items = json.RootElement;

        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("productPrice").ValueKind);
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("productCategory").ValueKind);
        Assert.Equal(0m, items[1].GetProperty("productPrice").GetDecimal());
        Assert.Equal(8m, items[2].GetProperty("productPrice").GetDecimal());
        Assert.True(items[2].GetProperty("productPriceVaries").GetBoolean());
    }
}
